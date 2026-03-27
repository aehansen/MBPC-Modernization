using Dapper;
using Oracle.ManagedDataAccess.Client;
using MongoDB.Driver;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Caching.Memory;
using Mbpc.Api.Models.Config;
using Mbpc.Api.Models.Mongo;
using Mbpc.Api.DTOs;
using System.Data;

namespace Mbpc.Api.Services
{
    public class CargaManagerService : ICargaService
    {
        // ── Dependencias de Datos ────────────────────────────────────────────
        private readonly IMongoCollection<ViajeDetalleMongo> _detailsCollection;
        private readonly IMongoCollection<ViajePosicionMongo> _viajesCollection;
        private readonly string _oracleConnectionString;
        
        // ── Utilidades ───────────────────────────────────────────────────────
        private readonly ILogger<CargaManagerService> _logger;
        private readonly IWebHostEnvironment _env;
        private readonly IMemoryCache _cache; // Inyectado para la Misión 1 (Consistencia)

        // ── Claves de Caché ──────────────────────────────────────────────────
        private const string CacheKeyPrefixCargas = "cargas_viaje_";

        public CargaManagerService(
            IMongoClient mongoClient,
            IOptions<MongoDbSettings> mongoSettings,
            IOptions<OracleDbSettings> oracleSettings,
            ILogger<CargaManagerService> logger,
            IWebHostEnvironment env,
            IMemoryCache cache)
        {
            var database = mongoClient.GetDatabase(mongoSettings.Value.DatabaseName);
            _detailsCollection  = database.GetCollection<ViajeDetalleMongo>(mongoSettings.Value.DetailsMbpcCollectionName);
            _viajesCollection   = database.GetCollection<ViajePosicionMongo>(mongoSettings.Value.LastMbpcCollectionName);
            _oracleConnectionString = oracleSettings.Value.ConnectionString;
            _logger = logger;
            _env    = env;
            _cache  = cache;
        }

        // ── LECTURA (MongoDB + Caché) ────────────────────────────────────────

        public IEnumerable<CargaDto> ObtenerCargasPorViaje(string parametroBusqueda)
        {
            // 1. Verificamos si la lista ya está en la memoria Caché
            var cacheKey = $"{CacheKeyPrefixCargas}{parametroBusqueda}";
            if (_cache.TryGetValue(cacheKey, out IEnumerable<CargaDto>? cachedCargas) && cachedCargas != null)
            {
                _logger.LogDebug("CACHE HIT — Devolviendo cargas para parámetro: {Parametro}", parametroBusqueda);
                return cachedCargas;
            }

            _logger.LogInformation("CACHE MISS — Buscando cargas en MongoDB para parámetro: {Parametro}", parametroBusqueda);

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

            var resultado = detalleConCargas.Barcazas.Select(b => new CargaDto
            {
                Id               = b.Nombre ?? Guid.NewGuid().ToString(),
                ViajeId          = nombreBuque,
                DescripcionLista = $"{b.Nombre} - {b.Carga} ({b.Cantidad} {b.Unidad})",
                NivelRiesgo      = "Bajo",
                MuelleActual     = b.MuelleActual,
                Tonelaje         = b.Cantidad 
            }).ToList();

            // 2. Guardamos el resultado en Caché
            var cacheOptions = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5),
                SlidingExpiration               = TimeSpan.FromMinutes(2)
            };
            _cache.Set(cacheKey, resultado, cacheOptions);

            return resultado;
        }

        // ── ESCRITURA (Oracle + CQRS Mongo + Invalidación Caché) ─────────────

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
                        InvalidarCacheViajePorBarcaza(id); // Limpiar caché
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
                        InvalidarCacheViajePorBarcaza(id); // Limpiar caché
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

            return exitoOracle;
        }

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

            if (exitoOracle)
            {
                try
                {
                    _logger.LogInformation("Sincronizando CANTIDAD EXACTA ({Toneladas}) en MongoDB para embarcación {Id}", toneladas, id);

                    var filter = Builders<ViajeDetalleMongo>.Filter.Eq("barcazas.BARCAZA", id);
                    var update = Builders<ViajeDetalleMongo>.Update.Set("barcazas.$.CANTIDAD", toneladas);
                    var result = _detailsCollection.UpdateOne(filter, update);

                    if (result.ModifiedCount > 0)
                    {
                        _logger.LogInformation("¡CQRS Exitoso! CANTIDAD actualizada en Mongo para embarcación {Id}.", id);
                        InvalidarCacheViajePorBarcaza(id); // Limpiar caché
                    }
                    else
                    {
                        _logger.LogWarning("Mongo no encontró la embarcación '{Id}' para actualizar la carga.", id);
                    }
                }
                catch (Exception mongoEx)
                {
                    _logger.LogError(mongoEx, "Fallo al sincronizar MongoDB (carga) para la embarcación {Id}.", id);
                }
            }

            return exitoOracle;
        }

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

            if (exitoOracle)
            {
                try
                {
                    _logger.LogInformation("Sincronizando CANTIDAD EXACTA ({Toneladas}) en MongoDB para embarcación {Id}", toneladas, id);

                    var filter = Builders<ViajeDetalleMongo>.Filter.Eq("barcazas.BARCAZA", id);
                    var update = Builders<ViajeDetalleMongo>.Update.Set("barcazas.$.CANTIDAD", toneladas);

                    if (toneladas == 0)
                    {
                        update = update.Set("barcazas.$.CARGA", "EN LASTRE");
                        _logger.LogInformation("Embarcación {Id} quedó con 0tn. Modificando estado de carga a EN LASTRE.", id);
                    }

                    var result = _detailsCollection.UpdateOne(filter, update);

                    if (result.ModifiedCount > 0)
                    {
                        _logger.LogInformation("¡CQRS Exitoso! CANTIDAD actualizada en Mongo para embarcación {Id}.", id);
                        InvalidarCacheViajePorBarcaza(id); // Limpiar caché
                    }
                    else
                    {
                        _logger.LogWarning("Mongo no encontró la embarcación '{Id}' para actualizar la descarga.", id);
                    }
                }
                catch (Exception mongoEx)
                {
                    _logger.LogError(mongoEx, "Fallo al sincronizar MongoDB (descarga) para la embarcación {Id}.", id);
                }
            }

            return exitoOracle;
        }

        public async Task<bool> AgregarCargaAsync(string nombreBuque, NuevaCargaDto nuevaCarga)
        {
            _logger.LogInformation(
                "Agregando carga '{Nombre}' (tipo: {Tipo}, {Tonelaje}tn) al buque '{Buque}'.",
                nuevaCarga.Nombre, nuevaCarga.Tipo, nuevaCarga.Tonelaje, nombreBuque);

            bool exitoOracle = false;

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

            if (exitoOracle)
            {
                try
                {
                    _logger.LogInformation(
                        "Sincronizando nueva carga '{Nombre}' en MongoDB (details_mbpc) para buque '{Buque}'.",
                        nuevaCarga.Nombre, nombreBuque);

                    var nuevaBarcazaDoc = new BarcazaMongo
                    {
                        Nombre      = nuevaCarga.Nombre,
                        Carga       = nuevaCarga.Tipo,
                        Cantidad    = nuevaCarga.Tonelaje,
                        Unidad      = "Tn",
                        MuelleActual = null
                    };

                    var filtro = Builders<ViajeDetalleMongo>.Filter.Eq("VesselName", nombreBuque);
                    var update = Builders<ViajeDetalleMongo>.Update.Push("barcazas", nuevaBarcazaDoc);
                    var options = new UpdateOptions { IsUpsert = true };

                    var result = await _detailsCollection.UpdateOneAsync(filtro, update, options);

                    if (result.UpsertedId != null || result.ModifiedCount > 0)
                    {
                        _logger.LogInformation("¡CQRS Exitoso! Carga inyectada para buque '{Buque}'.", nombreBuque);
                        
                        // En este caso ya tenemos el nombre del buque directo para invalidar la caché
                        _cache.Remove($"{CacheKeyPrefixCargas}{nombreBuque}");
                    }
                }
                catch (Exception mongoEx)
                {
                    _logger.LogError(mongoEx,
                        "Fallo al sincronizar MongoDB (Push) para la nueva carga '{Nombre}' del buque '{Buque}'.",
                        nuevaCarga.Nombre, nombreBuque);
                }
            }

            return exitoOracle;
        }

        // ── Helper Privado: Invalidación Inteligente de Caché ────────────────
        
        /// <summary>
        /// Busca a qué buque pertenece la barcaza recién actualizada y elimina 
        /// la lista completa de cargas de ese buque de la memoria caché.
        /// De esta forma, el próximo GET leerá los datos frescos desde Mongo.
        /// </summary>
        private void InvalidarCacheViajePorBarcaza(string idBarcaza)
        {
            try
            {
                // Buscamos a qué buque pertenece la barcaza
                var filter = Builders<ViajeDetalleMongo>.Filter.Eq("barcazas.BARCAZA", idBarcaza);
                var buqueDoc = _detailsCollection.Find(filter).FirstOrDefault();

                if (buqueDoc != null && !string.IsNullOrWhiteSpace(buqueDoc.VesselName))
                {
                    var cacheKey = $"{CacheKeyPrefixCargas}{buqueDoc.VesselName}";
                    _cache.Remove(cacheKey);
                    _logger.LogInformation("Caché invalidada exitosamente para el viaje: {Viaje}", buqueDoc.VesselName);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "No se pudo limpiar la caché del viaje tras actualizar la barcaza {Id}", idBarcaza);
            }
        }
    }
}