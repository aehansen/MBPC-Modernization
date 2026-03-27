using Dapper;
using Oracle.ManagedDataAccess.Client;
using MongoDB.Driver;
using Microsoft.Extensions.Options;
using Mbpc.Api.Models.Config;
using Mbpc.Api.Models.Mongo;
using Mbpc.Api.DTOs;
using System.Data;

namespace Mbpc.Api.Services
{
    public class CargaManagerService : ICargaService
    {
        // Motor de Lectura (MongoDB)
        private readonly IMongoCollection<ViajeDetalleMongo> _detailsCollection;
        private readonly IMongoCollection<ViajePosicionMongo> _viajesCollection;

        // Motor de Escritura (Oracle)
        private readonly string _oracleConnectionString;

        private readonly ILogger<CargaManagerService> _logger;
        private readonly IWebHostEnvironment _env;

        public CargaManagerService(
            IMongoClient mongoClient,
            IOptions<MongoDbSettings> mongoSettings,
            IOptions<OracleDbSettings> oracleSettings,
            ILogger<CargaManagerService> logger,
            IWebHostEnvironment env)
        {
            var database = mongoClient.GetDatabase(mongoSettings.Value.DatabaseName);
            _detailsCollection  = database.GetCollection<ViajeDetalleMongo>(mongoSettings.Value.DetailsMbpcCollectionName);
            _viajesCollection   = database.GetCollection<ViajePosicionMongo>(mongoSettings.Value.LastMbpcCollectionName);
            _oracleConnectionString = oracleSettings.Value.ConnectionString;
            _logger = logger;
            _env    = env;
        }

        // ── LECTURA (MongoDB) ────────────────────────────────────────────────

        public IEnumerable<CargaDto> ObtenerCargasPorViaje(string parametroBusqueda)
        {
            _logger.LogInformation("Buscando cargas para parámetro: {Parametro}", parametroBusqueda);

            string nombreBuque = parametroBusqueda;

            // Si el parámetro es un ObjectId válido, buscamos primero el nombre del buque
            if (parametroBusqueda.Length == 24
                && MongoDB.Bson.ObjectId.TryParse(parametroBusqueda, out var objectId))
            {
                _logger.LogDebug("Parámetro es ObjectId. Resolviendo VesselName desde last_mbpc...");

                var filtroViaje = Builders<ViajePosicionMongo>.Filter.Eq("_id", objectId);
                var viaje = _viajesCollection.Find(filtroViaje).FirstOrDefault();

                if (viaje != null && !string.IsNullOrWhiteSpace(viaje.VesselName))
                    nombreBuque = viaje.VesselName;
            }

            _logger.LogDebug("Buscando en details_mbpc por VesselName: {NombreBuque}", nombreBuque);

            var filtroDetalles = Builders<ViajeDetalleMongo>.Filter.Eq("VesselName", nombreBuque);
            var detalles = _detailsCollection.Find(filtroDetalles).ToList();
            var detalleConCargas = detalles.FirstOrDefault(d => d.Barcazas != null && d.Barcazas.Any());

            if (detalleConCargas == null)
            {
                _logger.LogInformation("No se encontraron cargas para: {NombreBuque}", nombreBuque);
                return Enumerable.Empty<CargaDto>();
            }

            _logger.LogInformation(
                "{Count} barcazas encontradas para: {NombreBuque}",
                detalleConCargas.Barcazas!.Count, nombreBuque);

            return detalleConCargas.Barcazas.Select(b => new CargaDto
            {
                Id               = b.Nombre ?? Guid.NewGuid().ToString(),
                ViajeId          = nombreBuque,
                DescripcionLista = $"{b.Nombre} - {b.Carga} ({b.Cantidad} {b.Unidad})",
                NivelRiesgo      = "Bajo",
                MuelleActual     = b.MuelleActual,
                Tonelaje         = b.Cantidad 
            });
        }

        // ── ESCRITURA (Oracle) ───────────────────────────────────────────────

        public bool AmarrarBarcaza(string id, string nuevoMuelle)
        {
            _logger.LogInformation("Amarrando barcaza {Id} en muelle {Muelle}", id, nuevoMuelle);
            bool exitoOracle = false;

            try
            {
                using var connection = new OracleConnection(_oracleConnectionString);
                var parameters = new DynamicParameters();
                parameters.Add("p_ID_BARCAZA", id);
                parameters.Add("p_MUELLE", nuevoMuelle);
                parameters.Add("p_RESULTADO", dbType: DbType.Int32, direction: ParameterDirection.Output);

                connection.Execute(
                    "PKG_MBPC_CARGAS.SP_AMARRAR",
                    parameters,
                    commandType: CommandType.StoredProcedure);

                exitoOracle = parameters.Get<int>("p_RESULTADO") == 1;
            }
            catch (OracleException ex)
            {
                if (!_env.IsDevelopment())
                {
                    _logger.LogError(ex, "Error de Oracle en producción al amarrar barcaza {Id}.", id);
                    throw;
                }

                _logger.LogWarning(
                    "Oracle no disponible en desarrollo. Simulando amarre de {Id} en {Muelle}. Error: {Message}",
                    id, nuevoMuelle, ex.Message);
                
                exitoOracle = true; // Bypass de desarrollo
            }

            // --- INICIO LÓGICA CQRS (ACTUALIZACIÓN DUAL) ---
            if (exitoOracle)
            {
                try 
                {
                    _logger.LogInformation("Sincronizando estado en MongoDB para barcaza {Id}", id);
                    
                    var filter = Builders<ViajeDetalleMongo>.Filter.Eq("barcazas.BARCAZA", id);
                    var update = Builders<ViajeDetalleMongo>.Update.Set("barcazas.$.MUELLE_ACTUAL", nuevoMuelle);
                    
                    var result = _detailsCollection.UpdateOne(filter, update);
                    
                    if (result.ModifiedCount > 0)
                    {
                        _logger.LogInformation("¡CQRS Exitoso! Mongo actualizado para la barcaza {Id}.", id);
                    }
                    else
                    {
                        _logger.LogWarning("Mongo no encontró la barcaza '{Id}' para actualizar. Revisa que no haya espacios en blanco extra.", id);
                    }
                }
                catch(Exception mongoEx)
                {
                    _logger.LogError(mongoEx, "Fallo al sincronizar MongoDB para la barcaza {Id}. Se sincronizará en el próximo batch.", id);
                }
            }
            // --- FIN LÓGICA CQRS ---

            return exitoOracle;
        }

        public bool FondearBarcaza(string id, string zonaFondeo)
        {
            _logger.LogInformation("Fondeando barcaza {Id} en zona {Zona}", id, zonaFondeo);
            bool exitoOracle = false;

            try
            {
                using var connection = new OracleConnection(_oracleConnectionString);
                var parameters = new DynamicParameters();
                parameters.Add("p_ID_BARCAZA", id);
                parameters.Add("p_ZONA_FONDEO", zonaFondeo);
                parameters.Add("p_RESULTADO",   dbType: DbType.Int32, direction: ParameterDirection.Output);

                connection.Execute(
                    "PKG_MBPC_CARGAS.SP_FONDEAR",
                    parameters,
                    commandType: CommandType.StoredProcedure);

                exitoOracle = parameters.Get<int>("p_RESULTADO") == 1;
            }
            catch (OracleException ex)
            {
                if (!_env.IsDevelopment())
                {
                    _logger.LogError(ex, "Error de Oracle en producción al fondear barcaza {Id}.", id);
                    throw;
                }

                _logger.LogWarning(
                    "Oracle no disponible en desarrollo. Simulando fondeo de {Id} en {Zona}. Error: {Message}",
                    id, zonaFondeo, ex.Message);
                
                exitoOracle = true; // Bypass de desarrollo
            }

            // --- INICIO LÓGICA CQRS (ACTUALIZACIÓN DUAL) ---
            if (exitoOracle)
            {
                try 
                {
                    _logger.LogInformation("Sincronizando estado en MongoDB (Fondeo) para barcaza {Id}", id);
                    
                    var filter = Builders<ViajeDetalleMongo>.Filter.Eq("barcazas.BARCAZA", id);
                    
                    // Al fondear, enviamos NULL para que desaparezca el muelle y React muestre "En Tránsito"
                    var update = Builders<ViajeDetalleMongo>.Update.Set("barcazas.$.MUELLE_ACTUAL", (string?)null);
                    
                    var result = _detailsCollection.UpdateOne(filter, update);
                    
                    if (result.ModifiedCount > 0)
                    {
                        _logger.LogInformation("¡CQRS Exitoso! Mongo actualizado (Barcaza {Id} fondeada).", id);
                    }
                    else
                    {
                        _logger.LogWarning("Mongo no encontró la barcaza '{Id}' para actualizar el fondeo.", id);
                    }
                }
                catch(Exception mongoEx)
                {
                    _logger.LogError(mongoEx, "Fallo al sincronizar MongoDB para el fondeo de la barcaza {Id}.", id);
                }
            }
            // --- FIN LÓGICA CQRS ---

            return exitoOracle;
        }

        /// <summary>
        /// Registra la carga de toneladas en una embarcación.
        /// Lógica CQRS: escribe en Oracle (o simula en DEV) y establece CANTIDAD final en MongoDB.
        /// </summary>
        public bool CargarBarcaza(string id, double toneladas)
        {
            _logger.LogInformation("Registrando tonelaje final de {Toneladas}tn en embarcación {Id}", toneladas, id);
            bool exitoOracle = false;

            try
            {
                using var connection = new OracleConnection(_oracleConnectionString);
                var parameters = new DynamicParameters();
                parameters.Add("p_ID_BARCAZA",  id);
                parameters.Add("p_TONELADAS",   toneladas);
                parameters.Add("p_RESULTADO",   dbType: DbType.Int32, direction: ParameterDirection.Output);

                connection.Execute(
                    "PKG_MBPC_CARGAS.SP_CARGAR",
                    parameters,
                    commandType: CommandType.StoredProcedure);

                exitoOracle = parameters.Get<int>("p_RESULTADO") == 1;
            }
            catch (OracleException ex)
            {
                if (!_env.IsDevelopment())
                {
                    _logger.LogError(ex, "Error de Oracle en producción al cargar embarcación {Id}.", id);
                    throw;
                }

                _logger.LogWarning(
                    "Oracle no disponible en desarrollo. Simulando carga final a {Toneladas}tn en {Id}. Error: {Message}",
                    toneladas, id, ex.Message);

                exitoOracle = true; // Bypass de desarrollo
            }

            // --- INICIO LÓGICA CQRS (ACTUALIZACIÓN DUAL) ---
            if (exitoOracle)
            {
                try
                {
                    _logger.LogInformation("Sincronizando CANTIDAD EXACTA ({Toneladas}) en MongoDB para embarcación {Id}", toneladas, id);

                    var filter = Builders<ViajeDetalleMongo>.Filter.Eq("barcazas.BARCAZA", id);

                    // Set con valor absoluto para reflejar el tonelaje final
                    var update = Builders<ViajeDetalleMongo>.Update.Set("barcazas.$.CANTIDAD", toneladas);

                    var result = _detailsCollection.UpdateOne(filter, update);

                    if (result.ModifiedCount > 0)
                        _logger.LogInformation("¡CQRS Exitoso! CANTIDAD actualizada en Mongo para embarcación {Id}.", id);
                    else
                        _logger.LogWarning("Mongo no encontró la embarcación '{Id}' para actualizar la carga.", id);
                }
                catch (Exception mongoEx)
                {
                    _logger.LogError(mongoEx, "Fallo al sincronizar MongoDB (carga) para la embarcación {Id}.", id);
                }
            }
            // --- FIN LÓGICA CQRS ---

            return exitoOracle;
        }

        /// <summary>
        /// Registra la descarga de toneladas en una embarcación.
        /// Lógica CQRS: escribe en Oracle (o simula en DEV) y establece CANTIDAD final en MongoDB.
        /// </summary>
        public bool DescargarBarcaza(string id, double toneladas)
        {
            _logger.LogInformation("Registrando descarga a {Toneladas}tn finales de embarcación {Id}", toneladas, id);
            bool exitoOracle = false;

            try
            {
                using var connection = new OracleConnection(_oracleConnectionString);
                var parameters = new DynamicParameters();
                parameters.Add("p_ID_BARCAZA",  id);
                parameters.Add("p_TONELADAS",   toneladas);
                parameters.Add("p_RESULTADO",   dbType: DbType.Int32, direction: ParameterDirection.Output);

                connection.Execute(
                    "PKG_MBPC_CARGAS.SP_DESCARGAR",
                    parameters,
                    commandType: CommandType.StoredProcedure);

                exitoOracle = parameters.Get<int>("p_RESULTADO") == 1;
            }
            catch (OracleException ex)
            {
                if (!_env.IsDevelopment())
                {
                    _logger.LogError(ex, "Error de Oracle en producción al descargar embarcación {Id}.", id);
                    throw;
                }

                _logger.LogWarning(
                    "Oracle no disponible en desarrollo. Simulando descarga final a {Toneladas}tn de {Id}. Error: {Message}",
                    toneladas, id, ex.Message);

                exitoOracle = true; // Bypass de desarrollo
            }

            // --- INICIO LÓGICA CQRS (ACTUALIZACIÓN DUAL) ---
            if (exitoOracle)
            {
                try
                {
                    _logger.LogInformation("Sincronizando CANTIDAD EXACTA ({Toneladas}) en MongoDB para embarcación {Id}", toneladas, id);

                    var filter = Builders<ViajeDetalleMongo>.Filter.Eq("barcazas.BARCAZA", id);

                    // Set con valor absoluto para reflejar el tonelaje final
                    var update = Builders<ViajeDetalleMongo>.Update.Set("barcazas.$.CANTIDAD", toneladas);

                    // Regla de Negocio: Si la carga llega a 0, mutar el estado a "EN LASTRE"
                    if (toneladas == 0)
                    {
                        update = update.Set("barcazas.$.CARGA", "EN LASTRE");
                        _logger.LogInformation("Embarcación {Id} quedó con 0tn. Modificando estado de carga a EN LASTRE.", id);
                    }

                    var result = _detailsCollection.UpdateOne(filter, update);

                    if (result.ModifiedCount > 0)
                        _logger.LogInformation("¡CQRS Exitoso! CANTIDAD actualizada en Mongo para embarcación {Id}.", id);
                    else
                        _logger.LogWarning("Mongo no encontró la embarcación '{Id}' para actualizar la descarga.", id);
                }
                catch (Exception mongoEx)
                {
                    _logger.LogError(mongoEx, "Fallo al sincronizar MongoDB (descarga) para la embarcación {Id}.", id);
                }
            }
            // --- FIN LÓGICA CQRS ---

            return exitoOracle;
        }

        // ── TAREA 2: AGREGAR CARGA AL VIAJE ─────────────────────────────────

        /// <summary>
        /// Agrega una nueva carga (barcaza o bodega) al array de barcazas del buque.
        /// CQRS:
        ///   - Escritura: simula SP en Oracle (PKG_MBPC_CARGAS.SP_AGREGAR_CARGA) con bypass DEV.
        ///   - Lectura: Update.Push sobre el array "barcazas" en la colección details_mbpc.
        /// Si el documento del buque no existe en details_mbpc, se crea uno nuevo (upsert).
        /// </summary>
        public async Task<bool> AgregarCargaAsync(string nombreBuque, NuevaCargaDto nuevaCarga)
        {
            _logger.LogInformation(
                "Agregando carga '{Nombre}' (tipo: {Tipo}, {Tonelaje}tn) al buque '{Buque}'.",
                nuevaCarga.Nombre, nuevaCarga.Tipo, nuevaCarga.Tonelaje, nombreBuque);

            bool exitoOracle = false;

            // --- ESCRITURA (Oracle) ---
            try
            {
                using var connection = new OracleConnection(_oracleConnectionString);
                var parameters = new DynamicParameters();
                parameters.Add("p_BUQUE",     nombreBuque);
                parameters.Add("p_NOMBRE",    nuevaCarga.Nombre);
                parameters.Add("p_TIPO",      nuevaCarga.Tipo);
                parameters.Add("p_TONELAJE",  nuevaCarga.Tonelaje);
                parameters.Add("p_RESULTADO", dbType: DbType.Int32, direction: ParameterDirection.Output);

                await connection.ExecuteAsync(
                    "PKG_MBPC_CARGAS.SP_AGREGAR_CARGA",
                    parameters,
                    commandType: CommandType.StoredProcedure);

                exitoOracle = parameters.Get<int>("p_RESULTADO") == 1;
            }
            catch (OracleException ex)
            {
                if (!_env.IsDevelopment())
                {
                    _logger.LogError(ex, "Error de Oracle en producción al agregar carga al buque {Buque}.", nombreBuque);
                    throw;
                }

                _logger.LogWarning(
                    "Oracle no disponible en desarrollo. Bypass DEV activado para agregar carga '{Nombre}' al buque '{Buque}'. Error: {Message}",
                    nuevaCarga.Nombre, nombreBuque, ex.Message);

                exitoOracle = true; // Bypass de desarrollo
            }

            // --- INICIO LÓGICA CQRS (Update.Push en MongoDB) ---
            if (exitoOracle)
            {
                try
                {
                    _logger.LogInformation(
                        "Sincronizando nueva carga '{Nombre}' en MongoDB (details_mbpc) para buque '{Buque}'.",
                        nuevaCarga.Nombre, nombreBuque);

                    // Construimos el sub-documento de barcaza a inyectar
                    var nuevaBarcazaDoc = new BarcazaMongo
                    {
                        Nombre      = nuevaCarga.Nombre,
                        Carga       = nuevaCarga.Tipo,       // "Barcaza" o "Bodega" — mapea al campo CARGA
                        Cantidad    = nuevaCarga.Tonelaje,
                        Unidad      = "Tn",
                        MuelleActual = null                  // Nace sin muelle asignado
                    };

                    var filtro = Builders<ViajeDetalleMongo>.Filter.Eq("VesselName", nombreBuque);

                    // Update.Push agrega el nuevo sub-documento al array "barcazas"
                    var update = Builders<ViajeDetalleMongo>.Update.Push("barcazas", nuevaBarcazaDoc);

                    // Upsert: si el documento no existe en details_mbpc, lo crea
                    var options = new UpdateOptions { IsUpsert = true };

                    var result = await _detailsCollection.UpdateOneAsync(filtro, update, options);

                    if (result.UpsertedId != null)
                        _logger.LogInformation(
                            "¡CQRS Exitoso! Documento creado en details_mbpc para buque '{Buque}' con primera carga '{Nombre}'.",
                            nombreBuque, nuevaCarga.Nombre);
                    else if (result.ModifiedCount > 0)
                        _logger.LogInformation(
                            "¡CQRS Exitoso! Carga '{Nombre}' inyectada en el array barcazas del buque '{Buque}'.",
                            nuevaCarga.Nombre, nombreBuque);
                    else
                        _logger.LogWarning(
                            "El Update.Push no modificó ningún documento para el buque '{Buque}'. Verificar colección details_mbpc.",
                            nombreBuque);
                }
                catch (Exception mongoEx)
                {
                    _logger.LogError(mongoEx,
                        "Fallo al sincronizar MongoDB (Push) para la nueva carga '{Nombre}' del buque '{Buque}'.",
                        nuevaCarga.Nombre, nombreBuque);
                    // No revertimos exitoOracle — Oracle ya escribió; el batch de Mongo reintentará
                }
            }
            // --- FIN LÓGICA CQRS ---

            return exitoOracle;
        }
    }
}
