using Dapper;
using Oracle.ManagedDataAccess.Client;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Caching.Distributed;
using MongoDB.Driver;
using Mbpc.Api.Models.Config;
using Mbpc.Api.Models.Mongo;
using Mbpc.Api.Models;
using Mbpc.Api.DTOs;
using Polly;
using Polly.Retry;
using System.Data;
using System.Security.Claims;
using System.Text.Json;

namespace Mbpc.Api.Services
{
    public class ViajeManagerService : IViajeService
    {
        private readonly IMongoCollection<ViajePosicionMongo> _viajesCollection;
        private readonly IMongoCollection<ViajeDetalleMongo>  _detallesCollection;
        private readonly string                               _oracleConnectionString;
        private readonly ILogger<ViajeManagerService>         _logger;
        private readonly IWebHostEnvironment                  _env;
        private readonly IDistributedCache                    _cache;
        private readonly IHttpContextAccessor                 _httpContextAccessor;

        // ── POLÍTICAS POLLY ──────────────────────────────────────────────────
        // Retry para Oracle: 3 intentos con espera exponencial (2s, 4s, 8s)
        private static readonly AsyncRetryPolicy _oracleRetryPolicy = Policy
            .Handle<OracleException>()
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)),
                onRetry: (exception, timeSpan, retryCount, context) =>
                {
                    // El logger no es estático; se loguea desde el call-site si se necesita detalle.
                });

        // Retry para Redis: 2 intentos con espera fija de 500ms
        private static readonly AsyncRetryPolicy _redisRetryPolicy = Policy
            .Handle<Exception>(ex => ex is not OperationCanceledException)
            .WaitAndRetryAsync(
                retryCount: 2,
                sleepDurationProvider: _ => TimeSpan.FromMilliseconds(500));

        // Claves y TTL del caché.
        // Las claves incluyen el costeraId para que cada costera tenga su propio
        // espacio de caché aislado en Redis.
        private static string CacheKeyBarcosEnPuerto(int costeraId) => $"barcos:en_puerto:{costeraId}";
        private static string CacheKeyMapaViajes(int costeraId)     => $"viajes:mapa:{costeraId}";
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
            IMongoClient                  mongoClient,
            IOptions<MongoDbSettings>     mongoSettings,
            IOptions<OracleDbSettings>    oracleSettings,
            ILogger<ViajeManagerService>  logger,
            IWebHostEnvironment           env,
            IDistributedCache             cache,
            IHttpContextAccessor          httpContextAccessor)
        {
            var database = mongoClient.GetDatabase(mongoSettings.Value.DatabaseName);

            _viajesCollection = database.GetCollection<ViajePosicionMongo>(
                mongoSettings.Value.LastMbpcCollectionName);

            _detallesCollection = database.GetCollection<ViajeDetalleMongo>(
                mongoSettings.Value.DetailsMbpcCollectionName);

            _oracleConnectionString = oracleSettings.Value.ConnectionString;
            _logger              = logger;
            _env                 = env;
            _cache               = cache;
            _httpContextAccessor = httpContextAccessor;
        }

        // ── HELPER DE IDENTIDAD ──────────────────────────────────────────────

        /// <summary>
        /// Lee el Claim "CosteraId" del JWT del usuario autenticado y lo retorna
        /// como entero.
        ///   0   →  Super Admin: acceso a todas las costeras (sin filtro).
        ///  > 0  →  Operador: acceso restringido a su propia costera.
        ///  -1   →  Error de lectura / Claim ausente (el controller habrá devuelto Forbid
        ///           antes de llegar aquí, pero se retorna -1 como guardia defensiva).
        /// </summary>
        private int GetCurrentCosteraId()
        {
            var user = _httpContextAccessor.HttpContext?.User;

            if (user is null)
            {
                _logger.LogWarning("GetCurrentCosteraId: HttpContext o User es null.");
                return -1;
            }

            var claimValue = user.FindFirstValue("CosteraId");

            if (string.IsNullOrWhiteSpace(claimValue))
            {
                _logger.LogWarning("GetCurrentCosteraId: Claim 'CosteraId' ausente en el token.");
                return -1;
            }

            if (!int.TryParse(claimValue, out var costeraId))
            {
                _logger.LogWarning(
                    "GetCurrentCosteraId: Claim 'CosteraId' con valor no numérico: '{Valor}'.", claimValue);
                return -1;
            }

            return costeraId;
        }

        /// <summary>
        /// Construye el FilterDefinition de CosteraId según la identidad del usuario:
        ///   costeraId == 0  →  Filter.Empty  (Admin ve todo)
        ///   costeraId  > 0  →  Filter.Eq("CosteraId", costeraId)
        /// </summary>
        private static FilterDefinition<ViajePosicionMongo> BuildFiltroCostera(int costeraId)
        {
            if (costeraId == 0)
                return Builders<ViajePosicionMongo>.Filter.Empty;

            return Builders<ViajePosicionMongo>.Filter.Eq(v => v.CosteraId, costeraId);
        }

        /// <summary>
        /// Versión de BuildFiltroCostera para la colección de detalles.
        /// </summary>
        private static FilterDefinition<ViajeDetalleMongo> BuildFiltroCosteraDetalle(int costeraId)
        {
            if (costeraId == 0)
                return Builders<ViajeDetalleMongo>.Filter.Empty;

            return Builders<ViajeDetalleMongo>.Filter.Eq(d => d.CosteraId, costeraId);
        }

        // ── LECTURA (MongoDB) ────────────────────────────────────────────────

        /// <summary>
        /// EJE 3: El CosteraId se obtiene del contexto HTTP via GetCurrentCosteraId().
        /// Si es 0 (Admin), se usa Filter.Empty. Si es mayor a 0, se filtra estrictamente.
        /// </summary>
        public async Task<List<ViajePosicionMongo>> GetViajesAsync(int pagina = 1, int tamanio = 50)
        {
            var costeraId = GetCurrentCosteraId();
            var skip      = (pagina - 1) * tamanio;
            var filtro    = BuildFiltroCostera(costeraId);

            return await _viajesCollection
                .Find(filtro)
                .SortByDescending(v => v.MsgTime)
                .Skip(skip)
                .Limit(tamanio)
                .ToListAsync();
        }

        /// <summary>
        /// EJE 3: Busca por MMSI Y valida que el registro pertenezca a la costera
        /// del usuario autenticado. Si el MMSI existe pero no corresponde a esa costera,
        /// retorna null (mismo comportamiento que "no encontrado", para no filtrar info).
        /// Si el usuario es Admin (costeraId == 0), no aplica el filtro de costera.
        /// </summary>
        public async Task<ViajePosicionMongo?> GetViajeByMmsiAsync(string mmsi)
        {
            var costeraId    = GetCurrentCosteraId();
            var filtroMmsi   = Builders<ViajePosicionMongo>.Filter.Eq(v => v.Mmsi, mmsi);
            var filtroCostera = BuildFiltroCostera(costeraId);
            var filtroFinal  = Builders<ViajePosicionMongo>.Filter.And(filtroMmsi, filtroCostera);

            return await _viajesCollection.Find(filtroFinal).FirstOrDefaultAsync();
        }

        /// <summary>
        /// EJE 3: El filtro de estado (Amarrado/Fondeado) se combina con el filtro de
        /// CosteraId usando un And compuesto con Builders estrictos.
        /// La clave de Redis incluye el costeraId para aislar cachés por costera.
        /// Admin (costeraId == 0) obtiene todos los barcos sin restricción de costera.
        /// </summary>
        public async Task<List<BarcoPuertoDto>> GetBarcosEnPuertoAsync()
        {
            var costeraId = GetCurrentCosteraId();

            _logger.LogInformation(
                "Consultando barcos en puerto — CosteraId: {CosteraId} ({Rol}).",
                costeraId, costeraId == 0 ? "Admin" : "Operador");

            var cacheKey = CacheKeyBarcosEnPuerto(costeraId);

            // 1. Intentar leer desde Redis
            try
            {
                var cachedResult = await _redisRetryPolicy.ExecuteAsync(async () =>
                    await _cache.GetStringAsync(cacheKey));

                if (cachedResult is not null)
                {
                    _logger.LogInformation(
                        "Cache HIT: devolviendo barcos en puerto desde Redis — CosteraId: {CosteraId}.", costeraId);

                    return JsonSerializer.Deserialize<List<BarcoPuertoDto>>(cachedResult)
                           ?? new List<BarcoPuertoDto>();
                }
            }
            catch (Exception redisEx)
            {
                _logger.LogWarning(redisEx,
                    "Redis no disponible al leer caché de barcos en puerto. Consultando MongoDB directamente.");
            }

            // 2. Consultar MongoDB con filtro compuesto: estado + costera
            try
            {
                _logger.LogInformation(
                    "Cache MISS: consultando MongoDB para barcos en puerto — CosteraId: {CosteraId}.", costeraId);

                var regexAmarrado = new MongoDB.Bson.BsonRegularExpression("amarrado", "i");
                var regexFondeado = new MongoDB.Bson.BsonRegularExpression("fondeado", "i");

                var filtroEstado = Builders<ViajePosicionMongo>.Filter.Or(
                    Builders<ViajePosicionMongo>.Filter.Regex(v => v.NavegationStatusDesc, regexAmarrado),
                    Builders<ViajePosicionMongo>.Filter.Regex(v => v.NavegationStatusDesc, regexFondeado));

                var filtroCostera = BuildFiltroCostera(costeraId);
                var filtroFinal   = Builders<ViajePosicionMongo>.Filter.And(filtroEstado, filtroCostera);

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

                    _logger.LogInformation(
                        "Barcos en puerto almacenados en Redis (CosteraId: {CosteraId}, TTL: {Ttl}).",
                        costeraId, CacheTtl);
                }
                catch (Exception redisWriteEx)
                {
                    _logger.LogWarning(redisWriteEx, "No se pudo escribir en Redis. Se continuará sin caché.");
                }

                return resultado;
            }
            catch (Exception mongoEx)
            {
                _logger.LogError(mongoEx,
                    "Error al consultar MongoDB para barcos en puerto — CosteraId: {CosteraId}.", costeraId);
                return new List<BarcoPuertoDto>();
            }
        }

        // ── MAPA GEOESPACIAL (ArcGIS) ────────────────────────────────────────

        /// <summary>
        /// EJE 3: El filtro de CosteraId se aplica en la consulta a MongoDB ANTES
        /// de cruzar con detalles, evitando cargar en memoria datos de otras costeras.
        /// La caché en Redis está particionada por costeraId.
        /// Admin (costeraId == 0) ve todos los buques sin restricción.
        /// Los filtros de mmsi/nombre se aplican en memoria sobre el resultado acotado.
        /// </summary>
        public async Task<List<MapaViajeDto>> GetMapaViajesAsync(string? mmsi = null, string? nombreBuque = null)
        {
            var costeraId = GetCurrentCosteraId();
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
                    var filtroCostera        = BuildFiltroCostera(costeraId);
                    var filtroDetalleCostera = BuildFiltroCosteraDetalle(costeraId);

                    var posiciones = await _viajesCollection
                        .Find(filtroCostera)
                        .ToListAsync();

                    var detalles = await _detallesCollection
                        .Find(filtroDetalleCostera)
                        .ToListAsync();

                    var lookupDetalles = detalles
                        .Where(d => !string.IsNullOrWhiteSpace(d.VesselName))
                        .ToLookup(d => d.VesselName, StringComparer.OrdinalIgnoreCase);

                    listaCompleta = posiciones.Select(p =>
                    {
                        var detallesHomonimos = lookupDetalles[p.VesselName ?? ""];

                        // TAREA PENDIENTE ARQUITECTURA: cruzar por p.TravelId == detalle.IdViaje.
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
                            Origen  = tieneDetalle && !string.IsNullOrWhiteSpace(detalle?.Origin)
                                        ? detalle.Origin
                                        : p.Origin,
                            Destino = tieneDetalle && !string.IsNullOrWhiteSpace(detalle?.Destination)
                                        ? detalle.Destination
                                        : p.Destination,
                            TieneDetalleOperativo = tieneDetalle,
                            CantidadBarcazas = tieneDetalle ? (detalle?.Barcazas?.Count ?? 0) : 0,
                            Remolcador       = tieneDetalle ? detalle?.Remolcador?.Nombre : null
                        };
                    }).ToList();

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
                _logger.LogError(ex,
                    "Error construyendo la vista del mapa — CosteraId: {CosteraId}.", costeraId);
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
        /// EJE 3: El costeraId se obtiene del contexto HTTP y se pasa al stored
        /// procedure de Oracle como p_COSTERA_ID.
        /// </summary>
        public async Task<List<ViajeHistoricoDto>> GetHistoricoAsync(FiltroHistoricoDto filtro)
        {
            var costeraId = GetCurrentCosteraId();

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
                    "Oracle no disponible tras reintentos en GetHistoricoAsync — CosteraId: {CosteraId}. " +
                    "Activando fallback mock. Error: {Message}",
                    costeraId, ex.Message);

                return GetHistoricoMock(filtro, costeraId);
            }
        }

        // ── ESCRITURA (Oracle + CQRS) ────────────────────────────────────────

        /// <summary>
        /// Orquesta la creación completa de un viaje siguiendo el patrón CQRS:
        ///
        ///   FASE 1 — Oracle (fuente de verdad transaccional):
        ///     Llama a PKG_MBPC_VIAJES.SP_CREAR_VIAJE con reintentos Polly.
        ///     El SP retorna el ID generado (p_ID_VIAJE_GENERADO, OUT).
        ///     Si Oracle falla en producción, la excepción se propaga (sin bypass).
        ///     En desarrollo, activa el bypass DEV y continúa con un TravelId ficticio.
        ///
        ///   FASE 2 — MongoDB, colección last_mbpc (ViajePosicionMongo):
        ///     Inserta el documento de posición inicial con:
        ///       - Estado inicial "Amarrado" (regla de negocio invariable).
        ///       - TravelId obtenido de Oracle (o ficticio en DEV).
        ///       - CosteraId proveniente del DTO (ya inyectado por el Controller desde el JWT).
        ///       - Lat/Lon/Speed en 0 hasta que el feed AIS actualice la posición real.
        ///
        ///   FASE 3 — MongoDB, colección details_mbpc (ViajeDetalleMongo):
        ///     Inserta el documento de detalle operativo con:
        ///       - Los datos enriquecidos del DTO (muelle, ZOE, km, declaración Malvinas).
        ///       - CosteraId para aislar el detalle en el filtrado multitenant del mapa.
        ///       - Barcazas y Remolcador vacíos (se completan post-despacho por otro flujo).
        ///
        ///   FASE 4 — Invalidación de caché Redis:
        ///     Elimina las claves "barcos:en_puerto:{costeraId}" y "viajes:mapa:{costeraId}"
        ///     para que el frontend vea el nuevo buque en el próximo request sin esperar TTL.
        ///     Si Redis no está disponible, se loguea como Warning y el flujo NO se aborta
        ///     (degradación elegante: la caché se auto-expirará en CacheTtl = 2 min).
        ///
        /// Invariante de consistencia:
        ///   El éxito del método se define por el éxito de Oracle (FASE 1).
        ///   Un fallo en MongoDB (FASE 2 o 3) se loguea como Error pero NO revierte Oracle,
        ///   ya que Oracle es la fuente de verdad y MongoDB es la proyección de lectura
        ///   (consistencia eventual aceptada en el diseño CQRS del sistema).
        /// </summary>
        public async Task<bool> IniciarViajeAsync(NuevoViajeDto nuevoViaje)
        {
            _logger.LogInformation(
                "IniciarViajeAsync — Inicio para Buque: '{Buque}' | Origen: '{Origen}' | Destino: '{Destino}' | CosteraId: '{CosteraId}'",
                nuevoViaje.NombreBuque, nuevoViaje.Origen, nuevoViaje.Destino, nuevoViaje.CosteraId);

            // ── Resolución del CosteraId numérico ─────────────────────────────
            // El DTO trae CosteraId como string (para compatibilidad con el Claim del JWT).
            // Lo convertimos a int una sola vez para todo el flujo.
            if (!int.TryParse(nuevoViaje.CosteraId, out var costeraIdInt))
            {
                _logger.LogError(
                    "IniciarViajeAsync abortado: CosteraId '{CosteraId}' no es un entero válido.",
                    nuevoViaje.CosteraId);
                return false;
            }

            // ── FASE 1: Escritura en Oracle ───────────────────────────────────
            long travelIdGenerado = 0;
            bool exitoOracle      = false;

            try
            {
                (exitoOracle, travelIdGenerado) = await _oracleRetryPolicy.ExecuteAsync(async () =>
                {
                    using var connection = new OracleConnection(_oracleConnectionString);
                    var parameters       = new DynamicParameters();

                    // Parámetros de entrada al SP
                    //
                    // REGLA ORACLE + DAPPER: los parámetros opcionales (nullable) DEBEN
                    // declararse con dbType explícito. Sin él, Dapper pasa DBNull sin
                    // tipo y el driver ODP.NET lanza:
                    //   "The member X of type System.DBNull cannot be used as a parameter value"
                    // Los parámetros requeridos (non-nullable) no necesitan dbType porque
                    // Dapper infiere el tipo del valor concreto.
                    parameters.Add("p_BUQUE",        nuevoViaje.NombreBuque,            dbType: DbType.String);
                    parameters.Add("p_ORIGEN",        nuevoViaje.Origen,                 dbType: DbType.String);
                    parameters.Add("p_DESTINO",       nuevoViaje.Destino,                dbType: DbType.String);
                    parameters.Add("p_MUELLE_SALIDA", nuevoViaje.MuelleSalida,           dbType: DbType.String);   // nullable — DbType obligatorio
                    parameters.Add("p_PTO_CONTROL",   nuevoViaje.ProximoPuntoControl,    dbType: DbType.String);
                    parameters.Add("p_FECHA_PARTIDA", nuevoViaje.FechaPartida,           dbType: DbType.DateTime);
                    parameters.Add("p_ETA",           nuevoViaje.ETA,                    dbType: DbType.DateTime);
                    parameters.Add("p_ZOE",           nuevoViaje.ZOE,                    dbType: DbType.String);   // nullable — DbType obligatorio
                    parameters.Add("p_POSICION",      nuevoViaje.Posicion,               dbType: DbType.String);   // nullable — DbType obligatorio
                    parameters.Add("p_KM_PAR",        nuevoViaje.RioCanalKmPar,          dbType: DbType.Decimal);  // nullable — DbType obligatorio

                    // El enum se mapea a su letra de código para el SP legacy.
                    // Ejemplo: DeclaracionMalvinasEnum.NoVieneDeMalvinas_L → "L"
                    parameters.Add("p_MALVINAS_COD",  MapDeclaracionMalvinas(nuevoViaje.DeclaracionMalvinas), dbType: DbType.String);
                    parameters.Add("p_COSTERA_ID",    costeraIdInt,                      dbType: DbType.Int32);

                    // Parámetros de salida del SP
                    parameters.Add("p_RESULTADO",         dbType: DbType.Int32, direction: ParameterDirection.Output);
                    parameters.Add("p_ID_VIAJE_GENERADO", dbType: DbType.Int64, direction: ParameterDirection.Output);

                    await connection.ExecuteAsync(
                        "PKG_MBPC_VIAJES.SP_CREAR_VIAJE",
                        parameters,
                        commandType: CommandType.StoredProcedure);

                    var resultado = parameters.Get<int>("p_RESULTADO");
                    var idViaje   = parameters.Get<long>("p_ID_VIAJE_GENERADO");

                    return (resultado == 1, idViaje);
                });

                if (!exitoOracle)
                {
                    _logger.LogError(
                        "Oracle rechazó la creación del viaje (p_RESULTADO != 1) para Buque: '{Buque}'.",
                        nuevoViaje.NombreBuque);
                    return false;
                }

                _logger.LogInformation(
                    "FASE 1 OK — Oracle creó el viaje. TravelId: {TravelId} | Buque: '{Buque}'.",
                    travelIdGenerado, nuevoViaje.NombreBuque);
            }
            catch (OracleException ex)
            {
                if (!_env.IsDevelopment())
                {
                    _logger.LogError(ex,
                        "FASE 1 FALLO — Error de Oracle en producción al crear viaje para Buque: '{Buque}'.",
                        nuevoViaje.NombreBuque);
                    throw; // En producción, propagamos para que el Controller devuelva 500.
                }

                // Bypass DEV: permite trabajar sin Oracle en entorno de desarrollo.
                _logger.LogWarning(
                    "FASE 1 BYPASS DEV — Oracle no disponible tras reintentos. " +
                    "Simulando éxito. Buque: '{Buque}'. Error Oracle: {Message}",
                    nuevoViaje.NombreBuque, ex.Message);

                exitoOracle      = true;
                travelIdGenerado = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(); // ID ficticio reproducible
            }

            // ── FASE 2: Inserción en MongoDB — last_mbpc (ViajePosicionMongo) ─
            // Esta fase es CQRS puro: Oracle es la fuente de verdad;
            // MongoDB recibe la proyección para lectura/mapa.
            try
            {
                var nuevoDocumentoPosicion = new ViajePosicionMongo
                {
                    // Id es asignado por MongoDB al insertar (BsonId + ObjectId).
                    TravelId             = travelIdGenerado,
                    VesselName           = nuevoViaje.NombreBuque,
                    // Los buques nacen "Amarrado" (en muelle). Regla de negocio invariable.
                    NavegationStatusDesc = EstadoEtapa.Amarrado.ToString(),
                    MsgTime              = DateTime.UtcNow,
                    // Lat/Lon/Speed en 0: el feed AIS externo actualizará estos valores
                    // una vez que el buque comience a transmitir su posición real.
                    Latitude             = 0,
                    Longitude            = 0,
                    SpeedOverGround      = 0,
                    CourseOverGround     = 0,
                    Origin               = nuevoViaje.Origen,
                    Destination          = nuevoViaje.Destino,
                    // EJE 3: el CosteraId garantiza el aislamiento multitenant en las lecturas.
                    CosteraId            = costeraIdInt
                };

                await _viajesCollection.InsertOneAsync(nuevoDocumentoPosicion);

                _logger.LogInformation(
                    "FASE 2 OK — ViajePosicionMongo insertado. MongoId: '{MongoId}' | TravelId: {TravelId} | CosteraId: {CosteraId}.",
                    nuevoDocumentoPosicion.Id, travelIdGenerado, costeraIdInt);
            }
            catch (Exception mongoEx)
            {
                // Un fallo en MongoDB NO cancela el viaje: Oracle ya lo registró.
                // Se loguea como Error para alertar al equipo de ops sobre la inconsistencia temporal.
                _logger.LogError(mongoEx,
                    "FASE 2 FALLO — No se pudo insertar ViajePosicionMongo para Buque: '{Buque}', TravelId: {TravelId}. " +
                    "Oracle fue exitoso. El documento se puede re-sincronizar manualmente o vía job de reconciliación.",
                    nuevoViaje.NombreBuque, travelIdGenerado);
                // No retornamos false: la operación de negocio fue exitosa (Oracle ok).
            }

            // ── FASE 3: Inserción en MongoDB — details_mbpc (ViajeDetalleMongo) ─
            // Documento de detalle operativo: complementa ViajePosicionMongo con los
            // datos enriquecidos del formulario (muelle, ZOE, km, Malvinas, etc.).
            // Este documento es el que se muestra en el panel lateral del mapa ArcGIS.
            try
            {
                var nuevoDocumentoDetalle = new ViajeDetalleMongo
                {
                    // Id es asignado por MongoDB al insertar.
                    IdViaje    = travelIdGenerado,
                    VesselName = nuevoViaje.NombreBuque,
                    Origin     = nuevoViaje.Origen,
                    Destination = nuevoViaje.Destino,
                    // Remolcador y Barcazas: vacíos en el momento de la creación.
                    // Se completarán mediante llamadas separadas del flujo de despacho.
                    Remolcador = null,
                    Barcazas   = new List<BarcazaMongo>(),
                    // EJE 3: misma costera que el ViajePosicionMongo para garantizar
                    // que el cruce en GetMapaViajesAsync respete el aislamiento multitenant.
                    CosteraId  = costeraIdInt
                };

                await _detallesCollection.InsertOneAsync(nuevoDocumentoDetalle);

                _logger.LogInformation(
                    "FASE 3 OK — ViajeDetalleMongo insertado. MongoId: '{MongoId}' | TravelId: {TravelId} | CosteraId: {CosteraId}.",
                    nuevoDocumentoDetalle.Id, travelIdGenerado, costeraIdInt);
            }
            catch (Exception mongoExDetalle)
            {
                _logger.LogError(mongoExDetalle,
                    "FASE 3 FALLO — No se pudo insertar ViajeDetalleMongo para Buque: '{Buque}', TravelId: {TravelId}. " +
                    "La posición AIS fue insertada (Fase 2) pero el detalle operativo está ausente.",
                    nuevoViaje.NombreBuque, travelIdGenerado);
            }

            // ── FASE 4: Invalidación de caché Redis ───────────────────────────
            // Se eliminan las claves particionadas por CosteraId para que el frontend
            // vea el nuevo viaje inmediatamente en el mapa y en la lista de barcos en puerto,
            // sin esperar el TTL de 2 minutos.
            try
            {
                await _redisRetryPolicy.ExecuteAsync(async () =>
                {
                    await _cache.RemoveAsync(CacheKeyBarcosEnPuerto(costeraIdInt));
                    await _cache.RemoveAsync(CacheKeyMapaViajes(costeraIdInt));
                });

                _logger.LogInformation(
                    "FASE 4 OK — Cachés invalidadas para CosteraId '{CosteraId}' tras nuevo viaje de '{Buque}'.",
                    costeraIdInt, nuevoViaje.NombreBuque);
            }
            catch (Exception redisEx)
            {
                // Degradación elegante: si Redis no responde, la caché se auto-expirará en CacheTtl.
                // El nuevo viaje será visible en el próximo ciclo de refresco del frontend.
                _logger.LogWarning(redisEx,
                    "FASE 4 ADVERTENCIA — No se pudo invalidar la caché de Redis para CosteraId '{CosteraId}'. " +
                    "Las cachés se auto-expirarán en {Ttl}. El nuevo viaje de '{Buque}' será visible en ese plazo.",
                    costeraIdInt, CacheTtl, nuevoViaje.NombreBuque);
            }

            return true;
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
        /// las cachés de Redis de la costera afectada por el viaje.
        /// No realiza ninguna validación de negocio; esa es responsabilidad exclusiva
        /// de CambiarEstadoConValidacionAsync.
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

                // EJE 3: para invalidar la caché correcta, leemos el CosteraId del documento actualizado.
                try
                {
                    var viajeActualizado = await _viajesCollection.Find(filtro).FirstOrDefaultAsync();
                    var costeraId        = viajeActualizado?.CosteraId;

                    if (costeraId.HasValue)
                    {
                        await _redisRetryPolicy.ExecuteAsync(async () =>
                        {
                            await _cache.RemoveAsync(CacheKeyBarcosEnPuerto(costeraId.Value));
                            await _cache.RemoveAsync(CacheKeyMapaViajes(costeraId.Value));
                        });

                        _logger.LogInformation(
                            "Cachés de CosteraId '{CosteraId}' invalidadas tras cambio de estado a '{Estado}'.",
                            costeraId.Value, nuevoEstado);
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

        // ── HELPER DE MAPEO DE ENUMS ─────────────────────────────────────────

        /// <summary>
        /// Extrae el código de una sola letra del nombre del valor del enum
        /// DeclaracionMalvinasEnum para enviarlo al SP legacy.
        ///
        /// Convención de nombres del enum: [Descripcion]_[LETRA_CODIGO]
        /// Ejemplo: NoVieneDeMalvinas_L → "L"
        ///          VaAMalvinas_AutorizadoCPER_A → "A"
        ///
        /// Se extrae el último segmento después del último guion bajo (_),
        /// que siempre es la letra de código de una sola letra.
        /// </summary>
        private static string MapDeclaracionMalvinas(DeclaracionMalvinasEnum declaracion)
        {
            var nombreCompleto = declaracion.ToString();
            var ultimoSegmento = nombreCompleto.Split('_').Last();

            // Guardia defensiva: el segmento extraído debe ser exactamente una letra.
            if (ultimoSegmento.Length == 1 && char.IsLetter(ultimoSegmento[0]))
                return ultimoSegmento;

            // Si la convención del enum se rompe por algún motivo, lanzamos una excepción
            // clara durante el desarrollo para detectarlo temprano.
            throw new InvalidOperationException(
                $"El enum DeclaracionMalvinasEnum '{nombreCompleto}' no sigue la convención de nombres '_LETRA'. " +
                $"Segmento extraído: '{ultimoSegmento}'. Revise los nombres de los valores del enum.");
        }

        // ── DATOS MOCKEADOS (solo DEV — fallback Oracle) ─────────────────────

        private static List<ViajeHistoricoDto> GetHistoricoMock(FiltroHistoricoDto filtro, int costeraId)
        {
            var todos = new List<ViajeHistoricoDto>
            {
                new() { Id = "H-001", Buque = "ARA Alte. Brown",      Omi = "IMO9000001", Matricula = "ARG-0001", Origen = "Puerto Rosario",     Destino = "Puerto Buenos Aires", FechaPartida = "10/01/2026 07:00", Eta = "10/01/2026 18:00", Estado = "Finalizado", CosteraId = "1" },
                new() { Id = "H-002", Buque = "RÍO PARANÁ",           Omi = "IMO9000002", Matricula = "ARG-0002", Origen = "Puerto Corrientes",   Destino = "Puerto Buenos Aires", FechaPartida = "15/01/2026 06:30", Eta = "16/01/2026 08:00", Estado = "Finalizado", CosteraId = "1" },
                new() { Id = "H-003", Buque = "SANTA FE FLUVIAL",     Omi = "IMO9000003", Matricula = "ARG-0003", Origen = "Puerto Santa Fe",     Destino = "Puerto La Plata",     FechaPartida = "20/01/2026 08:00", Eta = "20/01/2026 20:00", Estado = "Finalizado", CosteraId = "1" },
                new() { Id = "H-004", Buque = "HIDROVÍA EXPRESS",     Omi = "IMO9000004", Matricula = "ARG-0004", Origen = "Puerto Concordia",    Destino = "Puerto Buenos Aires", FechaPartida = "02/02/2026 07:00", Eta = "03/02/2026 06:00", Estado = "Finalizado", CosteraId = "2" },
                new() { Id = "H-005", Buque = "GRAN CHACO",           Omi = "IMO9000005", Matricula = "ARG-0005", Origen = "Puerto Barranqueras", Destino = "Puerto Zárate",       FechaPartida = "14/02/2026 09:00", Eta = "16/02/2026 07:00", Estado = "Finalizado", CosteraId = "2" },
                new() { Id = "H-006", Buque = "ARA Gral. San Martín", Omi = "IMO9000006", Matricula = "ARG-0006", Origen = "Puerto Buenos Aires", Destino = "Puerto Montevideo",   FechaPartida = "01/03/2026 10:00", Eta = "01/03/2026 22:00", Estado = "Finalizado", CosteraId = "3" },
                new() { Id = "H-007", Buque = "LITORAL I",            Omi = "IMO9000007", Matricula = "ARG-0007", Origen = "Puerto Goya",         Destino = "Puerto Rosario",      FechaPartida = "10/03/2026 07:00", Eta = "11/03/2026 09:00", Estado = "Cancelado",  CosteraId = "1" },
            };

            var query = costeraId == 0
                ? todos.AsEnumerable()
                : todos.Where(v => v.CosteraId == costeraId.ToString());

            return query.Where(v =>
                (string.IsNullOrWhiteSpace(filtro.Nombre)    || v.Buque.Contains(filtro.Nombre,        StringComparison.OrdinalIgnoreCase)) &&
                (string.IsNullOrWhiteSpace(filtro.Omi)       || v.Omi.Contains(filtro.Omi,             StringComparison.OrdinalIgnoreCase)) &&
                (string.IsNullOrWhiteSpace(filtro.Matricula) || v.Matricula.Contains(filtro.Matricula,  StringComparison.OrdinalIgnoreCase)) &&
                (string.IsNullOrWhiteSpace(filtro.Origen)    || v.Origen.Contains(filtro.Origen,       StringComparison.OrdinalIgnoreCase)) &&
                (string.IsNullOrWhiteSpace(filtro.Destino)   || v.Destino.Contains(filtro.Destino,     StringComparison.OrdinalIgnoreCase))
            ).ToList();
        }
    }
}
