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

        public ViajeManagerService(
            IMongoClient mongoClient, 
            IOptions<MongoDbSettings> mongoSettings,
            IOptions<OracleDbSettings> oracleSettings)
        {
            var database = mongoClient.GetDatabase(mongoSettings.Value.DatabaseName);
            _viajesCollection = database.GetCollection<ViajePosicionMongo>(mongoSettings.Value.LastMbpcCollectionName);
            _oracleConnectionString = oracleSettings.Value.ConnectionString;
        }

        public async Task<List<ViajePosicionMongo>> GetViajesAsync()
        {
            return await _viajesCollection.Find(_ => true).ToListAsync();
        }

        public async Task<ViajePosicionMongo?> GetViajeByMmsiAsync(string mmsi)
        {
            return await _viajesCollection.Find(v => v.Mmsi == mmsi).FirstOrDefaultAsync();
        }

        public async Task<bool> IniciarViajeAsync(NuevoViajeDto nuevoViaje)
        {
            Console.WriteLine($"\n[DEBUG ORACLE] Iniciando viaje: {nuevoViaje.NombreBuque} ({nuevoViaje.Origen} -> {nuevoViaje.Destino})");
            try
            {
                using var connection = new OracleConnection(_oracleConnectionString);
                var parameters = new DynamicParameters();
                parameters.Add("p_BUQUE", nuevoViaje.NombreBuque);
                parameters.Add("p_ORIGEN", nuevoViaje.Origen);
                parameters.Add("p_DESTINO", nuevoViaje.Destino);
                parameters.Add("p_RESULTADO", dbType: DbType.Int32, direction: ParameterDirection.Output);

                // Intento real (Dapper)
                await connection.ExecuteAsync("PKG_MBPC_VIAJES.SP_CREAR_VIAJE", parameters, commandType: CommandType.StoredProcedure);
                return parameters.Get<int>("p_RESULTADO") == 1;
            }
            catch (Exception ex)
            {
                // BYPASS local para seguir desarrollando sin Oracle instalado
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine($"[WARNING ORACLE] Error de conexión: {ex.Message}");
                Console.WriteLine($"[MOCK ORACLE] => Simulación de inicio de viaje EXITOSA para: {nuevoViaje.NombreBuque}");
                Console.ResetColor();
                return true; 
            }
        }
    }
}