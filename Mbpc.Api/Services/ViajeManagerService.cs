using Dapper;
using Oracle.ManagedDataAccess.Client;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Mbpc.Api.Models.Config;
using Mbpc.Api.Models.Mongo;
using Mbpc.Api.DTOs;
using System.Data;

namespace Mbpc.Api.Services
{
    public class ViajeManagerService : IViajeService
    {
        private readonly IMongoCollection<ViajePosicionMongo> _viajesCollection;
        private readonly string _oracleConnectionString;
        private readonly ILogger<ViajeManagerService> _logger;
        private readonly IWebHostEnvironment _env;

        public ViajeManagerService(
            IMongoClient mongoClient,
            IOptions<MongoDbSettings> mongoSettings,
            IOptions<OracleDbSettings> oracleSettings,
            ILogger<ViajeManagerService> logger,
            IWebHostEnvironment env)
        {
            var database = mongoClient.GetDatabase(mongoSettings.Value.DatabaseName);
            _viajesCollection = database.GetCollection<ViajePosicionMongo>(
                mongoSettings.Value.LastMbpcCollectionName);
            _oracleConnectionString = oracleSettings.Value.ConnectionString;
            _logger = logger;
            _env = env;
        }

        // ── LECTURA (MongoDB) ────────────────────────────────────────────────

        public async Task<List<ViajePosicionMongo>> GetViajesAsync(int pagina = 1, int tamanio = 50)
        {
            var skip = (pagina - 1) * tamanio;
            // Ordenamos por fecha de mensaje descendente para ver los más nuevos arriba
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
        /// Filtra desde MongoDB los registros cuyo NavegationStatusDesc indique estado de amarre/fondeo,
        /// y complementa con datos operativos mockeados hasta integrar Oracle.
        /// </summary>
        public async Task<List<BarcoPuertoDto>> GetBarcosEnPuertoAsync()
        {
            _logger.LogInformation("Consultando barcos en puerto desde MongoDB.");

            // Filtramos estados que implican estar en puerto (amarrado o fondeado)
            var estadosEnPuerto = new[]
            {
                "Amarrado",
                "Moored",
                "At anchor",
                "Fondeado",
                "Navegando con el remolcador"
            };

            var filtro = Builders<ViajePosicionMongo>.Filter.In(
                v => v.NavegationStatusDesc,
                estadosEnPuerto);

            var posicionesMongo = await _viajesCollection
                .Find(filtro)
                .SortByDescending(v => v.MsgTime)
                .Limit(50)
                .ToListAsync();

            // Si en DEV no hay datos reales con esos estados, hacemos un mock representativo
            if (!posicionesMongo.Any() && _env.IsDevelopment())
            {
                _logger.LogWarning("No se encontraron barcos en puerto en MongoDB. Devolviendo datos mockeados (DEV).");
                return GetBarcosEnPuertoMock();
            }

            // Mapeamos el resultado de Mongo al DTO de presentación
            return posicionesMongo.Select((p, i) => new BarcoPuertoDto
            {
                Id      = p.Id,
                Buque   = p.VesselName ?? "DESCONOCIDO",
                Origen  = "No registrado",   // Oracle aportará esto en la integración completa
                Destino = "No registrado",   // Oracle aportará esto en la integración completa
                Eta     = p.MsgTime.AddHours(i + 2).ToString("dd/MM/yyyy HH:mm"), // Mock hasta integración
                Estado  = p.NavegationStatusDesc ?? "N/A",
                Mmsi    = p.Mmsi ?? string.Empty
            }).ToList();
        }

        /// <summary>
        /// Búsqueda histórica de viajes con múltiples criterios.
        /// Consulta Oracle para datos de archivo. En DEV devuelve datos mockeados.
        /// </summary>
        public async Task<List<ViajeHistoricoDto>> GetHistoricoAsync(FiltroHistoricoDto filtro)
        {
            _logger.LogInformation("Iniciando búsqueda histórica en Oracle.");

            try
            {
                using var connection = new OracleConnection(_oracleConnectionString);
                var parameters = new DynamicParameters();

                // Pasamos los filtros al SP (valores nulos son admitidos → el SP los ignora)
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
            }
            catch (OracleException ex)
            {
                if (!_env.IsDevelopment())
                {
                    _logger.LogError(ex, "Error de Oracle en producción al consultar histórico.");
                    throw;
                }

                _logger.LogWarning(
                    "Oracle no disponible. Bypass activado. Devolviendo histórico mockeado. Error: {Message}",
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
                using var connection = new OracleConnection(_oracleConnectionString);
                var parameters = new DynamicParameters();
                
                // Mapeo completo del DTO expandido hacia Oracle
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

                exitoOracle = parameters.Get<int>("p_RESULTADO") == 1;
            }
            catch (OracleException ex)
            {
                if (!_env.IsDevelopment())
                {
                    _logger.LogError(ex, "Error de Oracle en producción al crear viaje para {Buque}.", nuevoViaje.NombreBuque);
                    throw;
                }

                _logger.LogWarning(
                    "Oracle no disponible. Bypass activado. Simulando éxito al crear viaje para: {Buque}. Error: {Message}",
                    nuevoViaje.NombreBuque, ex.Message);
                
                exitoOracle = true; // Bypass de desarrollo
            }

            // --- INICIO LÓGICA CQRS (ACTUALIZACIÓN DUAL) ---
            if (exitoOracle)
            {
                try
                {
                    _logger.LogInformation("Sincronizando estado en MongoDB (CQRS) para el nuevo viaje de {Buque}", nuevoViaje.NombreBuque);

                    // Creamos el documento que MongoDB necesita para mostrarlo en la grilla
                    var nuevoRegistroMongo = new ViajePosicionMongo
                    {
                        // Mongo genera el Id (ObjectId) automáticamente si lo dejamos nulo
                        VesselName = nuevoViaje.NombreBuque,
                        NavegationStatusDesc = "Navegando con el remolcador", // Regla de negocio de PNA
                        MsgTime = DateTime.UtcNow, // Fecha de creación
                        Latitude = 0,  // Por ahora mock, luego parsearemos de nuevoViaje.Posicion
                        Longitude = 0, // Por ahora mock
                        SpeedOverGround = 0
                    };

                    await _viajesCollection.InsertOneAsync(nuevoRegistroMongo);
                    _logger.LogInformation("¡CQRS Exitoso! Viaje insertado en Mongo para visualización inmediata.");
                }
                catch (Exception mongoEx)
                {
                    _logger.LogError(mongoEx, "Fallo al insertar en MongoDB para el nuevo viaje de {Buque}.", nuevoViaje.NombreBuque);
                }
            }
            // --- FIN LÓGICA CQRS ---

            return exitoOracle;
        }

        // ── DATOS MOCKEADOS (solo DEV) ───────────────────────────────────────

        private static List<BarcoPuertoDto> GetBarcosEnPuertoMock()
        {
            return new List<BarcoPuertoDto>
            {
                new() { Id = "mock-001", Buque = "ARA Alte. Brown",     Origen = "Puerto Rosario",        Destino = "Puerto Buenos Aires", Eta = "26/06/2026 08:00", Estado = "Amarrado",  Mmsi = "701000001" },
                new() { Id = "mock-002", Buque = "RÍO PARANÁ",          Origen = "Puerto Corrientes",     Destino = "Puerto Buenos Aires", Eta = "26/06/2026 10:30", Estado = "Amarrado",  Mmsi = "701000002" },
                new() { Id = "mock-003", Buque = "SANTA FE FLUVIAL",    Origen = "Puerto Santa Fe",       Destino = "Puerto La Plata",     Eta = "27/06/2026 06:00", Estado = "Fondeado",  Mmsi = "701000003" },
                new() { Id = "mock-004", Buque = "HIDROVÍA EXPRESS",    Origen = "Puerto Concordia",      Destino = "Puerto Buenos Aires", Eta = "27/06/2026 14:00", Estado = "Amarrado",  Mmsi = "701000004" },
                new() { Id = "mock-005", Buque = "GRAN CHACO",          Origen = "Puerto Barranqueras",   Destino = "Puerto Zárate",       Eta = "28/06/2026 09:00", Estado = "Fondeado",  Mmsi = "701000005" },
            };
        }

        private static List<ViajeHistoricoDto> GetHistoricoMock(FiltroHistoricoDto filtro)
        {
            var todos = new List<ViajeHistoricoDto>
            {
                new() { Id = "H-001", Buque = "ARA Alte. Brown",     Omi = "IMO9000001", Matricula = "ARG-0001", Origen = "Puerto Rosario",      Destino = "Puerto Buenos Aires", FechaPartida = "10/01/2026 07:00", Eta = "10/01/2026 18:00", Estado = "Finalizado" },
                new() { Id = "H-002", Buque = "RÍO PARANÁ",          Omi = "IMO9000002", Matricula = "ARG-0002", Origen = "Puerto Corrientes",    Destino = "Puerto Buenos Aires", FechaPartida = "15/01/2026 06:30", Eta = "16/01/2026 08:00", Estado = "Finalizado" },
                new() { Id = "H-003", Buque = "SANTA FE FLUVIAL",    Omi = "IMO9000003", Matricula = "ARG-0003", Origen = "Puerto Santa Fe",      Destino = "Puerto La Plata",     FechaPartida = "20/01/2026 08:00", Eta = "20/01/2026 20:00", Estado = "Finalizado" },
                new() { Id = "H-004", Buque = "HIDROVÍA EXPRESS",    Omi = "IMO9000004", Matricula = "ARG-0004", Origen = "Puerto Concordia",     Destino = "Puerto Buenos Aires", FechaPartida = "02/02/2026 05:00", Eta = "03/02/2026 06:00", Estado = "Finalizado" },
                new() { Id = "H-005", Buque = "GRAN CHACO",          Omi = "IMO9000005", Matricula = "ARG-0005", Origen = "Puerto Barranqueras",  Destino = "Puerto Zárate",       FechaPartida = "14/02/2026 09:00", Eta = "16/02/2026 07:00", Estado = "Finalizado" },
                new() { Id = "H-006", Buque = "ARA Gral. San Martín",Omi = "IMO9000006", Matricula = "ARG-0006", Origen = "Puerto Buenos Aires",  Destino = "Puerto Montevideo",   FechaPartida = "01/03/2026 10:00", Eta = "01/03/2026 22:00", Estado = "Finalizado" },
                new() { Id = "H-007", Buque = "LITORAL I",           Omi = "IMO9000007", Matricula = "ARG-0007", Origen = "Puerto Goya",          Destino = "Puerto Rosario",      FechaPartida = "10/03/2026 07:00", Eta = "11/03/2026 09:00", Estado = "Cancelado"  },
            };

            // Aplicamos filtros simples en memoria (en producción lo hace el SP de Oracle)
            return todos.Where(v =>
                (string.IsNullOrWhiteSpace(filtro.Nombre)    || v.Buque.Contains(filtro.Nombre,    StringComparison.OrdinalIgnoreCase)) &&
                (string.IsNullOrWhiteSpace(filtro.Omi)       || v.Omi.Contains(filtro.Omi,         StringComparison.OrdinalIgnoreCase)) &&
                (string.IsNullOrWhiteSpace(filtro.Matricula) || v.Matricula.Contains(filtro.Matricula, StringComparison.OrdinalIgnoreCase)) &&
                (string.IsNullOrWhiteSpace(filtro.Origen)    || v.Origen.Contains(filtro.Origen,   StringComparison.OrdinalIgnoreCase)) &&
                (string.IsNullOrWhiteSpace(filtro.Destino)   || v.Destino.Contains(filtro.Destino, StringComparison.OrdinalIgnoreCase))
            ).ToList();
        }
    }
}
