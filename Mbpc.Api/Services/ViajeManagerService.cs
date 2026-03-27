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

        // Clave y TTL del caché para GetBarcosEnPuerto
        private const string CacheKeyBarcosEnPuerto = "barcos:en_puerto";
        private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(2);

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
            _oracleConnectionString = oracleSettings.Value.ConnectionString;
            _logger = logger;
            _env = env;
            _cache = cache;
        }

        // ── LECTURA (MongoDB) ────────────────────────────────────────────────

        public async Task<List<ViajePosicionMongo>> GetViajesAsync(int pagina = 1, int tamanio = 50)
        {
            var skip = (pagina - 1) * tamanio;
            return await _viajesCollection.Find(_ => true)
                                          .SortByDescending(v => v.MsgTime)
                                          .Skip(skip)
                                          .Limit(tamanio)
                                          .ToListAsync();
        }

        public async Task<ViajePosicionMongo?> GetViajeByMmsiAsync(string mmsi)
        {
            return await _viajesCollection.Find(v => v.Mmsi == mmsi).FirstOrDefaultAsync();
        }

        /// <summary>
        /// Retorna los barcos actualmente en puerto.
        /// Consulta MongoDB filtrando por NavegationStatusDesc "Amarrado" o "Fondeado"
        /// (case-insensitive via regex). El resultado se cachea en Redis por 2 minutos
        /// para reducir la presión sobre Mongo en horas pico.
        /// </summary>
        public async Task<List<BarcoPuertoDto>> GetBarcosEnPuertoAsync()
        {
            _logger.LogInformation("Consultando barcos en puerto.");

            // 1. Intentar leer desde Redis
            try
            {
                var cachedResult = await _redisRetryPolicy.ExecuteAsync(async () =>
                    await _cache.GetStringAsync(CacheKeyBarcosEnPuerto));

                if (cachedResult is not null)
                {
                    _logger.LogInformation("Cache HIT: devolviendo barcos en puerto desde Redis.");
                    return JsonSerializer.Deserialize<List<BarcoPuertoDto>>(cachedResult)
                           ?? new List<BarcoPuertoDto>();
                }
            }
            catch (Exception redisEx)
            {
                // Redis no disponible: no es fatal, continuamos hacia Mongo
                _logger.LogWarning(redisEx, "Redis no disponible al leer caché de barcos en puerto. Consultando MongoDB directamente.");
            }

            // 2. Consultar MongoDB
            try
            {
                _logger.LogInformation("Cache MISS: consultando MongoDB para barcos en puerto.");

                // Regex case-insensitive para "Amarrado" o "Fondeado"
                var regexAmarrado = new MongoDB.Bson.BsonRegularExpression("amarrado", "i");
                var regexFondeado = new MongoDB.Bson.BsonRegularExpression("fondeado", "i");

                var filtro = Builders<ViajePosicionMongo>.Filter.Or(
                    Builders<ViajePosicionMongo>.Filter.Regex(v => v.NavegationStatusDesc, regexAmarrado),
                    Builders<ViajePosicionMongo>.Filter.Regex(v => v.NavegationStatusDesc, regexFondeado));

                var posicionesMongo = await _viajesCollection
                    .Find(filtro)
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
                    var serialized = JsonSerializer.Serialize(resultado);
                    var cacheOptions = new DistributedCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = CacheTtl
                    };

                    await _redisRetryPolicy.ExecuteAsync(async () =>
                        await _cache.SetStringAsync(CacheKeyBarcosEnPuerto, serialized, cacheOptions));

                    _logger.LogInformation("Barcos en puerto almacenados en Redis (TTL: {Ttl}).", CacheTtl);
                }
                catch (Exception redisWriteEx)
                {
                    _logger.LogWarning(redisWriteEx, "No se pudo escribir en Redis. Se continuará sin caché.");
                }

                return resultado;
            }
            catch (Exception mongoEx)
            {
                _logger.LogError(mongoEx, "Error al consultar MongoDB para barcos en puerto.");
                return new List<BarcoPuertoDto>();
            }
        }

        public async Task<List<ViajeHistoricoDto>> GetHistoricoAsync(FiltroHistoricoDto filtro)
        {
            try
            {
                return await _oracleRetryPolicy.ExecuteAsync(async () =>
                {
                    using var connection = new OracleConnection(_oracleConnectionString);
                    var parameters = new DynamicParameters();

                    parameters.Add("p_NOMBRE",    filtro.Nombre    ?? (object)DBNull.Value);
                    parameters.Add("p_OMI",       filtro.Omi       ?? (object)DBNull.Value);
                    parameters.Add("p_MATRICULA", filtro.Matricula ?? (object)DBNull.Value);
                    parameters.Add("p_ORIGEN",    filtro.Origen    ?? (object)DBNull.Value);
                    parameters.Add("p_DESTINO",   filtro.Destino   ?? (object)DBNull.Value);
                    parameters.Add("p_DESDE",     filtro.Desde     ?? (object)DBNull.Value);
                    parameters.Add("p_HASTA",     filtro.Hasta     ?? (object)DBNull.Value);

                    var resultado = await connection.QueryAsync<ViajeHistoricoDto>(
                        "PKG_MBPC_VIAJES.SP_HISTORICO",
                        parameters,
                        commandType: CommandType.StoredProcedure);

                    return resultado.ToList();
                });
            }
            catch (OracleException ex)
            {
                if (!_env.IsDevelopment())
                {
                    _logger.LogError(ex, "Error de Oracle en producción al consultar histórico.");
                    throw;
                }

                _logger.LogWarning(
                    "Oracle no disponible tras reintentos. Bypass DEV activado. Devolviendo histórico mockeado. Error: {Message}",
                    ex.Message);

                return GetHistoricoMock(filtro);
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

                    parameters.Add("p_BUQUE",           nuevoViaje.NombreBuque);
                    parameters.Add("p_ORIGEN",          nuevoViaje.Origen);
                    parameters.Add("p_DESTINO",         nuevoViaje.Destino);
                    parameters.Add("p_MUELLE_SALIDA",   nuevoViaje.MuelleSalida);
                    parameters.Add("p_PTO_CONTROL",     nuevoViaje.ProximoPuntoControl);
                    parameters.Add("p_FECHA_PARTIDA",   nuevoViaje.FechaPartida);
                    parameters.Add("p_ETA",             nuevoViaje.ETA);
                    parameters.Add("p_ZOE",             nuevoViaje.ZOE);
                    parameters.Add("p_POSICION",        nuevoViaje.Posicion);
                    parameters.Add("p_KM_PAR",          nuevoViaje.RioCanalKmPar);
                    parameters.Add("p_MALVINAS_COD",    nuevoViaje.DeclaracionMalvinas.ToString());
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
                        // TAREA 1 — FIX: Los buques nacen "Amarrado", no navegando con remolcador
                        NavegationStatusDesc = "Amarrado",
                        MsgTime              = DateTime.UtcNow,
                        Latitude             = 0,
                        Longitude            = 0,
                        SpeedOverGround      = 0,
                        Origin               = nuevoViaje.Origen,
                        Destination          = nuevoViaje.Destino
                    };

                    await _viajesCollection.InsertOneAsync(nuevoRegistroMongo);
                    _logger.LogInformation("¡CQRS Exitoso! Viaje insertado en Mongo con estado inicial 'Amarrado'.");

                    // Invalidar caché de barcos en puerto para reflejar el nuevo registro
                    try
                    {
                        await _redisRetryPolicy.ExecuteAsync(async () =>
                            await _cache.RemoveAsync(CacheKeyBarcosEnPuerto));

                        _logger.LogInformation("Caché de barcos en puerto invalidada tras nuevo viaje.");
                    }
                    catch (Exception redisEx)
                    {
                        _logger.LogWarning(redisEx, "No se pudo invalidar la caché de Redis tras crear viaje. Se auto-expirará en {Ttl}.", CacheTtl);
                    }
                }
                catch (Exception mongoEx)
                {
                    _logger.LogError(mongoEx, "Fallo al insertar en MongoDB para el nuevo viaje de {Buque}.", nuevoViaje.NombreBuque);
                }
            }
            // --- FIN LÓGICA CQRS ---

            return exitoOracle;
        }

        // ── TAREA 1: CAMBIO DE ESTADO DEL BUQUE (MongoDB Update.Set) ────────

        /// <summary>
        /// Cambia el estado de navegación del buque a "Navegando" (Zarpar).
        /// CQRS: simula bypass a Oracle y actualiza NavegationStatusDesc en MongoDB directamente.
        /// Invalida el caché de barcos en puerto para forzar recarga.
        /// </summary>
        public async Task<bool> ZarparAsync(string id)
        {
            _logger.LogInformation("Ejecutando ZARPAR para viaje {Id}. Bypass Oracle (DEV/simulado). Actualizando Mongo.", id);
            return await CambiarEstadoNavegacionAsync(id, "Navegando");
        }

        /// <summary>
        /// Cambia el estado de navegación del buque a "Amarrado".
        /// CQRS: simula bypass a Oracle y actualiza NavegationStatusDesc en MongoDB directamente.
        /// Invalida el caché de barcos en puerto para forzar recarga.
        /// </summary>
        public async Task<bool> AmarrarViajeAsync(string id)
        {
            _logger.LogInformation("Ejecutando AMARRAR para viaje {Id}. Bypass Oracle (DEV/simulado). Actualizando Mongo.", id);
            return await CambiarEstadoNavegacionAsync(id, "Amarrado");
        }

        /// <summary>
        /// Cambia el estado de navegación del buque a "Fondeado".
        /// CQRS: simula bypass a Oracle y actualiza NavegationStatusDesc en MongoDB directamente.
        /// Invalida el caché de barcos en puerto para forzar recarga.
        /// </summary>
        public async Task<bool> FondearViajeAsync(string id)
        {
            _logger.LogInformation("Ejecutando FONDEAR para viaje {Id}. Bypass Oracle (DEV/simulado). Actualizando Mongo.", id);
            return await CambiarEstadoNavegacionAsync(id, "Fondeado");
        }

        /// <summary>
        /// Método privado unificado que ejecuta el Update.Set sobre NavegationStatusDesc en MongoDB.
        /// El filtro acepta tanto ObjectId como VesselName para máxima compatibilidad.
        /// </summary>
        private async Task<bool> CambiarEstadoNavegacionAsync(string id, string nuevoEstado)
        {
            try
            {
                FilterDefinition<ViajePosicionMongo> filtro;

                // Intentamos primero por ObjectId; si no parsea, buscamos por VesselName
                if (id.Length == 24 && MongoDB.Bson.ObjectId.TryParse(id, out var objectId))
                {
                    filtro = Builders<ViajePosicionMongo>.Filter.Eq("_id", objectId);
                }
                else
                {
                    filtro = Builders<ViajePosicionMongo>.Filter.Eq(v => v.VesselName, id);
                }

                var update = Builders<ViajePosicionMongo>.Update
                    .Set(v => v.NavegationStatusDesc, nuevoEstado);

                var result = await _viajesCollection.UpdateOneAsync(filtro, update);

                if (result.MatchedCount == 0)
                {
                    _logger.LogWarning("No se encontró documento en last_mbpc con id/nombre '{Id}' para cambiar estado.", id);
                    return false;
                }

                _logger.LogInformation("¡CQRS Exitoso! NavegationStatusDesc actualizado a '{Estado}' para '{Id}'.", nuevoEstado, id);

                // Invalidar caché de barcos en puerto (el estado "Amarrado"/"Fondeado"/"Navegando" afecta la query)
                try
                {
                    await _redisRetryPolicy.ExecuteAsync(async () =>
                        await _cache.RemoveAsync(CacheKeyBarcosEnPuerto));

                    _logger.LogInformation("Caché de barcos en puerto invalidada tras cambio de estado a '{Estado}'.", nuevoEstado);
                }
                catch (Exception redisEx)
                {
                    _logger.LogWarning(redisEx, "No se pudo invalidar Redis tras cambio de estado. Se auto-expirará en {Ttl}.", CacheTtl);
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cambiar NavegationStatusDesc a '{Estado}' para '{Id}'.", nuevoEstado, id);
                return false;
            }
        }

        // ── DATOS MOCKEADOS (solo DEV — fallback Oracle) ─────────────────────

        private static List<ViajeHistoricoDto> GetHistoricoMock(FiltroHistoricoDto filtro)
        {
            var todos = new List<ViajeHistoricoDto>
            {
                new() { Id = "H-001", Buque = "ARA Alte. Brown",      Omi = "IMO9000001", Matricula = "ARG-0001", Origen = "Puerto Rosario",      Destino = "Puerto Buenos Aires", FechaPartida = "10/01/2026 07:00", Eta = "10/01/2026 18:00", Estado = "Finalizado" },
                new() { Id = "H-002", Buque = "RÍO PARANÁ",           Omi = "IMO9000002", Matricula = "ARG-0002", Origen = "Puerto Corrientes",    Destino = "Puerto Buenos Aires", FechaPartida = "15/01/2026 06:30", Eta = "16/01/2026 08:00", Estado = "Finalizado" },
                new() { Id = "H-003", Buque = "SANTA FE FLUVIAL",     Omi = "IMO9000003", Matricula = "ARG-0003", Origen = "Puerto Santa Fe",      Destino = "Puerto La Plata",     FechaPartida = "20/01/2026 08:00", Eta = "20/01/2026 20:00", Estado = "Finalizado" },
                new() { Id = "H-004", Buque = "HIDROVÍA EXPRESS",     Omi = "IMO9000004", Matricula = "ARG-0004", Origen = "Puerto Concordia",     Destino = "Puerto Buenos Aires", FechaPartida = "02/02/2026 07:00", Eta = "03/02/2026 06:00", Estado = "Finalizado" },
                new() { Id = "H-005", Buque = "GRAN CHACO",           Omi = "IMO9000005", Matricula = "ARG-0005", Origen = "Puerto Barranqueras",  Destino = "Puerto Zárate",       FechaPartida = "14/02/2026 09:00", Eta = "16/02/2026 07:00", Estado = "Finalizado" },
                new() { Id = "H-006", Buque = "ARA Gral. San Martín", Omi = "IMO9000006", Matricula = "ARG-0006", Origen = "Puerto Buenos Aires",  Destino = "Puerto Montevideo",   FechaPartida = "01/03/2026 10:00", Eta = "01/03/2026 22:00", Estado = "Finalizado" },
                new() { Id = "H-007", Buque = "LITORAL I",            Omi = "IMO9000007", Matricula = "ARG-0007", Origen = "Puerto Goya",          Destino = "Puerto Rosario",      FechaPartida = "10/03/2026 07:00", Eta = "11/03/2026 09:00", Estado = "Cancelado"  },
            };

            return todos.Where(v =>
                (string.IsNullOrWhiteSpace(filtro.Nombre)    || v.Buque.Contains(filtro.Nombre,       StringComparison.OrdinalIgnoreCase)) &&
                (string.IsNullOrWhiteSpace(filtro.Omi)       || v.Omi.Contains(filtro.Omi,            StringComparison.OrdinalIgnoreCase)) &&
                (string.IsNullOrWhiteSpace(filtro.Matricula) || v.Matricula.Contains(filtro.Matricula, StringComparison.OrdinalIgnoreCase)) &&
                (string.IsNullOrWhiteSpace(filtro.Origen)    || v.Origen.Contains(filtro.Origen,      StringComparison.OrdinalIgnoreCase)) &&
                (string.IsNullOrWhiteSpace(filtro.Destino)   || v.Destino.Contains(filtro.Destino,    StringComparison.OrdinalIgnoreCase))
            ).ToList();
        }
    }
}
