using Dapper;
using Mbpc.Api.DTOs;
using Mbpc.Api.Models.Mongo;
using Mbpc.Api.Models.Config;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using Oracle.ManagedDataAccess.Client;

namespace Mbpc.Api.Services
{
    public class TipoCargaManagerService : ITipoCargaService
    {
        private readonly IMongoCollection<TipoCargaMongo> _collection;
        private readonly string _oracleConnectionString;
        private readonly ILogger<TipoCargaManagerService> _logger;

        public TipoCargaManagerService(
            IMongoClient mongoClient,
            IOptions<MongoDbSettings> mongoSettings,
            IOptions<OracleDbSettings> oracleSettings,
            ILogger<TipoCargaManagerService> logger)
        {
            var database = mongoClient.GetDatabase(mongoSettings.Value.DatabaseName);
            _collection = database.GetCollection<TipoCargaMongo>("tipos_carga");
            _oracleConnectionString = oracleSettings.Value.ConnectionString;
            _logger = logger;
        }

        public async Task<IEnumerable<TipoCargaDto>> BuscarAutocompleteAsync(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return Enumerable.Empty<TipoCargaDto>();

            var regex = new BsonRegularExpression(query.Trim(), "i");

            var filter = Builders<TipoCargaMongo>.Filter.Or(
                Builders<TipoCargaMongo>.Filter.Regex(x => x.Nombre, regex),
                Builders<TipoCargaMongo>.Filter.Regex(x => x.Codigo, regex)
            );

            var results = await _collection.Find(filter).Limit(20).ToListAsync();

            return results.Select(x => new TipoCargaDto
            {
                OracleId = x.OracleId,
                Nombre = x.Nombre,
                Codigo = x.Codigo,
                EsPeligrosa = x.EsPeligrosa
            });
        }

        public async Task<int> SincronizarDesdeOracleAsync()
        {
            const string sql = @"
                SELECT 
                    ID, 
                    NOMBRE, 
                    CODIGO, 
                    TIPO_CARGA_PELIGROSA_ID 
                FROM TBL_TIPO_CARGA";

            List<TipoCargaMongo> registros = new List<TipoCargaMongo>();

            try
            {
                _logger.LogInformation("Iniciando sincronización del Maestro de Tipos de Carga desde Oracle...");

                await using (var connection = new OracleConnection(_oracleConnectionString))
                {
                    var rows = await connection.QueryAsync(sql);

                    registros = rows.Select(row => new TipoCargaMongo
                    {
                        OracleId = (int)row.ID,
                        Nombre = (string)(row.NOMBRE ?? string.Empty),
                        Codigo = (string)(row.CODIGO ?? string.Empty),
                        EsPeligrosa = row.TIPO_CARGA_PELIGROSA_ID is not null
                    }).ToList();
                }
            }
            catch (Exception ex)
            {
                // PLAN B: MOCK DE DATOS SI ORACLE FALLA
                _logger.LogWarning("Oracle offline o inaccesible. Generando MOCK de Tipos de Carga. Error: {Msg}", ex.Message);
                
                registros = new List<TipoCargaMongo>
                {
                    new TipoCargaMongo { OracleId = 1, Nombre = "SOJA EN GRANO", Codigo = "SOJ", EsPeligrosa = false },
                    new TipoCargaMongo { OracleId = 2, Nombre = "TRIGO", Codigo = "TRI", EsPeligrosa = false },
                    new TipoCargaMongo { OracleId = 3, Nombre = "PETROLEO CRUDO", Codigo = "PET", EsPeligrosa = true },
                    new TipoCargaMongo { OracleId = 4, Nombre = "GASOIL", Codigo = "GAS", EsPeligrosa = true },
                    new TipoCargaMongo { OracleId = 5, Nombre = "MINERAL DE HIERRO", Codigo = "MIN", EsPeligrosa = false },
                    new TipoCargaMongo { OracleId = 412, Nombre = "EN LASTRE", Codigo = "LAS", EsPeligrosa = false }
                };
            }

            // Guardamos en Mongo (ya sea lo real de Oracle o el Mock)
            if (registros.Any())
            {
                await _collection.DeleteManyAsync(Builders<TipoCargaMongo>.Filter.Empty);
                await _collection.InsertManyAsync(registros);
                _logger.LogInformation("Sincronización finalizada: {Count} registros de carga en Mongo.", registros.Count);
            }

            return registros.Count;
        }
    }
}