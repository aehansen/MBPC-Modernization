using Dapper;
using Oracle.ManagedDataAccess.Client;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Caching.Distributed;
using MongoDB.Driver;
using Mbpc.Api.Models.Config;
using Mbpc.Api.Models.Mongo;
using Mbpc.Api.DTOs;
using Polly;
using Polly.Retry;
using System.Data;
using System.Text.Json;

namespace Mbpc.Api.Services
{
    public class ViajeManagerService : IViajeService
    {
        private readonly IMongoCollection<ViajePosicionMongo> _viajesCollection;
        private readonly IMongoCollection<ViajeDetalleMongo> _detallesCollection;
        private readonly string _oracleConnectionString;
        private readonly ILogger<ViajeManagerService> _logger;
        private readonly IWebHostEnvironment _env;
        private readonly IDistributedCache _cache;

        // ── POLÍTICAS POLLY ──────────────────────────────────────────────────
        // Retry para Oracle: 3 intentos con espera exponencial (2s, 4s, 8s)
        private static readonly AsyncRetryPolicy _oracleRetryPolicy = Policy
            .Handle<OracleException>()
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)),
                onRetry: (exception, timeSpan, retryCount, context) =>
                {
                    // El logger no es estático, se loguea desde el call-site si se necesita detalle
                });

        // Retry para Redis: 2 intentos con espera fija de 500ms
        private static readonly AsyncRetryPolicy _redisRetryPolicy = Policy
            .Handle<Exception>(ex => ex is not OperationCanceledException)
            .WaitAndRetryAsync(
                retryCount: 2,
                sleepDurationProvider: _ => TimeSpan.FromMilliseconds(500));

        // Claves y TTL del caché.
        // NOTA EJE 3: las claves de caché ahora incluyen el costeraId para que cada
        // costera tenga su propio espacio de caché aislado en Redis.
        private static string CacheKeyBarcosEnPuerto(string costeraId) => $"barcos:en_puerto:{costeraId}";
        private static string CacheKeyMapaViajes(string costeraId)     => $"viajes:mapa:{costeraId}";
        private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(2);

        // ── TABLA DE TRANSICIONES VÁLIDAS (EJE 2 — Máquina de Estados) ──────
        //
        //  Estado actual  │ Zarpar (→Nav) │ Amarrar (→Ama) │ Fondear (→Fon) │ Reanudar (→Rea)
        //  ───────────────┼───────────────┼────────────────┼────────────────┼────────────────
        //  Amarrado       │      ✔        │       ✘        │       ✘        │       ✘
        //  Navegando      │      ✘        │      ✔         │      ✔         │       ✘
        //  Fondeado       │      ✘ *      │       ✘        │       ✘        │      ✔
        //  Reanudado      │      ✔        │      ✔         │      ✔         │       ✘
        //
        //  * Fondeado NO puede Zarpar directamente: primero debe Reanudar.
        private static readonly IReadOnlyDictionary<EstadoEtapa, HashSet<EstadoEtapa>>
            _transicionesPermitidas = new Dictionary<EstadoEtapa, HashSet<EstadoEtapa>>
            {
                [EstadoEtapa.Amarrado]  = new HashSet<EstadoEtapa> { EstadoEtapa.Navegando },
                [EstadoEtapa.Navegando] = new HashSet<EstadoEtapa> { EstadoEtapa.Amarrado, EstadoEtapa.Fondeado },
                [EstadoEtapa.Fondeado]  = new HashSet<EstadoEtapa> { EstadoEtapa.Reanudado },
                [EstadoEtapa.Reanudado] = new HashSet<EstadoEtapa> { EstadoEtapa.Navegando, EstadoEtapa.Amarrado, EstadoEtapa.Fondeado },
            };

        public ViajeManagerService(
            IMongoClient mongoClient,
            IOptions<MongoDbSettings> mongoSettings,
            IOptions<OracleDbSettings> oracleSettings,
            ILogger<ViajeManagerService> logger,
            IWebHostEnvironment env,
            IDistributedCache cache)
        {
            var database = mongoClient.GetDatabase(mongoSettings.Value.DatabaseName);

            _viajesCollection = database.GetCollection<ViajePosicionMongo>(
                mongoSettings.Value.LastMbpcCollectionName);

            _detallesCollection = database.GetCollection<ViajeDetalleMongo>(
                mongoSettings.Value.DetailsMbpcCollectionName);

            _oracleConnectionString = oracleSettings.Value.ConnectionString;
            _logger = logger;
            _env    = env;
            _cache  = cache;
        }

        // ── LECTURA (MongoDB) ────────────────────────────────────────────────

        /// <summary>
        /// EJE 3: Se agrega filtro Eq("CosteraId", costeraId) a la consulta base.
        /// El campo CosteraId en MongoDB identifica a qué sección costera pertenece
        /// el registro, alineado con el claim del JWT del usuario autenticado.
        /// </summary>
        public async Task<List<ViajePosicionMongo>> GetViajesAsync(string costeraId, int pagina = 1, int tamanio = 50)
        {
            var skip = (pagina - 1) * tamanio;

            var filtro = Builders<ViajePosicionMongo>.Filter
                .Eq(v => v.CosteraId, costeraId);

            return await _viajesCollection
                .Find(filtro)
                .SortByDescending(v => v.MsgTime)
                .Skip(skip)
                .Limit(tamanio)
                .ToListAsync();
        }

        /// <summary>
        /// EJE 3: Busca por MMSI Y valida que el registro pertenezca a la costera
        /// del usuario. Si el MMSI existe pero no corresponde a esa costera, retorna
        /// null (mismo comportamiento que no encontrado, para no filtrar información).
        /// </summary>
        public async Task<ViajePosicionMongo?> GetViajeByMmsiAsync(string mmsi, string costeraId)
        {
            var filtro = Builders<ViajePosicionMongo>.Filter.And(
                Builders<ViajePosicionMongo>.Filter.Eq(v => v.Mmsi, mmsi),
                Builders<ViajePosicionMongo>.Filter.Eq(v => v.CosteraId, costeraId));

            return await _viajesCollection.Find(filtro).FirstOrDefaultAsync();
        }

        /// <summary>
        /// EJE 3: El filtro de estado (Amarrado/Fondeado) se combina con el
        /// filtro de CosteraId usando un And compuesto con Builders estrictos.
        /// La clave de Redis incluye el costeraId para aislar cachés por costera.
        /// </summary>
        public async Task<List<BarcoPuertoDto>> GetBarcosEnPuertoAsync(string costeraId)
        {
            _logger.LogInformation("Consultando barcos en puerto para costera '{CosteraId}'.", costeraId);

            var cacheKey = CacheKeyBarcosEnPuerto(costeraId);

            // 1. Intentar leer desde Redis
            try
            {
                var cachedResult = await _redisRetryPolicy.ExecuteAsync(async () =>
                    await _cache.GetStringAsync(cacheKey));

                if (cachedResult is not null)
                {
                    _logger.LogInformation("Cache HIT: devolviendo barcos en puerto desde Redis para costera '{CosteraId}'.", costeraId);
                    return JsonSerializer.Deserialize<List<BarcoPuertoDto>>(cachedResult)
                           ?? new List<BarcoPuertoDto>();
                }
            }
            catch (Exception redisEx)
            {
                _logger.LogWarning(redisEx, "Redis no disponible al leer caché de barcos en puerto. Consultando MongoDB directamente.");
            }

            // 2. Consultar MongoDB con filtro compuesto: estado + costera
            try
            {
                _logger.LogInformation("Cache MISS: consultando MongoDB para barcos en puerto de costera '{CosteraId}'.", costeraId);

                var regexAmarrado = new MongoDB.Bson.BsonRegularExpression("amarrado", "i");
                var regexFondeado = new MongoDB.Bson.BsonRegularExpression("fondeado", "i");

                // EJE 3: filtro AND( OR(Amarrado, Fondeado), CosteraId == costeraId )
                var filtroEstado = Builders<ViajePosicionMongo>.Filter.Or(
                    Builders<ViajePosicionMongo>.Filter.Regex(v => v.NavegationStatusDesc, regexAmarrado),
                    Builders<ViajePosicionMongo>.Filter.Regex(v => v.NavegationStatusDesc, regexFondeado));

                var filtroCostera = Builders<ViajePosicionMongo>.Filter
                    .Eq(v => v.CosteraId, costeraId);

                var filtroFinal = Builders<ViajePosicionMongo>.Filter.And(filtroEstado, filtroCostera);

                var posicionesMongo = await _viajesCollection
                    .Find(filtroFinal)
                    .SortByDescending(v => v.MsgTime)
                    .Limit(50)
                    .ToListAsync();

                var resultado = posicionesMongo.Select(p => new BarcoPuertoDto
                {
                    Id      = p.Id,
                    Buque   = p.VesselName ?? "DESCONOCIDO",
                    Origen  = p.Origin      ?? "No registrado",
                    Destino = p.Destination ?? "No registrado",
                    Eta     = p.MsgTime != default
                                  ? p.MsgTime.ToString("dd/MM/yyyy HH:mm")
                                  : "No registrado",
                    Estado  = p.NavegationStatusDesc ?? "N/A",
                    Mmsi    = p.Mmsi ?? string.Empty
                }).ToList();

                // 3. Guardar en Redis (fire-and-forget tolerante a fallos)
                try
                {
                    var serialized   = JsonSerializer.Serialize(resultado);
                    var cacheOptions = new DistributedCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = CacheTtl
                    };

                    await _redisRetryPolicy.ExecuteAsync(async () =>
                        await _cache.SetStringAsync(cacheKey, serialized, cacheOptions));

                    _logger.LogInformation("Barcos en puerto de costera '{CosteraId}' almacenados en Redis (TTL: {Ttl}).", costeraId, CacheTtl);
                }
                catch (Exception redisWriteEx)
                {
                    _logger.LogWarning(redisWriteEx, "No se pudo escribir en Redis. Se continuará sin caché.");
                }

                return resultado;
            }
            catch (Exception mongoEx)
            {
                _logger.LogError(mongoEx, "Error al consultar MongoDB para barcos en puerto de costera '{CosteraId}'.", costeraId);
                return new List<BarcoPuertoDto>();
            }
        }

        // ── MAPA GEOESPACIAL (ArcGIS) ────────────────────────────────────────

        /// <summary>
        /// EJE 3: El filtro de CosteraId se aplica en la consulta a MongoDB ANTES
        /// de cruzar con detalles, evitando cargar en memoria datos de otras costeras.
        /// La caché en Redis está particionada por costeraId.
        /// Los filtros de mmsi/nombre se siguen aplicando en memoria sobre el
        /// resultado ya acotado, preservando la estrategia original de caché unificada
        /// (ahora "unificada por costera").
        /// </summary>
        public async Task<List<MapaViajeDto>> GetMapaViajesAsync(string costeraId, string? mmsi = null, string? nombreBuque = null)
        {
            List<MapaViajeDto> listaCompleta;

            var cacheKey = CacheKeyMapaViajes(costeraId);

            // 1. Intentar obtener la lista completa cruzada desde Caché
            try
            {
                var cachedData = await _redisRetryPolicy.ExecuteAsync(async () =>
                    await _cache.GetStringAsync(cacheKey));

                if (cachedData != null)
                {
                    listaCompleta = JsonSerializer.Deserialize<List<MapaViajeDto>>(cachedData)
                                    ?? new List<MapaViajeDto>();
                }
                else
                {
                    // 2. Cache Miss: filtrar por costera en MongoDB y hacer el cruce en memoria
                    // EJE 3: se reemplaza Find(_ => true) por Find con filtro de CosteraId
                    var filtroCostera = Builders<ViajePosicionMongo>.Filter
                        .Eq(v => v.CosteraId, costeraId);

                    var posiciones = await _viajesCollection
                        .Find(filtroCostera)
                        .ToListAsync();

                    // Los detalles operativos (Oracle-backed) también se filtran por costera
                    // para evitar cruces de información entre jurisdicciones.
                    var filtroDetalleCostera = Builders<ViajeDetalleMongo>.Filter
                        .Eq(d => d.CosteraId, costeraId);

                    var detalles = await _detallesCollection
                        .Find(filtroDetalleCostera)
                        .ToListAsync();

                    // ToLookup agrupa documentos con el mismo VesselName (evita excepción por duplicados).
                    var lookupDetalles = detalles
                        .Where(d => !string.IsNullOrWhiteSpace(d.VesselName))
                        .ToLookup(d => d.VesselName, StringComparer.OrdinalIgnoreCase);

                    listaCompleta = posiciones.Select(p =>
                    {
                        var detallesHomonimos = lookupDetalles[p.VesselName ?? ""];

                        // TAREA PENDIENTE ARQUITECTURA: cruzar por p.TravelId == detalle.IdViaje.
                        // Por ahora se toma el primer detalle disponible para ese nombre.
                        var detalle      = detallesHomonimos.FirstOrDefault();
                        var tieneDetalle = detalle != null;

                        return new MapaViajeDto
                        {
                            Id                  = p.Id,
                            NombreBuque         = p.VesselName ?? "DESC",
                            Mmsi                = p.Mmsi,
                            Imo                 = p.Imo,
                            Indicativo          = p.CallSign,
                            Latitud             = p.Latitude,
                            Longitud            = p.Longitude,
                            Velocidad           = p.SpeedOverGround,
                            Rumbo               = p.CourseOverGround,
                            EstadoNav           = p.NavegationStatusDesc ?? "Desconocido",
                            UltimaActualizacion = p.MsgTime != default
                                                    ? p.MsgTime.ToString("dd/MM/yyyy HH:mm")
                                                    : "N/A",

                            // Priorizamos el origen/destino del detalle (Oracle-backed); fallback al AIS
                            Origen  = tieneDetalle && !string.IsNullOrWhiteSpace(detalle?.Origin)
                                        ? detalle.Origin
                                        : p.Origin,
                            Destino = tieneDetalle && !string.IsNullOrWhiteSpace(detalle?.Destination)
                                        ? detalle.Destination
                                        : p.Destination,
                            TieneDetalleOperativo = tieneDetalle,

                            // Extra data de negocio
                            CantidadBarcazas = tieneDetalle ? (detalle?.Barcazas?.Count ?? 0) : 0,
                            Remolcador       = tieneDetalle ? detalle?.Remolcador?.Nombre : null
                        };
                    }).ToList();

                    // Guardar en caché particionada por costera
                    var serialized   = JsonSerializer.Serialize(listaCompleta);
                    var cacheOptions = new DistributedCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = CacheTtl
                    };
                    await _redisRetryPolicy.ExecuteAsync(async () =>
                        await _cache.SetStringAsync(cacheKey, serialized, cacheOptions));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error construyendo la vista del mapa para costera '{CosteraId}'.", costeraId);
                return new List<MapaViajeDto>();
            }

            // 3. Aplicar filtros opcionales en memoria sobre el resultado ya acotado por costera
            var query = listaCompleta.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(mmsi))
            {
                var mmsis = mmsi.Split(',').Select(m => m.Trim()).ToList();
                query = query.Where(v => v.Mmsi != null && mmsis.Contains(v.Mmsi));
            }

            if (!string.IsNullOrWhiteSpace(nombreBuque))
            {
                var nombres = nombreBuque.Split(',').Select(n => n.Trim().ToLower()).ToList();
                query = query.Where(v => nombres.Any(n => v.NombreBuque.ToLower().Contains(n)));
            }

            return query.ToList();
        }

        /// <summary>
        /// EJE 3: Se agrega p_COSTERA_ID como parámetro al stored procedure de Oracle,
        /// permitiendo que la query del lado de la base de datos también filtre por costera.
        /// El fallback mock también respeta el filtro de costera.
        /// </summary>
        public async Task<List<ViajeHistoricoDto>> GetHistoricoAsync(FiltroHistoricoDto filtro, string costeraId)
        {
            try
            {
                return await _oracleRetryPolicy.ExecuteAsync(async () =>
                {
                    using var connection = new OracleConnection(_oracleConnectionString);
                    var parameters = new DynamicParameters();

                    parameters.Add("p_NOMBRE",     filtro.Nombre    ?? (object)DBNull.Value);
                    parameters.Add("p_OMI",        filtro.Omi       ?? (object)DBNull.Value);
                    parameters.Add("p_MATRICULA",  filtro.Matricula ?? (object)DBNull.Value);
                    parameters.Add("p_ORIGEN",     filtro.Origen    ?? (object)DBNull.Value);
                    parameters.Add("p_DESTINO",    filtro.Destino   ?? (object)DBNull.Value);
                    parameters.Add("p_DESDE",      filtro.Desde     ?? (object)DBNull.Value);
                    parameters.Add("p_HASTA",      filtro.Hasta     ?? (object)DBNull.Value);
                    // EJE 3: parámetro de costera para filtrado en Oracle
                    parameters.Add("p_COSTERA_ID", costeraId);

                    var resultado = await connection.QueryAsync<ViajeHistoricoDto>(
                        "PKG_MBPC_VIAJES.SP_HISTORICO",
                        parameters,
                        commandType: CommandType.StoredProcedure);

                    return resultado.ToList();
                });
            }
            catch (OracleException ex)
            {
                _logger.LogWarning(
                    "Oracle no disponible tras reintentos en GetHistoricoAsync para costera '{CosteraId}'. " +
                    "Activando fallback mock. Error: {Message}",
                    costeraId,
                    ex.Message);

                // EJE 3: el mock también filtra por costera para mantener coherencia
                return GetHistoricoMock(filtro, costeraId);
            }
        }

        // ── ESCRITURA (Oracle + CQRS) ────────────────────────────────────────

        public async Task<bool> IniciarViajeAsync(NuevoViajeDto nuevoViaje)
        {
            _logger.LogInformation("Iniciando viaje en Oracle para el buque: {Buque}", nuevoViaje.NombreBuque);
            bool exitoOracle = false;

            try
            {
                exitoOracle = await _oracleRetryPolicy.ExecuteAsync(async () =>
                {
                    using var connection = new OracleConnection(_oracleConnectionString);
                    var parameters = new DynamicParameters();

                    parameters.Add("p_BUQUE",        nuevoViaje.NombreBuque);
                    parameters.Add("p_ORIGEN",        nuevoViaje.Origen);
                    parameters.Add("p_DESTINO",       nuevoViaje.Destino);
                    parameters.Add("p_MUELLE_SALIDA", nuevoViaje.MuelleSalida);
                    parameters.Add("p_PTO_CONTROL",   nuevoViaje.ProximoPuntoControl);
                    parameters.Add("p_FECHA_PARTIDA", nuevoViaje.FechaPartida);
                    parameters.Add("p_ETA",           nuevoViaje.ETA);
                    parameters.Add("p_ZOE",           nuevoViaje.ZOE);
                    parameters.Add("p_POSICION",      nuevoViaje.Posicion);
                    parameters.Add("p_KM_PAR",        nuevoViaje.RioCanalKmPar);
                    parameters.Add("p_MALVINAS_COD",  nuevoViaje.DeclaracionMalvinas.ToString());
                    parameters.Add("p_RESULTADO", dbType: DbType.Int32, direction: ParameterDirection.Output);

                    await connection.ExecuteAsync(
                        "PKG_MBPC_VIAJES.SP_CREAR_VIAJE",
                        parameters,
                        commandType: CommandType.StoredProcedure);

                    return parameters.Get<int>("p_RESULTADO") == 1;
                });
            }
            catch (OracleException ex)
            {
                if (!_env.IsDevelopment())
                {
                    _logger.LogError(ex, "Error de Oracle en producción al crear viaje para {Buque}.", nuevoViaje.NombreBuque);
                    throw;
                }

                _logger.LogWarning(
                    "Oracle no disponible tras reintentos. Bypass DEV activado. Simulando éxito al crear viaje para: {Buque}. Error: {Message}",
                    nuevoViaje.NombreBuque, ex.Message);

                exitoOracle = true; // Bypass de desarrollo
            }

            // --- INICIO LÓGICA CQRS (ACTUALIZACIÓN DUAL) ---
            if (exitoOracle)
            {
                try
                {
                    _logger.LogInformation("Sincronizando estado en MongoDB (CQRS) para el nuevo viaje de {Buque}", nuevoViaje.NombreBuque);

                    var nuevoRegistroMongo = new ViajePosicionMongo
                    {
                        VesselName           = nuevoViaje.NombreBuque,
                        // Los buques nacen "Amarrado" (en muelle), no navegando
                        NavegationStatusDesc = EstadoEtapa.Amarrado.ToString(),
                        MsgTime              = DateTime.UtcNow,
                        Latitude             = 0,
                        Longitude            = 0,
                        SpeedOverGround      = 0,
                        Origin               = nuevoViaje.Origen,
                        Destination          = nuevoViaje.Destino,
                        // EJE 3: el nuevo viaje hereda la costera del DTO de creación
                        CosteraId            = nuevoViaje.CosteraId
                    };

                    await _viajesCollection.InsertOneAsync(nuevoRegistroMongo);
                    _logger.LogInformation("¡CQRS Exitoso! Viaje insertado en Mongo con estado inicial '{Estado}' para costera '{CosteraId}'.",
                        EstadoEtapa.Amarrado, nuevoViaje.CosteraId);

                    // Invalidar caché de la costera correspondiente
                    try
                    {
                        await _redisRetryPolicy.ExecuteAsync(async () =>
                        {
                            await _cache.RemoveAsync(CacheKeyBarcosEnPuerto(nuevoViaje.CosteraId));
                            await _cache.RemoveAsync(CacheKeyMapaViajes(nuevoViaje.CosteraId));
                        });

                        _logger.LogInformation("Cachés de costera '{CosteraId}' invalidadas tras nuevo viaje.", nuevoViaje.CosteraId);
                    }
                    catch (Exception redisEx)
                    {
                        _logger.LogWarning(redisEx,
                            "No se pudo invalidar la caché de Redis tras crear viaje. Se auto-expirará en {Ttl}.", CacheTtl);
                    }
                }
                catch (Exception mongoEx)
                {
                    _logger.LogError(mongoEx,
                        "Fallo al insertar en MongoDB para el nuevo viaje de {Buque}.", nuevoViaje.NombreBuque);
                }
            }
            // --- FIN LÓGICA CQRS ---

            return exitoOracle;
        }

        // ── MÁQUINA DE ESTADOS (EJE 2) ───────────────────────────────────────

        /// <summary>
        /// Zarpar: Amarrado/Reanudado → Navegando.
        /// Transición ilegal si el estado actual es Fondeado (primero debe Reanudar).
        /// </summary>
        public async Task<bool> ZarparAsync(string id)
        {
            _logger.LogInformation("Solicitud ZARPAR para viaje '{Id}'.", id);
            return await CambiarEstadoConValidacionAsync(id, EstadoEtapa.Navegando);
        }

        /// <summary>
        /// Amarrar: Navegando/Reanudado → Amarrado.
        /// </summary>
        public async Task<bool> AmarrarViajeAsync(string id)
        {
            _logger.LogInformation("Solicitud AMARRAR para viaje '{Id}'.", id);
            return await CambiarEstadoConValidacionAsync(id, EstadoEtapa.Amarrado);
        }

        /// <summary>
        /// Fondear: Navegando/Reanudado → Fondeado.
        /// </summary>
        public async Task<bool> FondearViajeAsync(string id)
        {
            _logger.LogInformation("Solicitud FONDEAR para viaje '{Id}'.", id);
            return await CambiarEstadoConValidacionAsync(id, EstadoEtapa.Fondeado);
        }

        /// <summary>
        /// Reanudar: Fondeado → Reanudado.
        /// Paso previo OBLIGATORIO para que un buque Fondeado pueda volver a Zarpar.
        /// </summary>
        public async Task<bool> ReanudarViajeAsync(string id)
        {
            _logger.LogInformation("Solicitud REANUDAR para viaje '{Id}'.", id);
            return await CambiarEstadoConValidacionAsync(id, EstadoEtapa.Reanudado);
        }

        // ── MOTOR DE VALIDACIÓN Y TRANSICIÓN (privado) ───────────────────────

        /// <summary>
        /// Orquesta la validación de transición y la escritura en MongoDB.
        /// 1. Lee el estado actual desde ViajePosicionMongo.
        /// 2. Valida la transición contra _transicionesPermitidas.
        /// 3. Si es válida, delega la escritura a CambiarEstadoNavegacionAsync.
        /// </summary>
        private async Task<bool> CambiarEstadoConValidacionAsync(string id, EstadoEtapa estadoDestino)
        {
            // 1. Leer el documento actual para obtener el estado vigente
            ViajePosicionMongo? viajeActual;
            try
            {
                var filtro = BuildFiltroViaje(id);
                viajeActual = await _viajesCollection.Find(filtro).FirstOrDefaultAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al leer el estado actual del viaje '{Id}' antes de la transición.", id);
                return false;
            }

            if (viajeActual is null)
            {
                _logger.LogWarning("Transición abortada: no se encontró el viaje '{Id}' en MongoDB.", id);
                return false;
            }

            // 2. Parsear el estado actual desde el string almacenado en ViajePosicionMongo.
            //    ViajePosicionMongo mantiene string por compatibilidad con el feed AIS externo.
            if (!Enum.TryParse<EstadoEtapa>(viajeActual.NavegationStatusDesc, ignoreCase: true, out var estadoActual))
            {
                // Estado no mapeado (ej: valor AIS externo como "Under Way Using Engine").
                // Permitimos la transición con advertencia para no bloquear buques del feed AIS.
                _logger.LogWarning(
                    "Estado '{EstadoRaw}' del viaje '{Id}' no es un EstadoEtapa reconocido. " +
                    "Se omite la validación de transición y se aplica '{Destino}' de todas formas.",
                    viajeActual.NavegationStatusDesc, id, estadoDestino);

                return await CambiarEstadoNavegacionAsync(id, estadoDestino.ToString());
            }

            // 3. Validar la transición contra la tabla de reglas de negocio
            if (!_transicionesPermitidas.TryGetValue(estadoActual, out var transicionesValidas)
                || !transicionesValidas.Contains(estadoDestino))
            {
                var mensajeDominio = (estadoActual, estadoDestino) switch
                {
                    (EstadoEtapa.Fondeado, EstadoEtapa.Navegando) =>
                        "Un buque FONDEADO no puede ZARPAR directamente. " +
                        "Primero debe ejecutar REANUDAR (Fondeado → Reanudado → Navegando).",

                    (EstadoEtapa.Amarrado,  EstadoEtapa.Amarrado)  or
                    (EstadoEtapa.Fondeado,  EstadoEtapa.Fondeado)  or
                    (EstadoEtapa.Navegando, EstadoEtapa.Navegando) or
                    (EstadoEtapa.Reanudado, EstadoEtapa.Reanudado) =>
                        $"El buque ya se encuentra en estado '{estadoActual}'. No se aplica ningún cambio.",

                    _ =>
                        $"Transición ilegal: '{estadoActual}' → '{estadoDestino}' no está permitida " +
                        "por las reglas de negocio del sistema MBPC."
                };

                _logger.LogError(
                    "TRANSICIÓN ILEGAL para viaje '{Id}'. Estado actual: '{Actual}'. " +
                    "Estado destino: '{Destino}'. Detalle: {Mensaje}",
                    id, estadoActual, estadoDestino, mensajeDominio);

                // Retornamos false (no lanzamos excepción) para mantener coherencia
                // con el contrato bool del servicio. El controller puede exponer el log
                // al cliente via ProblemDetails si lo requiere.
                return false;
            }

            // 4. Transición válida — ejecutar la escritura
            _logger.LogInformation(
                "Transición VÁLIDA para viaje '{Id}': '{Actual}' → '{Destino}'. Ejecutando update en MongoDB.",
                id, estadoActual, estadoDestino);

            return await CambiarEstadoNavegacionAsync(id, estadoDestino.ToString());
        }

        /// <summary>
        /// Construye el FilterDefinition aceptando tanto ObjectId (24 hex chars)
        /// como VesselName (fallback). Extraído como helper para evitar duplicación.
        /// </summary>
        private static FilterDefinition<ViajePosicionMongo> BuildFiltroViaje(string id)
        {
            if (id.Length == 24 && MongoDB.Bson.ObjectId.TryParse(id, out var objectId))
                return Builders<ViajePosicionMongo>.Filter.Eq("_id", objectId);

            return Builders<ViajePosicionMongo>.Filter.Eq(v => v.VesselName, id);
        }

        /// <summary>
        /// Escritura pura: ejecuta el Update.Set sobre NavegationStatusDesc e invalida
        /// las cachés de Redis de TODAS las costeras afectadas por el viaje.
        /// No realiza ninguna validación de negocio; esa es
        /// responsabilidad exclusiva de CambiarEstadoConValidacionAsync.
        /// </summary>
        private async Task<bool> CambiarEstadoNavegacionAsync(string id, string nuevoEstado)
        {
            try
            {
                var filtro = BuildFiltroViaje(id);

                var update = Builders<ViajePosicionMongo>.Update
                    .Set(v => v.NavegationStatusDesc, nuevoEstado);

                var result = await _viajesCollection.UpdateOneAsync(filtro, update);

                if (result.MatchedCount == 0)
                {
                    _logger.LogWarning(
                        "No se encontró documento con id/nombre '{Id}' para cambiar estado.", id);
                    return false;
                }

                _logger.LogInformation(
                    "¡CQRS Exitoso! NavegationStatusDesc actualizado a '{Estado}' para '{Id}'.", nuevoEstado, id);

                // EJE 3: para invalidar la caché correcta, leemos el CosteraId del documento
                // actualizado. Si no se puede obtener, no bloqueamos el flujo.
                try
                {
                    var viajeActualizado = await _viajesCollection.Find(filtro).FirstOrDefaultAsync();
                    var costeraId        = viajeActualizado?.CosteraId;

                    if (!string.IsNullOrWhiteSpace(costeraId))
                    {
                        await _redisRetryPolicy.ExecuteAsync(async () =>
                        {
                            await _cache.RemoveAsync(CacheKeyBarcosEnPuerto(costeraId));
                            await _cache.RemoveAsync(CacheKeyMapaViajes(costeraId));
                        });

                        _logger.LogInformation(
                            "Cachés de costera '{CosteraId}' invalidadas tras cambio de estado a '{Estado}'.",
                            costeraId, nuevoEstado);
                    }
                    else
                    {
                        _logger.LogWarning(
                            "No se pudo determinar CosteraId tras cambio de estado para '{Id}'. " +
                            "Las cachés se auto-expirarán en {Ttl}.", id, CacheTtl);
                    }
                }
                catch (Exception redisEx)
                {
                    _logger.LogWarning(redisEx,
                        "No se pudo invalidar Redis tras cambio de estado. Se auto-expirará en {Ttl}.", CacheTtl);
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error al cambiar NavegationStatusDesc a '{Estado}' para '{Id}'.", nuevoEstado, id);
                return false;
            }
        }

        // ── DATOS MOCKEADOS (solo DEV — fallback Oracle) ─────────────────────

        /// <summary>
        /// EJE 3: El mock ahora filtra por costeraId además de los filtros de búsqueda.
        /// Los registros mock incluyen CosteraId para simular el comportamiento real.
        /// </summary>
        private static List<ViajeHistoricoDto> GetHistoricoMock(FiltroHistoricoDto filtro, string costeraId)
        {
            var todos = new List<ViajeHistoricoDto>
            {
                new() { Id = "H-001", Buque = "ARA Alte. Brown",      Omi = "IMO9000001", Matricula = "ARG-0001", Origen = "Puerto Rosario",     Destino = "Puerto Buenos Aires", FechaPartida = "10/01/2026 07:00", Eta = "10/01/2026 18:00", Estado = "Finalizado", CosteraId = "COSTERAS-RIO-PARANA" },
                new() { Id = "H-002", Buque = "RÍO PARANÁ",           Omi = "IMO9000002", Matricula = "ARG-0002", Origen = "Puerto Corrientes",   Destino = "Puerto Buenos Aires", FechaPartida = "15/01/2026 06:30", Eta = "16/01/2026 08:00", Estado = "Finalizado", CosteraId = "COSTERAS-RIO-PARANA" },
                new() { Id = "H-003", Buque = "SANTA FE FLUVIAL",     Omi = "IMO9000003", Matricula = "ARG-0003", Origen = "Puerto Santa Fe",     Destino = "Puerto La Plata",     FechaPartida = "20/01/2026 08:00", Eta = "20/01/2026 20:00", Estado = "Finalizado", CosteraId = "COSTERAS-RIO-PARANA" },
                new() { Id = "H-004", Buque = "HIDROVÍA EXPRESS",     Omi = "IMO9000004", Matricula = "ARG-0004", Origen = "Puerto Concordia",    Destino = "Puerto Buenos Aires", FechaPartida = "02/02/2026 07:00", Eta = "03/02/2026 06:00", Estado = "Finalizado", CosteraId = "COSTERAS-DELTA"      },
                new() { Id = "H-005", Buque = "GRAN CHACO",           Omi = "IMO9000005", Matricula = "ARG-0005", Origen = "Puerto Barranqueras", Destino = "Puerto Zárate",       FechaPartida = "14/02/2026 09:00", Eta = "16/02/2026 07:00", Estado = "Finalizado", CosteraId = "COSTERAS-DELTA"      },
                new() { Id = "H-006", Buque = "ARA Gral. San Martín", Omi = "IMO9000006", Matricula = "ARG-0006", Origen = "Puerto Buenos Aires", Destino = "Puerto Montevideo",   FechaPartida = "01/03/2026 10:00", Eta = "01/03/2026 22:00", Estado = "Finalizado", CosteraId = "COSTERAS-RIO-PLATA"  },
                new() { Id = "H-007", Buque = "LITORAL I",            Omi = "IMO9000007", Matricula = "ARG-0007", Origen = "Puerto Goya",         Destino = "Puerto Rosario",      FechaPartida = "10/03/2026 07:00", Eta = "11/03/2026 09:00", Estado = "Cancelado",  CosteraId = "COSTERAS-RIO-PARANA" },
            };

            return todos.Where(v =>
                v.CosteraId == costeraId &&
                (string.IsNullOrWhiteSpace(filtro.Nombre)    || v.Buque.Contains(filtro.Nombre,        StringComparison.OrdinalIgnoreCase)) &&
                (string.IsNullOrWhiteSpace(filtro.Omi)       || v.Omi.Contains(filtro.Omi,             StringComparison.OrdinalIgnoreCase)) &&
                (string.IsNullOrWhiteSpace(filtro.Matricula) || v.Matricula.Contains(filtro.Matricula,  StringComparison.OrdinalIgnoreCase)) &&
                (string.IsNullOrWhiteSpace(filtro.Origen)    || v.Origen.Contains(filtro.Origen,       StringComparison.OrdinalIgnoreCase)) &&
                (string.IsNullOrWhiteSpace(filtro.Destino)   || v.Destino.Contains(filtro.Destino,     StringComparison.OrdinalIgnoreCase))
            ).ToList();
        }
    }
}
