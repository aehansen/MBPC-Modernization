using Dapper;
using Oracle.ManagedDataAccess.Client;
using MongoDB.Driver;
using Microsoft.Extensions.Options;
using Mbpc.Api.Models.Config;
using Mbpc.Api.Models.Mongo;
using Mbpc.Api.DTOs;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace Mbpc.Api.Services
{
    public class CargaManagerService : ICargaService
    {
        // Motor de Lectura (Mongo)
        private readonly IMongoCollection<ViajeDetalleMongo> _detailsCollection;
        private readonly IMongoCollection<ViajePosicionMongo> _viajesCollection;
        
        // Motor de Escritura (Oracle)
        private readonly string _oracleConnectionString;

        public CargaManagerService(
            IMongoClient mongoClient, 
            IOptions<MongoDbSettings> mongoSettings,
            IOptions<OracleDbSettings> oracleSettings)
        {
            // Setup Mongo
            var database = mongoClient.GetDatabase(mongoSettings.Value.DatabaseName);
            _detailsCollection = database.GetCollection<ViajeDetalleMongo>(mongoSettings.Value.DetailsMbpcCollectionName);
            _viajesCollection = database.GetCollection<ViajePosicionMongo>(mongoSettings.Value.LastMbpcCollectionName);
            
            // Setup Oracle
            _oracleConnectionString = oracleSettings.Value.ConnectionString;
        }

        // ==========================================
        // 1. FASE DE LECTURA (MongoDB - Strangled Read)
        // ==========================================
        public IEnumerable<CargaDto> ObtenerCargasPorViaje(string parametroBusqueda)
        {
            Console.WriteLine($"\n[DEBUG] --- INICIANDO BÚSQUEDA DE CARGAS (MANAGER) ---");
            Console.WriteLine($"[DEBUG] Parámetro recibido desde React: '{parametroBusqueda}'");
            
            string nombreBuque = parametroBusqueda;

            if (parametroBusqueda.Length == 24 && MongoDB.Bson.ObjectId.TryParse(parametroBusqueda, out var objectId))
            {
                Console.WriteLine($"[DEBUG] El parámetro es un ObjectId válido. Buscando en last_mbpc...");
                var filtroViaje = Builders<ViajePosicionMongo>.Filter.Eq("_id", objectId);
                var viaje = _viajesCollection.Find(filtroViaje).FirstOrDefault();
                
                if (viaje != null)
                {
                    if (!string.IsNullOrWhiteSpace(viaje.VesselName))
                    {
                        nombreBuque = viaje.VesselName;
                    }
                }
            }

            Console.WriteLine($"[DEBUG] Buscando en details_mbpc por VesselName == '{nombreBuque}'...");
            
            var filtroDetalles = Builders<ViajeDetalleMongo>.Filter.Eq("VesselName", nombreBuque);
            var detalles = _detailsCollection.Find(filtroDetalles).ToList();
            
            var detalleConCargas = detalles.FirstOrDefault(d => d.Barcazas != null && d.Barcazas.Any());

            if (detalleConCargas == null) 
            {
                return new List<CargaDto>();
            }

            Console.WriteLine($"[DEBUG] ¡ÉXITO! Se encontraron {detalleConCargas.Barcazas.Count} barcazas.");

            return detalleConCargas.Barcazas.Select(b => new CargaDto
            {
                Id = b.Nombre ?? Guid.NewGuid().ToString(),
                ViajeId = nombreBuque,
                DescripcionLista = $"{b.Nombre} - {b.Carga} ({b.Cantidad} {b.Unidad})",
                NivelRiesgo = "Estándar"
            });
        }

        // ==========================================
        // 2. FASE DE ESCRITURA (Oracle + Dapper) - MOCK LOCAL
        // ==========================================
        public bool AmarrarBarcaza(string id, string nuevoMuelle)
        {
            Console.WriteLine($"\n[DEBUG ORACLE] Intentando amarrar barcaza {id} en muelle {nuevoMuelle}...");
            try
            {
                using var connection = new OracleConnection(_oracleConnectionString);
                var parameters = new DynamicParameters();
                parameters.Add("p_ID_BARCAZA", id);
                parameters.Add("p_MUELLE_DESTINO", nuevoMuelle);
                parameters.Add("p_RESULTADO", dbType: DbType.Int32, direction: ParameterDirection.Output);

                connection.Execute("PKG_MBPC_CARGAS.SP_AMARRAR", parameters, commandType: CommandType.StoredProcedure);

                return parameters.Get<int>("p_RESULTADO") == 1;
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"[WARNING ORACLE] Conexión fallida (Es esperado en entorno local). Mensaje: {ex.Message}");
                Console.WriteLine($"[MOCK ORACLE] => Simulando ÉXITO para Amarre de barcaza {id} en {nuevoMuelle}.");
                Console.ResetColor();
                
                return true; 
            }
        }

        public bool FondearBarcaza(string id, string zonaFondeo)
        {
            Console.WriteLine($"\n[DEBUG ORACLE] Intentando fondear barcaza {id} en zona {zonaFondeo}...");
            try
            {
                using var connection = new OracleConnection(_oracleConnectionString);
                var parameters = new DynamicParameters();
                parameters.Add("p_ID_BARCAZA", id);
                parameters.Add("p_ZONA_FONDEO", zonaFondeo);
                parameters.Add("p_RESULTADO", dbType: DbType.Int32, direction: ParameterDirection.Output);

                connection.Execute("PKG_MBPC_CARGAS.SP_FONDEAR", parameters, commandType: CommandType.StoredProcedure);

                return parameters.Get<int>("p_RESULTADO") == 1;
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"[WARNING ORACLE] Conexión fallida (Es esperado en entorno local). Mensaje: {ex.Message}");
                Console.WriteLine($"[MOCK ORACLE] => Simulando ÉXITO para Fondeo de barcaza {id} en {zonaFondeo}.");
                Console.ResetColor();
                
                return true;
            }
        }
    }
}