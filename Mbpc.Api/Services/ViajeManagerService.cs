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
using Mbpc.Api.Services.Auth; 
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
        private readonly IHostEnvironment                      _env;
        private readonly IDistributedCache                     _cache;
        private readonly ICosteraUserContext                   _costeraUserContext;
        private readonly ICargaService                         _cargaService;
        private readonly IBuqueService                         _buqueService;

        private static readonly AsyncRetryPolicy _oracleRetryPolicy = Policy
            .Handle<OracleException>()
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)),
                onRetry: (exception, timeSpan, retryCount, context) =>
                {
                });

        private static readonly AsyncRetryPolicy _redisRetryPolicy = Policy
            .Handle<Exception>(ex => ex is not OperationCanceledException)
            .WaitAndRetryAsync(
                retryCount: 2,
                sleepDurationProvider: _ => TimeSpan.FromMilliseconds(500));

        private static string CacheKeyBarcosEnPuerto(int costeraId) => $"barcos:en_puerto:{costeraId}";
        private static string CacheKeyMapaViajes(int costeraId)     => $"viajes:mapa:{costeraId}";
        private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(2);

        private static readonly IReadOnlyDictionary<EstadoEtapa, HashSet<EstadoEtapa>>
            _transicionesPermitidas = new Dictionary<EstadoEtapa, HashSet<EstadoEtapa>>
            {
                [EstadoEtapa.Amarrado]  = new HashSet<EstadoEtapa> { EstadoEtapa.Navegando },
                [EstadoEtapa.Navegando] = new HashSet<EstadoEtapa> { EstadoEtapa.Amarrado, EstadoEtapa.Fondeado },
                [EstadoEtapa.Fondeado]  = new HashSet<EstadoEtapa> { EstadoEtapa.Reanudado },
                [EstadoEtapa.Reanudado] = new HashSet<EstadoEtapa> { EstadoEtapa.Navegando, EstadoEtapa.Amarrado, EstadoEtapa.Fondeado },
            };

        private const double RADIO_TIERRA_KM              = 6371.0;
        private const double KM_POR_MILLA_NAUTICA         = 1.852;
        private const double MAX_VELOCIDAD_KNOTS          = 60.0;
        private const double MIN_SEGUNDOS_ENTRE_REPORTES  = 1.0;

        public ViajeManagerService(
            IMongoClient                  mongoClient,
            IOptions<MongoDbSettings>     mongoSettings,
            IOptions<OracleDbSettings>    oracleSettings,
            ILogger<ViajeManagerService>  logger,
            IHostEnvironment              env,
            IDistributedCache             cache,
            ICosteraUserContext           costeraUserContext,
            ICargaService                 cargaService,
            IBuqueService                 buqueService) 
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
            _cargaService           = cargaService;
            _buqueService           = buqueService;
        }

        private static FilterDefinition<ViajePosicionMongo> BuildFiltroCostera(int costeraId)
        {
            if (costeraId == 0)
                return Builders<ViajePosicionMongo>.Filter.Empty;

            return Builders<ViajePosicionMongo>.Filter.Eq("CosteraId", costeraId);
        }

        private static FilterDefinition<ViajeDetalleMongo> BuildFiltroCosteraDetalle(int costeraId)
        {
            if (costeraId == 0)
                return Builders<ViajeDetalleMongo>.Filter.Empty;

            return Builders<ViajeDetalleMongo>.Filter.Eq("CosteraId", costeraId);
        }

        public async Task<List<ViajePosicionMongo>> GetViajesAsync(string? nombre = null, int pagina = 1, int tamanio = 50)
        {
            var costeraId = _costeraUserContext.GetCurrentCosteraId();
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

        public async Task<List<ViajeDto>> ObtenerViajesDtoAsync(string? nombre, int pagina, int tamanio)
        {
            var posicionesMongo = await GetViajesAsync(nombre, pagina, tamanio);
            var viajesDto = new List<ViajeDto>();

            foreach (var p in posicionesMongo)
            {
                var buqueNombre = p.VesselName ?? "DESCONOCIDO";

                // Si VesselName es un ID numérico, consultar el catálogo de buques para obtener el nombre real.
                if (long.TryParse(buqueNombre, out long buqueId))
                {
                    _logger.LogDebug(
                        "ObtenerViajesDtoAsync: VesselName '{Raw}' es ID numérico. Hidratando desde catálogo de buques.",
                        buqueNombre);

                    var infoBuque = await _buqueService.ObtenerBuquePorIdAsync(buqueId);

                    buqueNombre = !string.IsNullOrWhiteSpace(infoBuque?.Nombre)
                        ? infoBuque.Nombre
                        : $"BUQUE {buqueNombre}";
                }

                viajesDto.Add(new ViajeDto
                {
                    Id                    = p.Id,
                    Buque                 = buqueNombre,
                    NombreBuque           = p.VesselName ?? p.TravelId.ToString(),
                    Ruta                  = $"{p.Origin ?? "Sin Origen"} ➔ {p.Destination ?? "Sin Destino"} | Pos: {Math.Round(p.Latitude, 4)}, {Math.Round(p.Longitude, 4)}",
                    FechaInicioFormateada = p.MsgTime.ToString("dd/MM/yyyy HH:mm"),
                    EstadoActual          = p.NavegationStatusDesc ?? "N/A"
                });
            }

            return viajesDto;
        }

        public async Task<ViajePosicionMongo?> GetViajeByMmsiAsync(string mmsi)
        {
            var costeraId     = _costeraUserContext.GetCurrentCosteraId();
            var filtroMmsi    = Builders<ViajePosicionMongo>.Filter.Eq(v => v.Mmsi, mmsi);
            var filtroCostera = BuildFiltroCostera(costeraId);
            var filtroFinal   = Builders<ViajePosicionMongo>.Filter.And(filtroMmsi, filtroCostera);

            return await _viajesCollection.Find(filtroFinal).FirstOrDefaultAsync();
        }

        public async Task<(ViajeDetalleMongo? Detalle, long TravelId)> GetViajeDetalleByIdAsync(
            string id,
            CancellationToken ct = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(id);

            var filtroPosicion = BuildFiltroViaje(id);
            var viajePosicion  = await _viajesCollection.Find(filtroPosicion).FirstOrDefaultAsync(ct);

            if (viajePosicion is null)
                return (null, 0);

            var travelId = viajePosicion.TravelId;

            var filtroDetalleBase = travelId > 0
                ? Builders<ViajeDetalleMongo>.Filter.Eq(v => v.IdViaje, travelId)
                : Builders<ViajeDetalleMongo>.Filter.Eq(v => v.VesselName, viajePosicion.VesselName);

            int costeraId     = _costeraUserContext.GetCurrentCosteraId();
            var filtroCostera = BuildFiltroCosteraDetalle(costeraId);
            var filtroFinal   = Builders<ViajeDetalleMongo>.Filter.And(filtroDetalleBase, filtroCostera);

            if (travelId <= 0 && string.IsNullOrWhiteSpace(viajePosicion.VesselName))
            {
                _logger.LogWarning(
                    "GetViajeDetalleByIdAsync: La posición '{Id}' no tiene TravelId ni VesselName. " +
                    "No es posible cruzar con la colección de detalles.", id);
                return (null, 0);
            }

            var detalle = await _detallesCollection.Find(filtroFinal).FirstOrDefaultAsync(ct);
            return (detalle, travelId);
        }

        public async Task<List<BarcoPuertoDto>> GetBarcosEnPuertoAsync()
        {
            var costeraId = _costeraUserContext.GetCurrentCosteraId();

            _logger.LogInformation(
                "Consultando barcos en puerto — CosteraId: {CosteraId} ({Rol}).",
                costeraId, costeraId == 0 ? "Admin" : "Operador");

            var cacheKey = CacheKeyBarcosEnPuerto(costeraId);

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

        public async Task<List<MapaViajeDto>> GetMapaViajesAsync(string? mmsi = null, string? nombreBuque = null)
        {
            var costeraId = _costeraUserContext.GetCurrentCosteraId();
            List<MapaViajeDto> listaCompleta;

            var cacheKey = CacheKeyMapaViajes(costeraId);

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

        public async Task<List<ViajeHistoricoDto>> GetHistoricoAsync(FiltroHistoricoDto filtro)
        {
            var costeraId = _costeraUserContext.GetCurrentCosteraId();

            try
            {
                return await _oracleRetryPolicy.ExecuteAsync(async () =>
                {
                    using var connection = new OracleConnection(_oracleConnectionString);

                    var sql = new System.Text.StringBuilder(
                        @"SELECT TO_CHAR(T.ID)                                  AS Id,
                                COALESCE(B.NOMBRE, 
                                        CASE WHEN INSTR(T.BUQUE_INFO, ',') > 0 
                                            THEN TRIM(REGEXP_SUBSTR(T.BUQUE_INFO, '(.*?)(,|$)', 1, 5, NULL, 1))
                                            ELSE T.BUQUE_INFO 
                                        END, 
                                        'DESCONOCIDO')                        AS Buque,
                                COALESCE(TO_CHAR(B.NRO_OMI), 
                                        CASE WHEN INSTR(T.BUQUE_INFO, ',') > 0 AND REGEXP_SUBSTR(T.BUQUE_INFO, '(.*?)(,|$)', 1, 4, NULL, 1) != '0'
                                            THEN TRIM(REGEXP_SUBSTR(T.BUQUE_INFO, '(.*?)(,|$)', 1, 4, NULL, 1))
                                            ELSE NULL 
                                        END, 
                                        'S/D')                                AS Omi,
                                COALESCE(TO_CHAR(B.MATRICULA), 
                                        CASE WHEN INSTR(T.BUQUE_INFO, ',') > 0 AND REGEXP_SUBSTR(T.BUQUE_INFO, '(.*?)(,|$)', 1, 2, NULL, 1) != '0'
                                            THEN TRIM(REGEXP_SUBSTR(T.BUQUE_INFO, '(.*?)(,|$)', 1, 2, NULL, 1))
                                            ELSE NULL 
                                        END, 
                                        'S/D')                                AS Matricula,
                                T.ORIGEN_ID                                    AS Origen,
                                T.DESTINO                                      AS Destino,
                                TO_CHAR(T.FECHA_SALIDA, 'DD/MM/YYYY HH24:MI')  AS FechaPartida,
                                TO_CHAR(T.ETA,          'DD/MM/YYYY HH24:MI')  AS Eta,
                                CASE T.ESTADO 
                                    WHEN 1 THEN 'Planificado'
                                    WHEN 2 THEN 'Navegando' 
                                    WHEN 3 THEN 'Finalizado' 
                                    WHEN 4 THEN 'Cancelado' 
                                    ELSE 'Otro' 
                                END                                            AS Estado,
                                '0'                                            AS CosteraId
                        FROM   MBPC.TBL_VIAJE T
                        LEFT JOIN MBPC.Z_TBL_BUQUES_UNICO B ON T.BUQUE_ID = B.ID_BUQUE
                        WHERE  T.ESTADO IN (2, 3, 4)");

                    var parameters = new DynamicParameters();

                    // Filtros Dinámicos
                    if (!string.IsNullOrWhiteSpace(filtro.Nombre))
                    {
                        sql.Append(" AND UPPER(B.NOMBRE) LIKE UPPER(:nombre)");
                        parameters.Add("nombre", $"%{filtro.Nombre}%", DbType.String);
                    }

                    if (!string.IsNullOrWhiteSpace(filtro.Omi))
                    {
                        sql.Append(" AND B.NRO_OMI LIKE :omi");
                        parameters.Add("omi", $"%{filtro.Omi}%", DbType.String);
                    }

                    if (!string.IsNullOrWhiteSpace(filtro.Matricula))
                    {
                        sql.Append(" AND B.MATRICULA LIKE :matricula");
                        parameters.Add("matricula", $"%{filtro.Matricula}%", DbType.String);
                    }

                    if (!string.IsNullOrWhiteSpace(filtro.Origen))
                    {
                        sql.Append(" AND UPPER(T.ORIGEN_ID) LIKE UPPER(:origen)");
                        parameters.Add("origen", $"%{filtro.Origen}%", DbType.String);
                    }

                    if (!string.IsNullOrWhiteSpace(filtro.Destino))
                    {
                        sql.Append(" AND UPPER(T.DESTINO) LIKE UPPER(:destino)");
                        parameters.Add("destino", $"%{filtro.Destino}%", DbType.String);
                    }

                    if (filtro.Desde.HasValue)
                    {
                        sql.Append(" AND T.FECHA_SALIDA >= :desde");
                        parameters.Add("desde", filtro.Desde.Value.Date, DbType.Date);
                    }

                    if (filtro.Hasta.HasValue)
                    {
                        sql.Append(" AND T.FECHA_SALIDA < :hasta");
                        parameters.Add("hasta", filtro.Hasta.Value.Date.AddDays(1), DbType.Date);
                    }

                    sql.Append(" ORDER BY T.FECHA_SALIDA DESC");

                    var resultado = await connection.QueryAsync<ViajeHistoricoDto>(
                        sql.ToString(),
                        parameters);

                    return resultado.ToList();
                });
            }
            catch (OracleException ex)
            {
                _logger.LogError(ex, "Error de Oracle en GetHistoricoAsync. SQL State: {ErrorCode}", ex.Number);
                return Enumerable.Empty<ViajeHistoricoDto>().ToList();
            }
        }

        public async Task<bool> IniciarViajeAsync(NuevoViajeDto nuevoViaje)
        {
            if (nuevoViaje.ETA <= nuevoViaje.FechaPartida)
            {
                throw new InvalidOperationException(
                    $"Integridad temporal inválida: la ETA ({nuevoViaje.ETA:dd/MM/yyyy HH:mm}) debe ser posterior a la " +
                    $"FechaPartida ({nuevoViaje.FechaPartida:dd/MM/yyyy HH:mm}).");
            }

            var destino = nuevoViaje.Destino ?? string.Empty;
            bool esMalvinas =
                destino.Contains("MALVINAS", StringComparison.OrdinalIgnoreCase) ||
                destino.Contains("ISLAS DEL ATLANTICO SUR", StringComparison.OrdinalIgnoreCase);

            if (esMalvinas &&
                (nuevoViaje.DeclaracionMalvinas == DeclaracionMalvinasEnum.NoVaAMalvinas_NoPresentoDeclaracion_N ||
                 nuevoViaje.DeclaracionMalvinas == DeclaracionMalvinasEnum.NoVaAMalvinas_PresentoDeclaracion_J))
            {
                throw new InvalidOperationException(
                    "Destino con referencia a Malvinas/Islas del Atlántico Sur: se requiere una declaración de Malvinas válida. " +
                    $"Valor recibido: '{nuevoViaje.DeclaracionMalvinas}'.");
            }

            _logger.LogInformation(
                "IniciarViajeAsync — Inicio para BuqueId: '{BuqueId}' | Origen: '{Origen}' | Destino: '{Destino}' | CosteraId: '{CosteraId}' | Lat: {Lat} | Lng: {Lng}",
                nuevoViaje.BuqueId, nuevoViaje.Origen, nuevoViaje.Destino, nuevoViaje.CosteraId, nuevoViaje.Latitud, nuevoViaje.Longitud);

            if (!int.TryParse(nuevoViaje.CosteraId, out var costeraIdInt))
            {
                _logger.LogError(
                    "IniciarViajeAsync abortado: CosteraId '{CosteraId}' no es un entero válido.",
                    nuevoViaje.CosteraId);
                return false;
            }

            long travelIdGenerado = 0;
            bool exitoOracle      = false;
            
            string posicionLegacyString = (nuevoViaje.Latitud.HasValue && nuevoViaje.Longitud.HasValue) 
                ? $"{nuevoViaje.Latitud.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)}, {nuevoViaje.Longitud.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)}"
                : string.Empty;

            try
            {
                (exitoOracle, travelIdGenerado) = await _oracleRetryPolicy.ExecuteAsync(async () =>
                {
                    using var connection = new OracleConnection(_oracleConnectionString);
                    var parameters       = new DynamicParameters();

                    parameters.Add("p_BUQUE",         nuevoViaje.BuqueId,                dbType: DbType.Int64);
                    parameters.Add("p_ORIGEN",        nuevoViaje.Origen,                 dbType: DbType.String);
                    parameters.Add("p_DESTINO",       nuevoViaje.Destino,                dbType: DbType.String);
                    parameters.Add("p_MUELLE_SALIDA", nuevoViaje.MuelleSalida,           dbType: DbType.String);
                    parameters.Add("p_PTO_CONTROL",   nuevoViaje.ProximoPuntoControl,    dbType: DbType.String);
                    parameters.Add("p_FECHA_PARTIDA", nuevoViaje.FechaPartida,           dbType: DbType.DateTime);
                    parameters.Add("p_ETA",           nuevoViaje.ETA,                    dbType: DbType.DateTime);
                    parameters.Add("p_ZOE",           nuevoViaje.ZOE,                    dbType: DbType.String);
                    parameters.Add("p_POSICION",      posicionLegacyString,              dbType: DbType.String);
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

                _logger.LogWarning(
                    "FASE 1 BYPASS DEV — Oracle no disponible tras reintentos. " +
                    "Simulando éxito. BuqueId: '{BuqueId}'. Error Oracle: {Message}",
                    nuevoViaje.BuqueId, ex.Message);

                exitoOracle      = true;
                travelIdGenerado = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            }

            string vesselNameParaMongo = !string.IsNullOrWhiteSpace(nuevoViaje.NombreBuque)
                ? nuevoViaje.NombreBuque.Trim().ToUpperInvariant()
                : nuevoViaje.BuqueId.ToString();

            if (string.IsNullOrWhiteSpace(nuevoViaje.NombreBuque))
            {
                _logger.LogWarning(
                    "IniciarViajeAsync recibió NombreBuque vacío para BuqueId='{BuqueId}'. " +
                    "Se usará el ID numérico como VesselName temporal.",
                    nuevoViaje.BuqueId);
            }

            try
            {
                var nuevoDocumentoPosicion = new ViajePosicionMongo
                {
                    TravelId             = travelIdGenerado,
                    VesselName           = vesselNameParaMongo,
                    NavegationStatusDesc = EstadoEtapa.Amarrado.ToString(),
                    MsgTime              = DateTime.UtcNow,
                    Latitude             = nuevoViaje.Latitud.HasValue ? (double)nuevoViaje.Latitud.Value : 0, 
                    Longitude            = nuevoViaje.Longitud.HasValue ? (double)nuevoViaje.Longitud.Value : 0, 
                    SpeedOverGround      = 0,
                    CourseOverGround     = 0,
                    Origin               = nuevoViaje.Origen,
                    Destination          = nuevoViaje.Destino,
                    CosteraId            = costeraIdInt
                };

                await _viajesCollection.InsertOneAsync(nuevoDocumentoPosicion);

                _logger.LogInformation(
                    "FASE 2 OK — ViajePosicionMongo insertado con Lat: {Lat}, Lng: {Lng}. MongoId: '{MongoId}' | TravelId: {TravelId} | CosteraId: {CosteraId}.",
                    nuevoDocumentoPosicion.Latitude, nuevoDocumentoPosicion.Longitude, nuevoDocumentoPosicion.Id, travelIdGenerado, costeraIdInt);
            }
            catch (Exception mongoEx)
            {
                _logger.LogError(mongoEx,
                    "FASE 2 FALLO — No se pudo insertar ViajePosicionMongo para BuqueId: '{BuqueId}', TravelId: {TravelId}. " +
                    "Oracle fue exitoso. El documento se puede re-sincronizar manualmente o vía job de reconciliación.",
                    nuevoViaje.BuqueId, travelIdGenerado);
            }

            try
            {
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

        public async Task<PosicionActualizadaResultDto?> ActualizarPosicionAsync(
            string id,
            ActualizarPosicionDto dto)
        {
            _logger.LogInformation(
                "ActualizarPosicionAsync — Id: '{Id}' | Lat: {Lat} | Lng: {Lng} | FechaReporte: {Fecha}",
                id, dto.Latitud, dto.Longitud, dto.FechaReporte);

            var filtroId = BuildFiltroViaje(id);

            var posicionActual = await _viajesCollection
                .Find(filtroId)
                .FirstOrDefaultAsync();

            if (posicionActual is null)
            {
                _logger.LogWarning(
                    "ActualizarPosicionAsync: No se encontró posición en last_mbpc para Id '{Id}'.", id);
                return null;
            }

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

            if (velocidadKn > MAX_VELOCIDAD_KNOTS)
            {
                _logger.LogWarning(
                    "ActualizarPosicionAsync: Cinemática inválida para Id '{Id}'. Velocidad: {Vel:F1} kn, " +
                    "Distancia: {Dist:F2} NM, Δt: {Seg:F0} s.",
                    id, velocidadKn, distanciaNM, segundosTranscurridos);

                throw new InvalidOperationException(
                    $"Cinemática inválida: velocidad calculada de {velocidadKn:F1} kn supera el límite de " +
                    $"{MAX_VELOCIDAD_KNOTS} kn. " +
                    $"Distancia: {distanciaNM:F2} NM en {segundosTranscurridos:F0} segundos. " +
                    $"Verifique las coordenadas o el timestamp del transponder. Si el error persiste comuníquese con un administrador del sistema.");
            }

            var nuevaLocation = new LocationMongo
            {
                Geo = new GeoMongo
                {
                    Type        = "Point",
                    Coordinates = new[] { dto.Longitud, dto.Latitud }, 
                }
            };

            var update = Builders<ViajePosicionMongo>.Update
                .Set(p => p.Latitude,        dto.Latitud)
                .Set(p => p.Longitude,       dto.Longitud)
                .Set(p => p.MsgTime,         dto.FechaReporte)
                .Set(p => p.SpeedOverGround, velocidadKn)
                .Set(p => p.Location,        nuevaLocation);

            var updateResult = await _viajesCollection.UpdateOneAsync(filtroId, update);

            if (updateResult.MatchedCount == 0)
            {
                _logger.LogWarning(
                    "ActualizarPosicionAsync: UpdateOne no encontró documento para Id '{Id}'.", id);
                return null;
            }

            if (updateResult.ModifiedCount == 0)
            {
                _logger.LogWarning(
                    "ActualizarPosicionAsync: UpdateOne no modificó ningún documento para Id '{Id}'.", id);
                throw new InvalidOperationException(
                    "Cinemática inválida: la posición no pudo persistirse en la base de datos. " +
                    "Verifique el identificador del viaje y las coordenadas enviadas.");
            }

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

        public async Task<bool> ZarparAsync(string id)
        {
            _logger.LogInformation("Solicitud ZARPAR para viaje '{Id}'.", id);
            return await CambiarEstadoConValidacionAsync(id, EstadoEtapa.Navegando);
        }

        public async Task<bool> AmarrarViajeAsync(string id)
        {
            _logger.LogInformation("Solicitud AMARRAR para viaje '{Id}'.", id);
            return await CambiarEstadoConValidacionAsync(id, EstadoEtapa.Amarrado);
        }

        public async Task<bool> FinalizarViajeAsync(string id)
        {
            _logger.LogInformation("Solicitud FINALIZAR para viaje '{Id}'.", id);

            var (detalle, _) = await GetViajeDetalleByIdAsync(id);

            if (detalle is null)
            {
                throw new InvalidOperationException(
                    $"No se encontró el detalle operativo del viaje '{id}'. No es posible finalizar el viaje.");
            }

            var filtroEstado = BuildFiltroViaje(id);
            var posicionActual = await _viajesCollection.Find(filtroEstado).FirstOrDefaultAsync();
            var estadoRaw = posicionActual?.NavegationStatusDesc ?? string.Empty;

            if (estadoRaw.Equals("Finalizado", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "El viaje ya se encuentra finalizado. No se permiten más modificaciones.");
            }

            bool tieneBarcazasRaiz    = (detalle.Barcazas?.Count ?? 0) > 0;
            bool tieneBarcazasEtapa   = detalle.Etapas?.Any(e => (e.Barcazas?.Count ?? 0) > 0) == true;
            bool tieneBarcazasActivas = tieneBarcazasRaiz || tieneBarcazasEtapa;

            bool inspectoresABordo = detalle.Inspectores?.Any(i => i.FechaDesembarque is null) == true;
            bool practicosABordo   = detalle.Practicos?.Any(p => p.FechaDesembarque is null) == true;

            static bool ArenaPendiente(BarcazaMongo b) =>
                b.Carga?.Equals("ARENA", StringComparison.OrdinalIgnoreCase) == true
                && b.Descargada != true
                && (b.Cantidad ?? 0) > 0;

            bool tieneArenaPendiente =
                detalle.Barcazas?.Any(ArenaPendiente) == true
                || detalle.Etapas?.Any(e => e.Barcazas?.Any(ArenaPendiente) == true) == true;
                
            if (tieneArenaPendiente) 
                throw new InvalidOperationException("Operación denegada: Existen cargas de tipo ARENA pendientes de descarga.");

            if (tieneBarcazasActivas || inspectoresABordo || practicosABordo)
            {
                var motivos = new List<string>();

                if (tieneBarcazasActivas)
                    motivos.Add("tiene barcazas activas asociadas");

                if (inspectoresABordo)
                    motivos.Add("hay inspectores a bordo (FechaDesembarque = null)");

                if (practicosABordo)
                    motivos.Add("hay prácticos a bordo (FechaDesembarque = null)");

                throw new InvalidOperationException(
                    $"No se puede finalizar el viaje '{id}' porque {string.Join(" y ", motivos)}.");
            }

            return await CambiarEstadoNavegacionAsync(id, "Finalizado");
        }

        public async Task<PersonalViajeDto?> ObtenerPersonalAsync(string viajeId)
        {
            var (detalle, _) = await GetViajeDetalleByIdAsync(viajeId);
            if (detalle == null) return null;

            var dto = new PersonalViajeDto();

            if (detalle.Inspectores != null)
            {
                dto.Inspectores = detalle.Inspectores.Select(i => new PersonalItemDto
                {
                    Documento = i.Documento,
                    NombreApellido = i.NombreApellido,
                    FechaEmbarque = i.FechaEmbarque,
                    FechaDesembarque = i.FechaDesembarque
                }).ToList();
            }

            if (detalle.Practicos != null)
            {
                dto.Practicos = detalle.Practicos.Select(p => new PersonalItemDto
                {
                    Documento = p.Documento,
                    NombreApellido = p.NombreApellido,
                    FechaEmbarque = p.FechaEmbarque,
                    FechaDesembarque = p.FechaDesembarque
                }).ToList();
            }

            return dto;
        }

        public async Task<bool> EmbarcarPersonalAsync(string viajeId, EmbarcarPersonalDto dto)
        {
            var filtroOcupado = Builders<ViajeDetalleMongo>.Filter.Or(
                Builders<ViajeDetalleMongo>.Filter.ElemMatch(v => v.Inspectores, i => i.Documento == dto.Dni && i.FechaDesembarque == null),
                Builders<ViajeDetalleMongo>.Filter.ElemMatch(v => v.Practicos, p => p.Documento == dto.Dni && p.FechaDesembarque == null)
            );
            
            var estaOcupado = await _detallesCollection.Find(filtroOcupado).AnyAsync();
            if (estaOcupado)
                throw new InvalidOperationException($"El DNI {dto.Dni} ya se encuentra embarcado en otro viaje activo.");

            var (detalle, _) = await GetViajeDetalleByIdAsync(viajeId);
            if (detalle == null) return false;

            var update = dto.TipoPersonal == "Inspector" 
                ? Builders<ViajeDetalleMongo>.Update.Push(v => v.Inspectores, new InspectorMongo { Documento = dto.Dni, NombreApellido = dto.NombreApellido, FechaEmbarque = dto.FechaEmbarque })
                : Builders<ViajeDetalleMongo>.Update.Push(v => v.Practicos, new PracticoMongo { Documento = dto.Dni, NombreApellido = dto.NombreApellido, FechaEmbarque = dto.FechaEmbarque });

            var result = await _detallesCollection.UpdateOneAsync(v => v.Id == detalle.Id, update);
            return result.ModifiedCount > 0;
        }

        public async Task<bool> DesembarcarPersonalAsync(string viajeId, string dni, DesembarcarPersonalDto dto)
        {
            var (detalle, _) = await GetViajeDetalleByIdAsync(viajeId);
            if (detalle == null) return false;

            UpdateDefinition<ViajeDetalleMongo> update;
            FilterDefinition<ViajeDetalleMongo> filter;

            if (dto.TipoPersonal == "Inspector")
            {
                filter = Builders<ViajeDetalleMongo>.Filter.And(
                    Builders<ViajeDetalleMongo>.Filter.Eq(v => v.Id, detalle.Id),
                    Builders<ViajeDetalleMongo>.Filter.ElemMatch(v => v.Inspectores, i => i.Documento == dni && i.FechaDesembarque == null)
                );
                update = Builders<ViajeDetalleMongo>.Update.Set("inspectores.$.fechaDesembarque", dto.FechaDesembarque);
            }
            else
            {
                filter = Builders<ViajeDetalleMongo>.Filter.And(
                    Builders<ViajeDetalleMongo>.Filter.Eq(v => v.Id, detalle.Id),
                    Builders<ViajeDetalleMongo>.Filter.ElemMatch(v => v.Practicos, p => p.Documento == dni && p.FechaDesembarque == null)
                );
                update = Builders<ViajeDetalleMongo>.Update.Set("practicos.$.fechaDesembarque", dto.FechaDesembarque);
            }

            var result = await _detallesCollection.UpdateOneAsync(filter, update);
            if (result.ModifiedCount == 0)
                throw new InvalidOperationException($"El DNI {dni} no está embarcado activamente en este viaje como {dto.TipoPersonal}.");

            return true;
        }

        public async Task<bool> FondearViajeAsync(string id)
        {
            _logger.LogInformation("Solicitud FONDEAR para viaje '{Id}'.", id);
            return await CambiarEstadoConValidacionAsync(id, EstadoEtapa.Fondeado);
        }

        public async Task<bool> ReanudarViajeAsync(string id)
        {
            _logger.LogInformation("Solicitud REANUDAR para viaje '{Id}'.", id);
            return await CambiarEstadoConValidacionAsync(id, EstadoEtapa.Reanudado);
        }

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

        private static FilterDefinition<ViajePosicionMongo> BuildFiltroViaje(string id)
        {
            if (id.Length == 24 && ObjectId.TryParse(id, out var objectId))
                return Builders<ViajePosicionMongo>.Filter.Eq("_id", objectId);

            return Builders<ViajePosicionMongo>.Filter.Eq(v => v.VesselName, id);
        }

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

                if (nuevoEstado.Equals(EstadoEtapa.Amarrado.ToString(), StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogInformation(
                        "CambiarEstadoNavegacionAsync: Disparando sincronización de amarre para convoy del viaje '{Id}'.", id);
                    await _cargaService.SincronizarAmarreConvoyAsync(id);
                }
                else if (nuevoEstado.Equals(EstadoEtapa.Navegando.ToString(), StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogInformation(
                        "CambiarEstadoNavegacionAsync: Disparando sincronización de zarpe (EnTransito) para convoy del viaje '{Id}'.", id);
                    await _cargaService.SincronizarZarpeConvoyAsync(id);
                }

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
    }
}