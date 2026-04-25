// Archivo: ViajeManagerService.cs
using Dapper;
using Oracle.ManagedDataAccess.Client;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Caching.Distributed;
using MongoDB.Driver;
using MongoDB.Bson;
using Mbpc.Api.Models.Config;
using Mbpc.Api.Models.Mongo;
using Mbpc.Api.Models;
using Mbpc.Api.DTOs;
using Mbpc.Api.Services.Auth; // <-- Usamos la nueva abstracción
using Microsoft.Extensions.Hosting;
using Polly;
using Polly.Retry;
using System.Data;
using System.Security.Claims;
using System.Text.Json;

namespace Mbpc.Api.Services
{
    public class ViajeManagerService : IViajeService
    {
        private readonly IMongoCollection<ViajePosicionMongo>  _viajesCollection;
        private readonly IMongoCollection<ViajeDetalleMongo>   _detallesCollection;
        private readonly IMongoCollection<ViajeTracklogMongo>  _tracklogCollection;
        private readonly string                                _oracleConnectionString;
        private readonly ILogger<ViajeManagerService>          _logger;
        private readonly IHostEnvironment                      _env; // <-- CAMBIADO A IHostEnvironment
        private readonly IDistributedCache                     _cache;
        private readonly ICosteraUserContext                   _costeraUserContext;

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
        //  Navegando      │      ✘        │       ✔        │       ✔        │       ✘
        //  Fondeado       │      ✘ * │       ✘        │       ✘        │       ✔
        //  Reanudado      │      ✔        │       ✔        │       ✔        │       ✘
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

        // ── CONSTANTES FÍSICAS (Posicionamiento AIS) ─────────────────────────
        private const double RADIO_TIERRA_KM              = 6371.0;
        private const double KM_POR_MILLA_NAUTICA         = 1.852;
        private const double MAX_VELOCIDAD_KNOTS          = 60.0;
        // Tolerancia de 1 segundo para evitar división por cero en reportes duplicados.
        private const double MIN_SEGUNDOS_ENTRE_REPORTES  = 1.0;

        public ViajeManagerService(
            IMongoClient                  mongoClient,
            IOptions<MongoDbSettings>     mongoSettings,
            IOptions<OracleDbSettings>    oracleSettings,
            ILogger<ViajeManagerService>  logger,
            IHostEnvironment              env, // <-- CAMBIADO A IHostEnvironment
            IDistributedCache             cache,
            ICosteraUserContext           costeraUserContext) 
        {
            var database = mongoClient.GetDatabase(mongoSettings.Value.DatabaseName);

            _viajesCollection = database.GetCollection<ViajePosicionMongo>(
                mongoSettings.Value.LastMbpcCollectionName);

            _detallesCollection = database.GetCollection<ViajeDetalleMongo>(
                mongoSettings.Value.DetailsMbpcCollectionName);

            _tracklogCollection = database.GetCollection<ViajeTracklogMongo>(
                mongoSettings.Value.TracklogCollectionName);

            _oracleConnectionString = oracleSettings.Value.ConnectionString;
            _logger                 = logger;
            _env                    = env;
            _cache                  = cache;
            _costeraUserContext     = costeraUserContext; 
        }

        // ── HELPER DE IDENTIDAD ──────────────────────────────────────────────

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
        /// EJE 3: El CosteraId se obtiene del contexto a través de la abstracción ICosteraUserContext.
        /// Si es 0 (Admin), se usa Filter.Empty. Si es mayor a 0, se filtra estrictamente.
        ///
        /// EJE FILTRADO POR NOMBRE: Si se proporciona <paramref name="nombre"/>, se combina
        /// al filtro existente un Regex case-insensitive sobre VesselName, ejecutado directamente
        /// en MongoDB antes de paginar. Esto garantiza que la búsqueda recorra toda la colección
        /// de la costera y no solo la página visual activa.
        /// </summary>
        public async Task<List<ViajePosicionMongo>> GetViajesAsync(string? nombre = null, int pagina = 1, int tamanio = 50)
        {
            var costeraId = _costeraUserContext.GetCurrentCosteraId(); // <-- Llamada directa a la abstracción
            var skip      = (pagina - 1) * tamanio;
            var filtro    = BuildFiltroCostera(costeraId);

            if (!string.IsNullOrEmpty(nombre))
            {
                filtro &= Builders<ViajePosicionMongo>.Filter.Regex(
                    v => v.VesselName,
                    new BsonRegularExpression(nombre, "i"));
            }

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
            var costeraId     = _costeraUserContext.GetCurrentCosteraId(); // <-- Llamada directa a la abstracción
            var filtroMmsi    = Builders<ViajePosicionMongo>.Filter.Eq(v => v.Mmsi, mmsi);
            var filtroCostera = BuildFiltroCostera(costeraId);
            var filtroFinal   = Builders<ViajePosicionMongo>.Filter.And(filtroMmsi, filtroCostera);

            return await _viajesCollection.Find(filtroFinal).FirstOrDefaultAsync();
        }

        /// <summary>
        /// Retorna el documento de detalle operativo junto con el TravelId relacional.
        ///
        /// El TravelId se extrae siempre desde la colección de posiciones (last_mbpc),
        /// que es la fuente fiable del cruce con Oracle. Esto garantiza que el fallback
        /// a Oracle en ConvoyManagerService funcione incluso cuando el documento de detalle
        /// en MongoDB tiene IdViaje == 0 o está ausente por demora de sincronización CQRS.
        ///
        /// Contratos de retorno:
        ///   (null, 0)          →  no se encontró la posición base; id inválido o multitenant bloqueado.
        ///   (null, travelId)   →  posición encontrada pero el detalle aún no se sincronizó a Mongo.
        ///   (detalle, travelId)→  caso nominal; ConvoyManagerService debe preferir detalle.Etapas.
        /// </summary>
        public async Task<(ViajeDetalleMongo? Detalle, long TravelId)> GetViajeDetalleByIdAsync(
            string id,
            CancellationToken ct = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(id);

            // 1. Buscar la posición base para obtener el TravelId relacional.
            //    La colección last_mbpc es la fuente de verdad del cruce con Oracle.
            var filtroPosicion = BuildFiltroViaje(id);
            var viajePosicion  = await _viajesCollection.Find(filtroPosicion).FirstOrDefaultAsync(ct);

            if (viajePosicion is null)
                return (null, 0);

            var travelId = viajePosicion.TravelId;

            // 2. Cruzar con la colección de detalles usando el TravelId (enlace fuerte)
            //    o VesselName como fallback para entornos de desarrollo sin Oracle.
            var filtroDetalleBase = travelId > 0
                ? Builders<ViajeDetalleMongo>.Filter.Eq(v => v.IdViaje, travelId)
                : Builders<ViajeDetalleMongo>.Filter.Eq(v => v.VesselName, viajePosicion.VesselName);

            // 3. Aplicar aislamiento Multitenant (EJE 3).
            int costeraId     = _costeraUserContext.GetCurrentCosteraId(); // <-- Llamada directa a la abstracción
            var filtroCostera = BuildFiltroCosteraDetalle(costeraId);
            var filtroFinal   = Builders<ViajeDetalleMongo>.Filter.And(filtroDetalleBase, filtroCostera);

            if (travelId <= 0 && string.IsNullOrWhiteSpace(viajePosicion.VesselName))
            {
                _logger.LogWarning(
                    "GetViajeDetalleByIdAsync: La posición '{Id}' no tiene TravelId ni VesselName. " +
                    "No es posible cruzar con la colección de detalles.", id);
                return (null, 0);
            }

            // 4. Retornar la tupla: el Detalle puede ser null si aún no se sincronizó.
            var detalle = await _detallesCollection.Find(filtroFinal).FirstOrDefaultAsync(ct);
            return (detalle, travelId);
        }

        /// <summary>
        /// EJE 3: El filtro de estado (Amarrado/Fondeado) se combina con el filtro de
        /// CosteraId usando un And compuesto con Builders estrictos.
        /// La clave de Redis incluye el costeraId para aislar cachés por costera.
        /// Admin (costeraId == 0) obtiene todos los barcos sin restricción de costera.
        /// </summary>
        public async Task<List<BarcoPuertoDto>> GetBarcosEnPuertoAsync()
        {
            var costeraId = _costeraUserContext.GetCurrentCosteraId(); // <-- Llamada directa a la abstracción

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

                var regexAmarrado = new BsonRegularExpression("amarrado", "i");
                var regexFondeado = new BsonRegularExpression("fondeado", "i");

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
            var costeraId = _costeraUserContext.GetCurrentCosteraId(); // <-- Llamada directa a la abstracción
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
                            CantidadBarcazas = tieneDetalle ? (detalle?.Etapas?.Sum(e => e.Barcazas?.Count ?? 0) ?? 0) : 0,
                            Remolcador       = tieneDetalle ? detalle?.Etapas?.LastOrDefault()?.Remolcador?.Nombre : null
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
        /// EJE 3: El costeraId se obtiene del contexto a través de la abstracción y se pasa al stored
        /// procedure de Oracle como p_COSTERA_ID.
        /// </summary>
        public async Task<List<ViajeHistoricoDto>> GetHistoricoAsync(FiltroHistoricoDto filtro)
        {
            var costeraId = _costeraUserContext.GetCurrentCosteraId(); // <-- Llamada directa a la abstracción

            try
            {
                return await _oracleRetryPolicy.ExecuteAsync(async () =>
                {
                    using var connection = new OracleConnection(_oracleConnectionString);
                    var parameters = new DynamicParameters();
                    parameters.Add("p_Nombre",    string.IsNullOrEmpty(filtro.Nombre)    ? (object)DBNull.Value : filtro.Nombre,    DbType.String);
                    parameters.Add("p_OMI",       string.IsNullOrEmpty(filtro.Omi)       ? (object)DBNull.Value : filtro.Omi,       DbType.String);
                    parameters.Add("p_Matricula", string.IsNullOrEmpty(filtro.Matricula) ? (object)DBNull.Value : filtro.Matricula, DbType.String);
                    parameters.Add("p_Origen",    string.IsNullOrEmpty(filtro.Origen)    ? (object)DBNull.Value : filtro.Origen,    DbType.String);
                    parameters.Add("p_Destino",   string.IsNullOrEmpty(filtro.Destino)   ? (object)DBNull.Value : filtro.Destino,   DbType.String);
                    parameters.Add("p_Desde",     filtro.Desde.HasValue ? (object)filtro.Desde.Value : DBNull.Value, DbType.Date);
                    parameters.Add("p_Hasta",     filtro.Hasta.HasValue ? (object)filtro.Hasta.Value : DBNull.Value, DbType.Date);

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
        ///       - VesselName = NombreBuque del DTO si está presente (HITO 5.9); de lo contrario
        ///         BuqueId.ToString() como valor temporal con Warning en log.
        ///
        ///   FASE 3 — MongoDB, colección details_mbpc (ViajeDetalleMongo):
        ///     Inserta el documento de detalle operativo con:
        ///       - Los datos enriquecidos del DTO (muelle, ZOE, km, declaración Malvinas).
        ///       - CosteraId para aislar el detalle en el filtrado multitenant del mapa.
        ///       - Una etapa por defecto vacía (Remolcador y Barcazas se completan post-despacho).
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
                "IniciarViajeAsync — Inicio para BuqueId: '{BuqueId}' | Origen: '{Origen}' | Destino: '{Destino}' | CosteraId: '{CosteraId}'",
                nuevoViaje.BuqueId, nuevoViaje.Origen, nuevoViaje.Destino, nuevoViaje.CosteraId);

            // ── Resolución del CosteraId numérico ─────────────────────────────
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

                    parameters.Add("p_BUQUE",        nuevoViaje.BuqueId,                dbType: DbType.Int64);
                    parameters.Add("p_ORIGEN",        nuevoViaje.Origen,                 dbType: DbType.String);
                    parameters.Add("p_DESTINO",       nuevoViaje.Destino,                dbType: DbType.String);
                    parameters.Add("p_MUELLE_SALIDA", nuevoViaje.MuelleSalida,           dbType: DbType.String);
                    parameters.Add("p_PTO_CONTROL",   nuevoViaje.ProximoPuntoControl,    dbType: DbType.String);
                    parameters.Add("p_FECHA_PARTIDA", nuevoViaje.FechaPartida,           dbType: DbType.DateTime);
                    parameters.Add("p_ETA",           nuevoViaje.ETA,                    dbType: DbType.DateTime);
                    parameters.Add("p_ZOE",           nuevoViaje.ZOE,                    dbType: DbType.String);
                    parameters.Add("p_POSICION",      nuevoViaje.Posicion,               dbType: DbType.String);
                    parameters.Add("p_KM_PAR",        nuevoViaje.RioCanalKmPar,          dbType: DbType.Decimal);
                    parameters.Add("p_MALVINAS_COD",  MapDeclaracionMalvinas(nuevoViaje.DeclaracionMalvinas), dbType: DbType.String);
                    parameters.Add("p_COSTERA_ID",    costeraIdInt,                      dbType: DbType.Int32);
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
                        "Oracle rechazó la creación del viaje (p_RESULTADO != 1) para BuqueId: '{BuqueId}'.",
                        nuevoViaje.BuqueId);
                    return false;
                }

                _logger.LogInformation(
                    "FASE 1 OK — Oracle creó el viaje. TravelId: {TravelId} | BuqueId: '{BuqueId}'.",
                    travelIdGenerado, nuevoViaje.BuqueId);
            }
            catch (OracleException ex)
            {
                if (!_env.IsDevelopment())
                {
                    _logger.LogError(ex,
                        "FASE 1 FALLO — Error de Oracle en producción al crear viaje para BuqueId: '{BuqueId}'.",
                        nuevoViaje.BuqueId);
                    throw;
                }

                // Bypass DEV: permite trabajar sin Oracle en entorno de desarrollo.
                _logger.LogWarning(
                    "FASE 1 BYPASS DEV — Oracle no disponible tras reintentos. " +
                    "Simulando éxito. BuqueId: '{BuqueId}'. Error Oracle: {Message}",
                    nuevoViaje.BuqueId, ex.Message);

                exitoOracle      = true;
                travelIdGenerado = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            }

            // ── HITO 5.9 — Resolución del nombre real del buque ──────────────
            // Se resuelve UNA SOLA VEZ antes de las fases de MongoDB para que tanto
            // last_mbpc (Fase 2) como details_mbpc (Fase 3) usen exactamente el mismo
            // valor en VesselName, garantizando que el cruce por nombre funcione en
            // ObtenerCargasDesdeMongoDb y EliminarCargaAsync.
            string vesselNameParaMongo = !string.IsNullOrWhiteSpace(nuevoViaje.NombreBuque)
                ? nuevoViaje.NombreBuque.Trim().ToUpperInvariant()
                : nuevoViaje.BuqueId.ToString();

            if (string.IsNullOrWhiteSpace(nuevoViaje.NombreBuque))
            {
                _logger.LogWarning(
                    "HITO 5.9: IniciarViajeAsync recibió NombreBuque vacío para BuqueId='{BuqueId}'. " +
                    "Se usará el ID numérico como VesselName temporal. " +
                    "El frontend debe enviar el nombre real para correcta visualización en el dashboard.",
                    nuevoViaje.BuqueId);
            }

            // ── FASE 2: Inserción en MongoDB — last_mbpc (ViajePosicionMongo) ─
            try
            {
                var nuevoDocumentoPosicion = new ViajePosicionMongo
                {
                    TravelId             = travelIdGenerado,
                    VesselName           = vesselNameParaMongo,
                    NavegationStatusDesc = EstadoEtapa.Amarrado.ToString(),
                    MsgTime              = DateTime.UtcNow,
                    Latitude             = 0,
                    Longitude            = 0,
                    SpeedOverGround      = 0,
                    CourseOverGround     = 0,
                    Origin               = nuevoViaje.Origen,
                    Destination          = nuevoViaje.Destino,
                    CosteraId            = costeraIdInt
                };

                await _viajesCollection.InsertOneAsync(nuevoDocumentoPosicion);

                _logger.LogInformation(
                    "FASE 2 OK — ViajePosicionMongo insertado. MongoId: '{MongoId}' | TravelId: {TravelId} | CosteraId: {CosteraId}.",
                    nuevoDocumentoPosicion.Id, travelIdGenerado, costeraIdInt);
            }
            catch (Exception mongoEx)
            {
                _logger.LogError(mongoEx,
                    "FASE 2 FALLO — No se pudo insertar ViajePosicionMongo para BuqueId: '{BuqueId}', TravelId: {TravelId}. " +
                    "Oracle fue exitoso. El documento se puede re-sincronizar manualmente o vía job de reconciliación.",
                    nuevoViaje.BuqueId, travelIdGenerado);
            }

            // ── FASE 3: Inserción en MongoDB — details_mbpc (ViajeDetalleMongo) ─
            try
            {
                // HITO 5.9: vesselNameParaMongo ya fue resuelto antes de Fase 2 (scope del método).
                // ViajeDetalleMongo DEBE usar el mismo valor que ViajePosicionMongo para que el
                // cruce por VesselName entre las dos colecciones funcione correctamente.
                var nuevoDocumentoDetalle = new ViajeDetalleMongo
                {
                    IdViaje     = travelIdGenerado,
                    VesselName  = vesselNameParaMongo,
                    Origin      = nuevoViaje.Origen,
                    Destination = nuevoViaje.Destino,
                    Etapas      = new List<EtapaMongo>
                    {
                        new EtapaMongo
                        {
                            EtapaId     = 1,
                            FechaInicio = DateTime.UtcNow,
                            Remolcador  = null,
                            Barcazas    = new List<BarcazaMongo>()
                        }
                    },
                    CosteraId   = costeraIdInt
                };

                await _detallesCollection.InsertOneAsync(nuevoDocumentoDetalle);

                _logger.LogInformation(
                    "FASE 3 OK — ViajeDetalleMongo insertado. MongoId: '{MongoId}' | TravelId: {TravelId} | CosteraId: {CosteraId}.",
                    nuevoDocumentoDetalle.Id, travelIdGenerado, costeraIdInt);
            }
            catch (Exception mongoExDetalle)
            {
                _logger.LogError(mongoExDetalle,
                    "FASE 3 FALLO — No se pudo insertar ViajeDetalleMongo para BuqueId: '{BuqueId}', TravelId: {TravelId}. " +
                    "La posición AIS fue insertada (Fase 2) pero el detalle operativo está ausente.",
                    nuevoViaje.BuqueId, travelIdGenerado);
            }

            // ── FASE 4: Invalidación de caché Redis ───────────────────────────
            try
            {
                await _redisRetryPolicy.ExecuteAsync(async () =>
                {
                    await _cache.RemoveAsync(CacheKeyBarcosEnPuerto(costeraIdInt));
                    await _cache.RemoveAsync(CacheKeyMapaViajes(costeraIdInt));
                });

                _logger.LogInformation(
                    "FASE 4 OK — Cachés invalidadas para CosteraId '{CosteraId}' tras nuevo viaje de BuqueId '{BuqueId}'.",
                    costeraIdInt, nuevoViaje.BuqueId);
            }
            catch (Exception redisEx)
            {
                _logger.LogWarning(redisEx,
                    "FASE 4 ADVERTENCIA — No se pudo invalidar la caché de Redis para CosteraId '{CosteraId}'. " +
                    "Las cachés se auto-expirarán en {Ttl}. El nuevo viaje de BuqueId '{BuqueId}' será visible en ese plazo.",
                    costeraIdInt, CacheTtl, nuevoViaje.BuqueId);
            }

            return true;
        }

        // ── POSICIONAMIENTO AIS (EJE 4) ──────────────────────────────────────

        /// <summary>
        /// Actualiza la posición geográfica de un buque y registra el punto en el tracklog.
        ///
        /// Flujo:
        ///   1. Recupera el documento activo de ViajePosicionMongo.
        ///   2. Calcula distancia (Haversine) y velocidad en nudos.
        ///   3. Rechaza si velocidad > 60 kn (excepción de dominio tipada).
        ///   4. Actualiza el documento activo con las nuevas coordenadas y timestamp.
        ///   5. Inserta un registro inmutable en la colección de tracklog.
        ///
        /// Retorna null si no existe el documento con ese Id para la costera autenticada.
        /// Lanza InvalidOperationException si la cinemática es físicamente inválida.
        /// </summary>
        public async Task<PosicionActualizadaResultDto?> ActualizarPosicionAsync(
            string id,
            ActualizarPosicionDto dto)
        {
            // ── 1. Recuperar posición actual ──────────────────────────────────
            var filtroId = Builders<ViajePosicionMongo>.Filter.Eq(p => p.Id, id);

            var posicionActual = await _viajesCollection
                .Find(filtroId)
                .FirstOrDefaultAsync();

            if (posicionActual is null)
                return null;

            // ── 2. Cálculo cinemático ─────────────────────────────────────────
            double distanciaKm = CalcularHaversineKm(
                posicionActual.Latitude,  posicionActual.Longitude,
                dto.Latitud,              dto.Longitud);

            double distanciaNM = distanciaKm / KM_POR_MILLA_NAUTICA;

            double segundosTranscurridos = (dto.FechaReporte - posicionActual.MsgTime).TotalSeconds;

            double velocidadKn = 0.0;

            if (segundosTranscurridos > MIN_SEGUNDOS_ENTRE_REPORTES)
            {
                double horasTranscurridas = segundosTranscurridos / 3600.0;
                velocidadKn = distanciaNM / horasTranscurridas;
            }

            // ── 3. Validación cinemática ──────────────────────────────────────
            if (velocidadKn > MAX_VELOCIDAD_KNOTS)
            {
                throw new InvalidOperationException(
                    $"Cinemática inválida: velocidad calculada de {velocidadKn:F1} kn supera el límite de " +
                    $"{MAX_VELOCIDAD_KNOTS} kn. " +
                    $"Distancia: {distanciaNM:F2} NM en {segundosTranscurridos:F0} segundos. " +
                    $"Verifique las coordenadas o el timestamp del transponder. Si el error persiste comuníquese con un administrador del sistema.");
            }

            // ── 4. Construir GeoJSON point para el campo "location" ───────────
            var nuevaLocation = new LocationMongo
            {
                Geo = new GeoMongo
                {
                    Type        = "Point",
                    Coordinates = new[] { dto.Longitud, dto.Latitud },  // GeoJSON: [lng, lat]
                }
            };

            // ── 5. Actualizar documento activo en MongoDB ─────────────────────
            var update = Builders<ViajePosicionMongo>.Update
                .Set(p => p.Latitude,        dto.Latitud)
                .Set(p => p.Longitude,       dto.Longitud)
                .Set(p => p.MsgTime,         dto.FechaReporte)
                .Set(p => p.SpeedOverGround, velocidadKn)
                .Set(p => p.Location,        nuevaLocation);

            var updateResult = await _viajesCollection.UpdateOneAsync(filtroId, update);

            if (updateResult.ModifiedCount == 0)
            {
                _logger.LogWarning(
                    "ActualizarPosicionAsync: UpdateOne no modificó ningún documento para Id '{Id}'.", id);
            }

            // ── 6. Insertar en tracklog (colección inmutable de historial) ─────
            var tracklogEntry = new ViajeTracklogMongo
            {
                PosicionId           = posicionActual.Id,
                TravelId             = posicionActual.TravelId,
                VesselName           = posicionActual.VesselName,
                Mmsi                 = posicionActual.Mmsi,
                Latitude             = dto.Latitud,
                Longitude            = dto.Longitud,
                SpeedOverGround      = velocidadKn,
                CalculatedSpeedKnots = velocidadKn,
                DistanceNM           = distanciaNM,
                NavegationStatusDesc = posicionActual.NavegationStatusDesc,
                MsgTime              = dto.FechaReporte,
                InsertedAt           = DateTime.UtcNow,
                CosteraId            = posicionActual.CosteraId,
                Location             = nuevaLocation,
            };

            await _tracklogCollection.InsertOneAsync(tracklogEntry);

            return new PosicionActualizadaResultDto
            {
                VesselName           = posicionActual.VesselName,
                Latitud              = dto.Latitud,
                Longitud             = dto.Longitud,
                VelocidadCalculadaKn = Math.Round(velocidadKn,  2),
                DistanciaRecorridaNM = Math.Round(distanciaNM,  3),
                TracklogId           = tracklogEntry.Id,
            };
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
            if (id.Length == 24 && ObjectId.TryParse(id, out var objectId))
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

        // ── FÓRMULA DE HAVERSINE ─────────────────────────────────────────────

        /// <summary>
        /// Calcula la distancia en kilómetros entre dos puntos WGS-84
        /// usando la fórmula de Haversine (error &lt; 0.5 % para distancias &lt; 20 000 km).
        /// </summary>
        private static double CalcularHaversineKm(
            double lat1, double lng1,
            double lat2, double lng2)
        {
            double dLat = ToRadians(lat2 - lat1);
            double dLng = ToRadians(lng2 - lng1);

            double a =
                Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                Math.Sin(dLng / 2) * Math.Sin(dLng / 2);

            double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

            return RADIO_TIERRA_KM * c;
        }

        private static double ToRadians(double grados) => grados * Math.PI / 180.0;

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

            if (ultimoSegmento.Length == 1 && char.IsLetter(ultimoSegmento[0]))
                return ultimoSegmento;

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
                new() { Id = "H-002", Buque = "RÍO PARANÁ",           Omi = "IMO9000002", Matricula = "ARG-0002", Origen = "Puerto Corrientes",  Destino = "Puerto Buenos Aires", FechaPartida = "15/01/2026 06:30", Eta = "16/01/2026 08:00", Estado = "Finalizado", CosteraId = "1" },
                new() { Id = "H-003", Buque = "SANTA FE FLUVIAL",     Omi = "IMO9000003", Matricula = "ARG-0003", Origen = "Puerto Santa Fe",    Destino = "Puerto La Plata",     FechaPartida = "20/01/2026 08:00", Eta = "20/01/2026 20:00", Estado = "Finalizado", CosteraId = "1" },
                new() { Id = "H-004", Buque = "HIDROVÍA EXPRESS",     Omi = "IMO9000004", Matricula = "ARG-0004", Origen = "Puerto Concordia",   Destino = "Puerto Buenos Aires", FechaPartida = "02/02/2026 07:00", Eta = "03/02/2026 06:00", Estado = "Finalizado", CosteraId = "2" },
                new() { Id = "H-005", Buque = "GRAN CHACO",           Omi = "IMO9000005", Matricula = "ARG-0005", Origen = "Puerto Barranqueras", Destino = "Puerto Zárate",      FechaPartida = "14/02/2026 09:00", Eta = "16/02/2026 07:00", Estado = "Finalizado", CosteraId = "2" },
                new() { Id = "H-006", Buque = "ARA Gral. San Martín", Omi = "IMO9000006", Matricula = "ARG-0006", Origen = "Puerto Buenos Aires", Destino = "Puerto Montevideo",  FechaPartida = "01/03/2026 10:00", Eta = "01/03/2026 22:00", Estado = "Finalizado", CosteraId = "3" },
                new() { Id = "H-007", Buque = "LITORAL I",            Omi = "IMO9000007", Matricula = "ARG-0007", Origen = "Puerto Goya",        Destino = "Puerto Rosario",      FechaPartida = "10/03/2026 07:00", Eta = "11/03/2026 09:00", Estado = "Cancelado",  CosteraId = "1" },
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