using Dapper;
using Oracle.ManagedDataAccess.Client;
using Oracle.ManagedDataAccess.Types;
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
        private readonly IMongoCollection<ViajeDetalleMongo>  _detailsCollection;
        private readonly IMongoCollection<ViajePosicionMongo> _viajesCollection;
        private readonly string _oracleConnectionString;

        // ── Utilidades ───────────────────────────────────────────────────────
        private readonly ILogger<CargaManagerService> _logger;
        private readonly IWebHostEnvironment          _env;
        private readonly IMemoryCache                 _cache;

        // ── Claves de Caché ──────────────────────────────────────────────────
        private const string CacheKeyPrefixCargas = "cargas_viaje_";

        public CargaManagerService(
            IMongoClient                    mongoClient,
            IOptions<MongoDbSettings>       mongoSettings,
            IOptions<OracleDbSettings>      oracleSettings,
            ILogger<CargaManagerService>    logger,
            IWebHostEnvironment             env,
            IMemoryCache                    cache)
        {
            var database        = mongoClient.GetDatabase(mongoSettings.Value.DatabaseName);
            _detailsCollection  = database.GetCollection<ViajeDetalleMongo>(mongoSettings.Value.DetailsMbpcCollectionName);
            _viajesCollection   = database.GetCollection<ViajePosicionMongo>(mongoSettings.Value.LastMbpcCollectionName);
            _oracleConnectionString = oracleSettings.Value.ConnectionString;
            _logger = logger;
            _env    = env;
            _cache  = cache;
        }

        // ── LECTURA (Oracle Fallback + MongoDB + Caché) ──────────────────────

        public IEnumerable<CargaDto> ObtenerCargasPorViaje(string parametroBusqueda)
        {
            // ── 1. Cache check — siempre primero, independiente de la ruta ───
            var cacheKey = $"{CacheKeyPrefixCargas}{parametroBusqueda}";
            if (_cache.TryGetValue(cacheKey, out IEnumerable<CargaDto>? cachedCargas) && cachedCargas != null)
            {
                _logger.LogDebug("CACHE HIT — Devolviendo cargas para parámetro: {Parametro}", parametroBusqueda);
                return cachedCargas;
            }

            // ── 2. Detección de ruta: ¿es un TravelId numérico? ─────────────
            if (long.TryParse(parametroBusqueda, out long travelId))
            {
                _logger.LogInformation(
                    "ORACLE FALLBACK — Parámetro '{Parametro}' es un TravelId numérico. Consultando legacy Oracle.",
                    parametroBusqueda);

                return ObtenerCargasDesdeOracle(travelId, cacheKey);
            }

            // ── 3. Ruta MongoDB (Default CQRS) ───────────────────────────────
            _logger.LogInformation(
                "CACHE MISS — Parámetro '{Parametro}' resuelve a MongoDB.",
                parametroBusqueda);

            return ObtenerCargasDesdeMongoDb(parametroBusqueda, cacheKey);
        }

        // ── Ruta Oracle: TravelId → EtapaId → SP traer_cargas ───────────────

        private IEnumerable<CargaDto> ObtenerCargasDesdeOracle(long travelId, string cacheKey)
        {
            try
            {
                using var connection = new OracleConnection(_oracleConnectionString);
                connection.Open();

                // 2a. Obtener la etapa activa vinculada al viaje
                var etapaId = connection.ExecuteScalar<long?>(
                    "SELECT MAX(ETAPA_ID) FROM TBL_ETAPA WHERE VIAJE_ID = :TravelId",
                    new { TravelId = travelId });

                if (etapaId is null || etapaId == 0)
                {
                    _logger.LogInformation(
                        "Oracle — No existe etapa activa para TravelId {TravelId}. Retornando lista vacía.",
                        travelId);
                    return Enumerable.Empty<CargaDto>();
                }

                _logger.LogDebug(
                    "Oracle — EtapaId {EtapaId} encontrado para TravelId {TravelId}. Ejecutando SP traer_cargas.",
                    etapaId, travelId);

                // 2b. Ejecutar el SP con RefCursor de salida
                var spParams = new OracleDynamicParameters();
                spParams.Add("vEtapaId", etapaId.Value,              OracleDbType.Int64,     ParameterDirection.Input);
                spParams.Add("vCursor",  dbType: OracleDbType.RefCursor, direction: ParameterDirection.Output);

                var rawRows = connection.Query<OracleCargaRow>(
                    "mbpc.traer_cargas",
                    spParams,
                    commandType: CommandType.StoredProcedure);

                var resultado = rawRows.Select(r => new CargaDto
                {
                    Id               = r.Id?.ToString() ?? Guid.NewGuid().ToString(),
                    ViajeId          = travelId.ToString(),
                    DescripcionLista = r.DescripcionLista ?? $"Carga #{r.Id}",
                    NivelRiesgo      = "Bajo",
                    MuelleActual     = r.MuelleActual,
                    Tonelaje         = r.Tonelaje
                }).ToList();

                _logger.LogInformation(
                    "Oracle — {Count} cargas recuperadas para TravelId {TravelId} / EtapaId {EtapaId}.",
                    resultado.Count, travelId, etapaId);

                var cacheOptions = new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5),
                    SlidingExpiration               = TimeSpan.FromMinutes(2)
                };
                _cache.Set(cacheKey, resultado, cacheOptions);

                return resultado;
            }
            catch (OracleException ex)
            {
                if (!_env.IsDevelopment())
                {
                    _logger.LogError(ex, "Error de Oracle en producción al obtener cargas para TravelId {TravelId}.", travelId);
                    throw;
                }

                _logger.LogWarning(
                    "Oracle no disponible en desarrollo. Retornando lista vacía para TravelId {TravelId}. Error: {Message}",
                    travelId, ex.Message);

                return Enumerable.Empty<CargaDto>();
            }
        }

        // ── Ruta MongoDB: nombre de buque u ObjectId ─────────────────────────

        private IEnumerable<CargaDto> ObtenerCargasDesdeMongoDb(string parametroBusqueda, string cacheKey)
        {
            string nombreBuque = parametroBusqueda;

            // Si el parámetro es un ObjectId válido, resolvemos el nombre del buque
            if (parametroBusqueda.Length == 24
                && MongoDB.Bson.ObjectId.TryParse(parametroBusqueda, out var objectId))
            {
                _logger.LogDebug("Parámetro es ObjectId. Resolviendo VesselName desde last_mbpc...");

                var filtroViaje = Builders<ViajePosicionMongo>.Filter.Eq("_id", objectId);
                var viaje       = _viajesCollection.Find(filtroViaje).FirstOrDefault();

                if (viaje != null && !string.IsNullOrWhiteSpace(viaje.VesselName))
                    nombreBuque = viaje.VesselName;
            }

            _logger.LogDebug("Buscando en details_mbpc por VesselName: {NombreBuque}", nombreBuque);

            var filtroDetalles = Builders<ViajeDetalleMongo>.Filter.Eq(d => d.VesselName, nombreBuque);
            var detalles       = _detailsCollection.Find(filtroDetalles).ToList();

            // REFACTOR: las barcazas viven dentro de cada Etapa — usamos SelectMany para aplanarlas.
            var detalleConCargas = detalles.FirstOrDefault(d =>
                d.Etapas != null && d.Etapas.Any(e => e.Barcazas != null && e.Barcazas.Any()));

            if (detalleConCargas == null)
            {
                _logger.LogInformation("No se encontraron cargas para: {NombreBuque}", nombreBuque);
                return Enumerable.Empty<CargaDto>();
            }

            // Aplanamos todas las barcazas de todas las etapas del documento.
            var todasLasBarcazas = detalleConCargas.Etapas
                .SelectMany(e => e.Barcazas ?? new List<BarcazaMongo>())
                .Where(b => b is not null)
                .ToList();

            _logger.LogInformation(
                "{Count} barcazas encontradas (vía Etapas) para: {NombreBuque}",
                todasLasBarcazas.Count, nombreBuque);

            var resultado = todasLasBarcazas.Select(b => new CargaDto
            {
                Id               = b.Nombre ?? Guid.NewGuid().ToString(),
                ViajeId          = nombreBuque,
                DescripcionLista = $"{b.Nombre} - {b.Carga} ({b.Cantidad} {b.Unidad})",
                NivelRiesgo      = "Bajo",
                MuelleActual     = b.MuelleActual,
                Tonelaje         = b.Cantidad
            }).ToList();

            var cacheOptions = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5),
                SlidingExpiration               = TimeSpan.FromMinutes(2)
            };
            _cache.Set(cacheKey, resultado, cacheOptions);

            return resultado;
        }

        // ── DTO interno para mapeo del cursor Oracle ─────────────────────────

        /// <summary>
        /// Clase de mapeo intermedia para las columnas devueltas por mbpc.traer_cargas.
        /// Los nombres de propiedad deben coincidir exactamente con los alias de columna
        /// que expone el cursor del SP (case-insensitive en Dapper/Oracle).
        /// </summary>
        private sealed class OracleCargaRow
        {
            public long?   Id               { get; init; }
            public string? DescripcionLista { get; init; }
            public string? MuelleActual     { get; init; }
            public double  Tonelaje         { get; init; }
        }

        // ── ESCRITURA (Oracle + CQRS Mongo Load-Mutate-Save + Invalidación Caché) ──

        public bool AmarrarBarcaza(string id, string nuevoMuelle)
        {
            _logger.LogInformation("Amarrando barcaza {Id} en muelle {Muelle}", id, nuevoMuelle);
            bool exitoOracle = false;

            try
            {
                using var connection = new OracleConnection(_oracleConnectionString);
                var parameters = new DynamicParameters();
                parameters.Add("p_ID_BARCAZA", id);
                parameters.Add("p_MUELLE",     nuevoMuelle);
                parameters.Add("p_RESULTADO",  dbType: DbType.Int32, direction: ParameterDirection.Output);

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

            if (exitoOracle)
            {
                try
                {
                    _logger.LogInformation("Sincronizando estado en MongoDB (amarre) para barcaza {Id}", id);

                    // ── Load ─────────────────────────────────────────────────
                    var filtro = Builders<ViajeDetalleMongo>.Filter.ElemMatch(
                        d => d.Etapas,
                        etapa => etapa.Barcazas != null && etapa.Barcazas.Any(b => b.Nombre == id));

                    var doc = _detailsCollection.Find(filtro).FirstOrDefault();
                    if (doc is null)
                    {
                        _logger.LogWarning("Mongo no encontró un documento con la barcaza '{Id}' para amarrar.", id);
                    }
                    else
                    {
                        // ── Mutate ───────────────────────────────────────────
                        var barcazaTarget = doc.Etapas
                            .SelectMany(e => e.Barcazas ?? new List<BarcazaMongo>())
                            .FirstOrDefault(b => b.Nombre == id);

                        if (barcazaTarget is not null)
                        {
                            barcazaTarget.MuelleActual = nuevoMuelle;

                            // ── Save ─────────────────────────────────────────
                            var filtroId = Builders<ViajeDetalleMongo>.Filter.Eq(d => d.Id, doc.Id);
                            _detailsCollection.ReplaceOne(filtroId, doc);

                            _logger.LogInformation("¡CQRS Exitoso! Mongo actualizado (amarre) para la barcaza {Id}.", id);
                            InvalidarCacheViajePorBuque(doc.VesselName);
                        }
                        else
                        {
                            _logger.LogWarning("La barcaza '{Id}' fue encontrada en el filtro pero no en la iteración LINQ.", id);
                        }
                    }
                }
                catch (Exception mongoEx)
                {
                    _logger.LogError(mongoEx, "Fallo al sincronizar MongoDB (amarre) para la barcaza {Id}. Se sincronizará en el próximo batch.", id);
                }
            }

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
                parameters.Add("p_ID_BARCAZA",  id);
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
                    _logger.LogInformation("Sincronizando estado en MongoDB (fondeo) para barcaza {Id}", id);

                    // ── Load ─────────────────────────────────────────────────
                    var filtro = Builders<ViajeDetalleMongo>.Filter.ElemMatch(
                        d => d.Etapas,
                        etapa => etapa.Barcazas != null && etapa.Barcazas.Any(b => b.Nombre == id));

                    var doc = _detailsCollection.Find(filtro).FirstOrDefault();
                    if (doc is null)
                    {
                        _logger.LogWarning("Mongo no encontró un documento con la barcaza '{Id}' para fondear.", id);
                    }
                    else
                    {
                        // ── Mutate ───────────────────────────────────────────
                        // Al fondear, se limpia el muelle para que el cliente muestre "En Tránsito"
                        var barcazaTarget = doc.Etapas
                            .SelectMany(e => e.Barcazas ?? new List<BarcazaMongo>())
                            .FirstOrDefault(b => b.Nombre == id);

                        if (barcazaTarget is not null)
                        {
                            barcazaTarget.MuelleActual = null;

                            // ── Save ─────────────────────────────────────────
                            var filtroId = Builders<ViajeDetalleMongo>.Filter.Eq(d => d.Id, doc.Id);
                            _detailsCollection.ReplaceOne(filtroId, doc);

                            _logger.LogInformation("¡CQRS Exitoso! Mongo actualizado (fondeo) para la barcaza {Id}.", id);
                            InvalidarCacheViajePorBuque(doc.VesselName);
                        }
                        else
                        {
                            _logger.LogWarning("La barcaza '{Id}' fue encontrada en el filtro pero no en la iteración LINQ.", id);
                        }
                    }
                }
                catch (Exception mongoEx)
                {
                    _logger.LogError(mongoEx, "Fallo al sincronizar MongoDB (fondeo) para la barcaza {Id}.", id);
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
                parameters.Add("p_ID_BARCAZA", id);
                parameters.Add("p_TONELADAS",  toneladas);
                parameters.Add("p_RESULTADO",  dbType: DbType.Int32, direction: ParameterDirection.Output);

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
                    _logger.LogInformation(
                        "Sincronizando CANTIDAD EXACTA ({Toneladas}) en MongoDB para embarcación {Id}", toneladas, id);

                    // ── Load ─────────────────────────────────────────────────
                    var filtro = Builders<ViajeDetalleMongo>.Filter.ElemMatch(
                        d => d.Etapas,
                        etapa => etapa.Barcazas != null && etapa.Barcazas.Any(b => b.Nombre == id));

                    var doc = _detailsCollection.Find(filtro).FirstOrDefault();
                    if (doc is null)
                    {
                        _logger.LogWarning("Mongo no encontró la embarcación '{Id}' para actualizar la carga.", id);
                    }
                    else
                    {
                        // ── Mutate ───────────────────────────────────────────
                        var barcazaTarget = doc.Etapas
                            .SelectMany(e => e.Barcazas ?? new List<BarcazaMongo>())
                            .FirstOrDefault(b => b.Nombre == id);

                        if (barcazaTarget is not null)
                        {
                            barcazaTarget.Cantidad = toneladas;

                            // ── Save ─────────────────────────────────────────
                            var filtroId = Builders<ViajeDetalleMongo>.Filter.Eq(d => d.Id, doc.Id);
                            _detailsCollection.ReplaceOne(filtroId, doc);

                            _logger.LogInformation("¡CQRS Exitoso! CANTIDAD actualizada en Mongo para embarcación {Id}.", id);
                            InvalidarCacheViajePorBuque(doc.VesselName);
                        }
                        else
                        {
                            _logger.LogWarning("La embarcación '{Id}' fue encontrada en el filtro pero no en la iteración LINQ.", id);
                        }
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
                parameters.Add("p_ID_BARCAZA", id);
                parameters.Add("p_TONELADAS",  toneladas);
                parameters.Add("p_RESULTADO",  dbType: DbType.Int32, direction: ParameterDirection.Output);

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
                    _logger.LogInformation(
                        "Sincronizando CANTIDAD EXACTA ({Toneladas}) en MongoDB para embarcación {Id}", toneladas, id);

                    // ── Load ─────────────────────────────────────────────────
                    var filtro = Builders<ViajeDetalleMongo>.Filter.ElemMatch(
                        d => d.Etapas,
                        etapa => etapa.Barcazas != null && etapa.Barcazas.Any(b => b.Nombre == id));

                    var doc = _detailsCollection.Find(filtro).FirstOrDefault();
                    if (doc is null)
                    {
                        _logger.LogWarning("Mongo no encontró la embarcación '{Id}' para actualizar la descarga.", id);
                    }
                    else
                    {
                        // ── Mutate ───────────────────────────────────────────
                        var barcazaTarget = doc.Etapas
                            .SelectMany(e => e.Barcazas ?? new List<BarcazaMongo>())
                            .FirstOrDefault(b => b.Nombre == id);

                        if (barcazaTarget is not null)
                        {
                            barcazaTarget.Cantidad = toneladas;

                            if (toneladas == 0)
                            {
                                barcazaTarget.Carga = "EN LASTRE";
                                _logger.LogInformation(
                                    "Embarcación {Id} quedó con 0tn. Estado de carga actualizado a EN LASTRE.", id);
                            }

                            // ── Save ─────────────────────────────────────────
                            var filtroId = Builders<ViajeDetalleMongo>.Filter.Eq(d => d.Id, doc.Id);
                            _detailsCollection.ReplaceOne(filtroId, doc);

                            _logger.LogInformation("¡CQRS Exitoso! CANTIDAD actualizada en Mongo para embarcación {Id}.", id);
                            InvalidarCacheViajePorBuque(doc.VesselName);
                        }
                        else
                        {
                            _logger.LogWarning("La embarcación '{Id}' fue encontrada en el filtro pero no en la iteración LINQ.", id);
                        }
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
                "Agregando carga BarcazaId={BarcazaId} (tipo: {Tipo}, {Tonelaje}tn) al buque '{Buque}'.",
                nuevaCarga.BarcazaId, nuevaCarga.Tipo, nuevaCarga.Tonelaje, nombreBuque);

            bool exitoOracle = false;

            try
            {
                using var connection = new OracleConnection(_oracleConnectionString);
                var parameters = new DynamicParameters();
                parameters.Add("p_BUQUE",     nombreBuque);
                // REFACTOR: BarcazaId es long (clave relacional del MDM de buques).
                parameters.Add("p_NOMBRE",    nuevaCarga.BarcazaId, dbType: DbType.Int64);
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
                    "Oracle no disponible en desarrollo. Bypass DEV activado para agregar BarcazaId={BarcazaId} al buque '{Buque}'. Error: {Message}",
                    nuevaCarga.BarcazaId, nombreBuque, ex.Message);

                exitoOracle = true; // Bypass de desarrollo
            }

            if (exitoOracle)
            {
                try
                {
                    _logger.LogInformation(
                        "Sincronizando nueva carga BarcazaId={BarcazaId} en MongoDB (details_mbpc) para buque '{Buque}'.",
                        nuevaCarga.BarcazaId, nombreBuque);

                    // Usamos BarcazaId.ToString() como identificador string temporal
                    // (compatibilidad con la propiedad Nombre de BarcazaMongo).
                    var nuevaBarcazaDoc = new BarcazaMongo
                    {
                        Nombre       = nuevaCarga.BarcazaId.ToString(),
                        Carga        = nuevaCarga.Tipo,
                        Cantidad     = nuevaCarga.Tonelaje,
                        Unidad       = "Tn",
                        MuelleActual = null
                    };

                    // ── Load ─────────────────────────────────────────────────
                    var filtro = Builders<ViajeDetalleMongo>.Filter.Eq(d => d.VesselName, nombreBuque);
                    var doc    = await _detailsCollection.Find(filtro).FirstOrDefaultAsync();

                    if (doc is not null)
                    {
                        // ── Mutate: inyectar en la primera etapa disponible ───
                        if (doc.Etapas.Count == 0)
                        {
                            _logger.LogWarning(
                                "El documento del buque '{Buque}' no tiene etapas. " +
                                "Se creará una etapa vacía para alojar la nueva barcaza.",
                                nombreBuque);
                            doc.Etapas.Add(new EtapaMongo { Barcazas = new List<BarcazaMongo>() });
                        }

                        var primeraEtapa = doc.Etapas.First();
                        primeraEtapa.Barcazas ??= new List<BarcazaMongo>();
                        primeraEtapa.Barcazas.Add(nuevaBarcazaDoc);

                        // ── Save ─────────────────────────────────────────────
                        var filtroId = Builders<ViajeDetalleMongo>.Filter.Eq(d => d.Id, doc.Id);
                        var result   = await _detailsCollection.ReplaceOneAsync(filtroId, doc);

                        if (result.ModifiedCount > 0)
                        {
                            _logger.LogInformation(
                                "¡CQRS Exitoso! Carga BarcazaId={BarcazaId} inyectada en MongoDB para buque '{Buque}'.",
                                nuevaCarga.BarcazaId, nombreBuque);
                            _cache.Remove($"{CacheKeyPrefixCargas}{nombreBuque}");
                        }
                        else
                        {
                            _logger.LogWarning(
                                "ReplaceOne no modificó ningún documento para el buque '{Buque}'.", nombreBuque);
                        }
                    }
                    else
                    {
                        _logger.LogWarning(
                            "No existe documento en MongoDB para el buque '{Buque}'. " +
                            "Se omite la sincronización Mongo; el próximo batch la resolverá.",
                            nombreBuque);
                    }
                }
                catch (Exception mongoEx)
                {
                    _logger.LogError(mongoEx,
                        "Fallo al sincronizar MongoDB (AgregarCarga) para BarcazaId={BarcazaId} del buque '{Buque}'.",
                        nuevaCarga.BarcazaId, nombreBuque);
                }
            }

            return exitoOracle;
        }

        // ── Helpers Privados ─────────────────────────────────────────────────

        /// <summary>
        /// Invalida la entrada de caché directamente por nombre de buque.
        /// Reemplaza al antiguo helper que hacía una segunda consulta a Mongo.
        /// </summary>
        private void InvalidarCacheViajePorBuque(string? vesselName)
        {
            if (string.IsNullOrWhiteSpace(vesselName))
                return;

            var cacheKey = $"{CacheKeyPrefixCargas}{vesselName}";
            _cache.Remove(cacheKey);
            _logger.LogInformation("Caché invalidada exitosamente para el viaje: {Viaje}", vesselName);
        }

        /// <summary>
        /// Helper de compatibilidad: localiza el documento dueño de la barcaza por ID
        /// e invalida la caché correspondiente. Se mantiene para contextos donde
        /// no disponemos del VesselName en el scope de llamada.
        /// </summary>
        private void InvalidarCacheViajePorBarcaza(string idBarcaza)
        {
            try
            {
                // Load-Mutate-Save: el filtro ya no usa strings mágicos de arrays embebidos.
                var filtro = Builders<ViajeDetalleMongo>.Filter.ElemMatch(
                    d => d.Etapas,
                    etapa => etapa.Barcazas != null && etapa.Barcazas.Any(b => b.Nombre == idBarcaza));

                var buqueDoc = _detailsCollection.Find(filtro).FirstOrDefault();

                if (buqueDoc != null && !string.IsNullOrWhiteSpace(buqueDoc.VesselName))
                    InvalidarCacheViajePorBuque(buqueDoc.VesselName);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "No se pudo limpiar la caché del viaje tras actualizar la barcaza {Id}", idBarcaza);
            }
        }
    }

    // ── OracleDynamicParameters — wrapper para RefCursor con Dapper ──────────

    /// <summary>
    /// Implementación de IDynamicParameters que permite registrar parámetros nativos
    /// de Oracle (incluido OracleDbType.RefCursor) y pasarlos a Dapper correctamente.
    /// </summary>
    public sealed class OracleDynamicParameters : SqlMapper.IDynamicParameters
    {
        private readonly List<OracleParameterInfo> _params = new();

        private sealed class OracleParameterInfo
        {
            public required string             Name      { get; init; }
            public          object?            Value     { get; init; }
            public required OracleDbType       DbType    { get; init; }
            public required ParameterDirection Direction { get; init; }
        }

        /// <summary>Agrega un parámetro con valor.</summary>
        public void Add(string name, object? value, OracleDbType dbType,
                        ParameterDirection direction = ParameterDirection.Input)
            => _params.Add(new OracleParameterInfo
            {
                Name      = name,
                Value     = value,
                DbType    = dbType,
                Direction = direction
            });

        /// <summary>Agrega un parámetro sin valor (e.g. RefCursor de salida).</summary>
        public void Add(string name, OracleDbType dbType, ParameterDirection direction)
            => _params.Add(new OracleParameterInfo
            {
                Name      = name,
                Value     = null,
                DbType    = dbType,
                Direction = direction
            });

        void SqlMapper.IDynamicParameters.AddParameters(IDbCommand command, SqlMapper.Identity identity)
        {
            if (command is not OracleCommand oracleCmd)
                throw new InvalidOperationException(
                    "OracleDynamicParameters solo puede usarse con OracleCommand.");

            foreach (var p in _params)
            {
                var oracleParam = oracleCmd.Parameters.Add(p.Name, p.DbType);
                oracleParam.Direction = p.Direction;

                if (p.Value is not null)
                    oracleParam.Value = p.Value;
            }
        }
    }
}
