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
using MongoDB.Bson;

namespace Mbpc.Api.Services
{
    public class CargaManagerService : ICargaService
    {
        // ── Dependencias de Datos ────────────────────────────────────────────
        private readonly IMongoCollection<ViajeDetalleMongo>  _detailsCollection;
        private readonly IMongoCollection<ViajePosicionMongo> _viajesCollection;
        private readonly string _oracleConnectionString;

        // ── Servicios de Dominio ─────────────────────────────────────────────
        private readonly ITipoCargaService _tipoCargaService;

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
            IMemoryCache                    cache,
            ITipoCargaService               tipoCargaService)
        {
            var database        = mongoClient.GetDatabase(mongoSettings.Value.DatabaseName);
            _detailsCollection  = database.GetCollection<ViajeDetalleMongo>(mongoSettings.Value.DetailsMbpcCollectionName);
            _viajesCollection   = database.GetCollection<ViajePosicionMongo>(mongoSettings.Value.LastMbpcCollectionName);
            _oracleConnectionString = oracleSettings.Value.ConnectionString;
            _logger           = logger;
            _env              = env;
            _cache            = cache;
            _tipoCargaService = tipoCargaService;
        }

        // ── LECTURA (Oracle Fallback + MongoDB + Caché) ──────────────────────

        public async Task<IEnumerable<CargaDto>> ObtenerCargasPorViaje(string parametroBusqueda)
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

            return await ObtenerCargasDesdeMongoDb(parametroBusqueda, cacheKey);
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
                    Tonelaje         = r.Tonelaje,
                    // Hito 5.7: "0" es la convención para Bodega en el schema Oracle.
                    // El campo Id NO se modifica para preservar la integridad de las mutaciones PUT/DELETE.
                    TipoUnidad       = r.Id?.ToString() == "0" ? "Bodega" : "Barcaza"
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

        // ── Resolución de Identidad compartida ───────────────────────────────

        /// <summary>
        /// Traduce cualquier identificador de viaje (ObjectId de last_mbpc o nombre de buque)
        /// al VesselName real que usan los documentos de details_mbpc.
        ///
        /// Contrato:
        ///   - Si <paramref name="parametro"/> es un ObjectId válido (24 hex), busca el documento
        ///     en _viajesCollection (last_mbpc) y retorna su VesselName.
        ///   - Si el ObjectId no existe o el VesselName está vacío, retorna el propio parámetro
        ///     como fallback (puede ser ya un nombre de buque literal).
        ///   - Si <paramref name="parametro"/> NO es un ObjectId, lo retorna sin modificar.
        ///
        /// Este helper es la ÚNICA fuente de resolución ObjectId → VesselName del servicio.
        /// Tanto ObtenerCargasDesdeMongoDb como EliminarCargaAsync lo deben usar para
        /// garantizar que ambos métodos "vean" el mismo documento en details_mbpc.
        /// </summary>
        private async Task<string> ResolverVesselNameAsync(string parametro)
        {
            if (parametro.Length == 24 && ObjectId.TryParse(parametro, out var objectId))
            {
                _logger.LogDebug(
                    "ResolverVesselNameAsync: '{Parametro}' es ObjectId. Buscando VesselName en last_mbpc...",
                    parametro);

                var filtroViaje = Builders<ViajePosicionMongo>.Filter.Eq("_id", objectId);
                var viaje       = await _viajesCollection.Find(filtroViaje).FirstOrDefaultAsync();

                if (viaje != null && !string.IsNullOrWhiteSpace(viaje.VesselName))
                {
                    _logger.LogDebug(
                        "ResolverVesselNameAsync: ObjectId '{ObjectId}' → VesselName '{VesselName}'.",
                        parametro, viaje.VesselName);
                    return viaje.VesselName;
                }

                _logger.LogWarning(
                    "ResolverVesselNameAsync: ObjectId '{ObjectId}' encontrado en last_mbpc pero VesselName vacío o nulo. " +
                    "Se retorna el parámetro original como fallback.",
                    parametro);
            }

            // No es ObjectId o no se encontró: retornar tal cual (ya es un nombre de buque)
            return parametro;
        }

        // ── Ruta MongoDB: nombre de buque u ObjectId ─────────────────────────

        private async Task<IEnumerable<CargaDto>> ObtenerCargasDesdeMongoDb(string parametroBusqueda, string cacheKey)
        {
            // ── Resolución de Identidad: ObjectId → VesselName (helper compartido) ──
            string nombreBuque = await ResolverVesselNameAsync(parametroBusqueda);

            _logger.LogDebug("Buscando en details_mbpc por VesselName: {NombreBuque}", nombreBuque);

            var filtroDetalles = Builders<ViajeDetalleMongo>.Filter.Eq(d => d.VesselName, nombreBuque);
            var detalles       = await _detailsCollection.Find(filtroDetalles).ToListAsync();

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

            // ── Hidratación: resolvemos TipoCarga para cada barcaza en paralelo ──
            var tareasTipoCarga = todasLasBarcazas
                .Select(b => b.MercaderiaId.HasValue && b.MercaderiaId.Value > 0
                    ? _tipoCargaService.ObtenerPorIdAsync(b.MercaderiaId.Value)
                    : Task.FromResult<TipoCargaDto?>(null))
                .ToList();

            var tiposCarga = await Task.WhenAll(tareasTipoCarga);

            var resultado = todasLasBarcazas
                .Select((b, index) =>
                {
                    var tipoCarga = tiposCarga[index];

                    string descripcion;
                    string nivelRiesgo;

                    // Hito 5.8: Descripción diferenciada por tipo de unidad.
                    // "0" es la convención de Bodega; cualquier otro Nombre es una Barcaza.
                    bool esBodega = b.Nombre == "0";

                    if (tipoCarga is not null)
                    {
                        descripcion = esBodega
                            ? $"{tipoCarga.Nombre} ({b.Cantidad} {b.Unidad})"
                            : $"{b.Nombre} (Mat. {b.Matricula ?? "S/N"}) - {tipoCarga.Nombre} ({b.Cantidad} {b.Unidad})";

                        nivelRiesgo = tipoCarga.EsPeligrosa ? "Alto" : "Bajo";
                    }
                    else
                    {
                        // Fallback seguro: sin tipo de carga resuelto usamos los datos crudos de Mongo
                        descripcion = esBodega
                            ? $"{b.Carga} ({b.Cantidad} {b.Unidad})"
                            : $"{b.Nombre} (Mat. {b.Matricula ?? "S/N"}) - {b.Carga} ({b.Cantidad} {b.Unidad})";

                        nivelRiesgo = "Bajo";

                        if (b.MercaderiaId.HasValue && b.MercaderiaId.Value > 0)
                        {
                            _logger.LogWarning(
                                "TipoCarga no encontrado para MercaderiaId={MercaderiaId} (barcaza '{Nombre}'). Usando valores por defecto.",
                                b.MercaderiaId.Value, b.Nombre);
                        }
                    }

                    return new CargaDto
                    {
                        Id               = b.Nombre ?? Guid.NewGuid().ToString(),
                        // Hito 5.8: ViajeId se llena con el parametroBusqueda (ObjectId real del viaje),
                        // NO con nombreBuque, para que el frontend pueda construir la ruta DELETE correcta.
                        ViajeId          = parametroBusqueda,
                        DescripcionLista = descripcion,
                        NivelRiesgo      = nivelRiesgo,
                        MuelleActual     = b.MuelleActual,
                        Tonelaje         = b.Cantidad,
                        // Hito 5.7: "0" es la convención para Bodega; cualquier otro Nombre es Barcaza.
                        // El campo Id NO se modifica para preservar la integridad de las mutaciones PUT/DELETE.
                        TipoUnidad       = esBodega ? "Bodega" : "Barcaza"
                    };
                })
                .ToList();

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
                        var barcazaTarget = doc.Etapas
                            .SelectMany(e => e.Barcazas ?? new List<BarcazaMongo>())
                            .FirstOrDefault(b => b.Nombre == id);

                        if (barcazaTarget is not null)
                        {
                            barcazaTarget.MuelleActual = zonaFondeo;

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
                    _logger.LogError(mongoEx, "Fallo al sincronizar MongoDB (fondeo) para la barcaza {Id}. Se sincronizará en el próximo batch.", id);
                }
            }

            return exitoOracle;
        }

        public bool CargarBarcaza(string id, double toneladas)
        {
            _logger.LogInformation("Registrando carga a {Toneladas}tn de embarcación {Id}", toneladas, id);
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
                    "Oracle no disponible en desarrollo. Simulando carga a {Toneladas}tn de {Id}. Error: {Message}",
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
            _logger.LogInformation("Registrando descarga a {Toneladas}tn de embarcación {Id}", toneladas, id);
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
                    "Oracle no disponible en desarrollo. Simulando descarga a {Toneladas}tn de {Id}. Error: {Message}",
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
                "Agregando carga BarcazaId={BarcazaId} (tipo: {Tipo}, {Tonelaje}tn, MercaderiaId={MercaderiaId}) al buque '{Buque}'.",
                nuevaCarga.BarcazaId, nuevaCarga.Tipo, nuevaCarga.Tonelaje, nuevaCarga.MercaderiaId, nombreBuque);

            bool exitoOracle = false;

            try
            {
                using var connection = new OracleConnection(_oracleConnectionString);
                var parameters = new DynamicParameters();
                parameters.Add("p_BUQUE",         nombreBuque);
                parameters.Add("p_NOMBRE",        nuevaCarga.BarcazaId, dbType: DbType.Int64);
                parameters.Add("p_TIPO",          nuevaCarga.Tipo);
                parameters.Add("p_TONELAJE",      nuevaCarga.Tonelaje);
                parameters.Add("p_TIPO_CARGA_ID", nuevaCarga.MercaderiaId, dbType: DbType.Int32);
                parameters.Add("p_RESULTADO",     dbType: DbType.Int32, direction: ParameterDirection.Output);

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

                    var nuevaBarcazaDoc = new BarcazaMongo
                    {
                        Nombre       = nuevaCarga.BarcazaId.ToString(),
                        Carga        = nuevaCarga.Tipo,
                        Cantidad     = nuevaCarga.Tonelaje,
                        Unidad       = "Tn",
                        MuelleActual = null,
                        MercaderiaId = nuevaCarga.MercaderiaId
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

                        InvalidarCacheViajePorBuque(doc.VesselName);

                        if (result.ModifiedCount > 0)
                        {
                            _logger.LogInformation(
                                "¡CQRS Exitoso! Carga BarcazaId={BarcazaId} inyectada en MongoDB para buque '{Buque}'.",
                                nuevaCarga.BarcazaId, nombreBuque);
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

        // ── NUEVOS MÉTODOS: Modificar y Eliminar ─────────────────────────────

        /// <summary>
        /// Modifica los datos de una barcaza existente.
        /// Oracle: SP_MODIFICAR_CARGA. MongoDB: Load-Mutate-Save sobre el array anidado Etapas → Barcazas.
        /// </summary>
        public async Task<bool> ModificarCargaAsync(string id, ModificarCargaDto dto)
        {
            _logger.LogInformation(
                "Modificando carga '{Id}' en ViajeId='{ViajeId}' → NuevoId={NuevoId}, Tipo={Tipo}, Tonelaje={Tonelaje}tn, MercaderiaId={MercaderiaId}.",
                id, dto.ViajeId, dto.BarcazaId, dto.Tipo, dto.Tonelaje, dto.MercaderiaId);

            bool exitoOracle = false;

            // ── Oracle ───────────────────────────────────────────────────────
            try
            {
                using var connection = new OracleConnection(_oracleConnectionString);
                var parameters = new DynamicParameters();
                parameters.Add("p_ID_BARCAZA_ACTUAL", id);
                parameters.Add("p_NUEVO_ID_BARCAZA",  dto.BarcazaId,    dbType: DbType.Int64);
                parameters.Add("p_TIPO",              dto.Tipo);
                parameters.Add("p_TONELAJE",          dto.Tonelaje);
                parameters.Add("p_TIPO_CARGA_ID",     dto.MercaderiaId, dbType: DbType.Int32);
                parameters.Add("p_RESULTADO",         dbType: DbType.Int32, direction: ParameterDirection.Output);

                await connection.ExecuteAsync(
                    "PKG_MBPC_CARGAS.SP_MODIFICAR_CARGA",
                    parameters,
                    commandType: CommandType.StoredProcedure);

                exitoOracle = parameters.Get<int>("p_RESULTADO") == 1;
            }
            catch (OracleException ex)
            {
                if (!_env.IsDevelopment())
                {
                    _logger.LogError(ex, "Error de Oracle en producción al modificar la carga '{Id}'.", id);
                    throw;
                }

                _logger.LogWarning(
                    "Oracle no disponible en desarrollo. Bypass DEV activado para modificar '{Id}'. Error: {Message}",
                    id, ex.Message);

                exitoOracle = true; // Bypass de desarrollo
            }

            // ── MongoDB Load-Mutate-Save ──────────────────────────────────────
            if (exitoOracle)
            {
                try
                {
                    _logger.LogInformation(
                        "Sincronizando modificación de carga '{Id}' en MongoDB.", id);

                    // ── Load ─────────────────────────────────────────────────
                    // FIX DE SCOPING: Filter.And ancla la búsqueda estrictamente
                    // al documento del viaje indicado por dto.ViajeId, evitando
                    // que una barcaza con igual nombre en otro viaje sea modificada
                    // por error (corrupción de datos cruzados).
                    var filtroViajeId   = Builders<ViajeDetalleMongo>.Filter.Eq(x => x.VesselName, dto.ViajeId);
                    var filtroElemMatch = Builders<ViajeDetalleMongo>.Filter.ElemMatch(
                        d => d.Etapas,
                        etapa => etapa.Barcazas != null && etapa.Barcazas.Any(b => b.Nombre == id));
                    var filtro = Builders<ViajeDetalleMongo>.Filter.And(filtroViajeId, filtroElemMatch);

                    var doc = await _detailsCollection.Find(filtro).FirstOrDefaultAsync();
                    if (doc is null)
                    {
                        _logger.LogWarning(
                            "Mongo no encontró la barcaza '{Id}' dentro del viaje '{ViajeId}' para modificar. " +
                            "Verificar que el ViajeId sea correcto y que la barcaza pertenezca a ese viaje.",
                            id, dto.ViajeId);
                    }
                    else
                    {
                        // ── Mutate ───────────────────────────────────────────
                        var barcazaTarget = doc.Etapas
                            .SelectMany(e => e.Barcazas ?? new List<BarcazaMongo>())
                            .FirstOrDefault(b => b.Nombre == id);

                        if (barcazaTarget is not null)
                        {
                            barcazaTarget.Nombre       = dto.BarcazaId.ToString();
                            barcazaTarget.Carga        = dto.Tipo;
                            barcazaTarget.Cantidad     = dto.Tonelaje;
                            barcazaTarget.MercaderiaId = dto.MercaderiaId;

                            // ── Save ─────────────────────────────────────────
                            var filtroId = Builders<ViajeDetalleMongo>.Filter.Eq(d => d.Id, doc.Id);
                            var result   = await _detailsCollection.ReplaceOneAsync(filtroId, doc);

                            if (result.ModifiedCount > 0)
                            {
                                _logger.LogInformation(
                                    "¡CQRS Exitoso! Carga '{Id}' modificada en MongoDB (nuevo nombre: '{NuevoNombre}').",
                                    id, barcazaTarget.Nombre);
                                InvalidarCacheViajePorBuque(doc.VesselName);
                            }
                            else
                            {
                                _logger.LogWarning(
                                    "ReplaceOne no modificó ningún documento al intentar modificar la barcaza '{Id}'.", id);
                            }
                        }
                        else
                        {
                            _logger.LogWarning(
                                "La barcaza '{Id}' fue encontrada en el filtro ElemMatch pero no en la iteración LINQ.", id);
                        }
                    }
                }
                catch (Exception mongoEx)
                {
                    _logger.LogError(mongoEx,
                        "Fallo al sincronizar MongoDB (ModificarCarga) para la barcaza '{Id}'. " +
                        "Se sincronizará en el próximo batch.", id);
                }
            }

            return exitoOracle;
        }

        /// <summary>
        /// Elimina una barcaza del manifiesto del viaje.
        /// Oracle: SP_ELIMINAR_CARGA. MongoDB: $pull atómico sobre el array anidado Etapas.$[].Barcazas.
        ///
        /// RESOLUCIÓN DE IDENTIDAD (Hito 5.8):
        ///   El frontend envía el _id de last_mbpc como viajeId. details_mbpc NO comparte ese _id;
        ///   se indexa por VesselName. Este método usa ResolverVesselNameAsync para traducir el
        ///   ObjectId al nombre real antes de filtrar details_mbpc, de forma idéntica a
        ///   ObtenerCargasDesdeMongoDb para que ambos métodos "vean" el mismo documento.
        ///
        /// SCOPING (Hito 5.8):
        ///   El filtro ancla la operación $pull estrictamente al documento del buque correcto,
        ///   previniendo que bodegas con Nombre="0" sean eliminadas del documento incorrecto.
        /// </summary>
        public async Task<bool> EliminarCargaAsync(string viajeId, string cargaId)
        {
            _logger.LogInformation("Eliminando carga '{CargaId}' del viaje '{ViajeId}'.", cargaId, viajeId);

            if (string.IsNullOrWhiteSpace(viajeId) || string.IsNullOrWhiteSpace(cargaId))
                return false;

            bool exitoOracle = false;

            // ── 1. Escritura en Oracle (Bypass en Desarrollo) ────────────────
            try
            {
                using var connection = new OracleConnection(_oracleConnectionString);
                var parameters = new DynamicParameters();
                parameters.Add("p_ID_BARCAZA", cargaId);
                parameters.Add("p_RESULTADO",  dbType: DbType.Int32, direction: ParameterDirection.Output);

                await connection.ExecuteAsync("PKG_MBPC_CARGAS.SP_ELIMINAR_CARGA", parameters, commandType: CommandType.StoredProcedure);
                exitoOracle = parameters.Get<int>("p_RESULTADO") == 1;
            }
            catch (OracleException)
            {
                if (!_env.IsDevelopment()) throw;
                _logger.LogWarning("Oracle Offline. Bypass DEV activado para eliminar '{CargaId}'.", cargaId);
                exitoOracle = true; 
            }

            // ── 2. Sincronización MongoDB ────────────────────────────────────
            if (exitoOracle)
            {
                try
                {
                    // PASO A: Traducir el viajeId (de last_mbpc) al VesselName que entiende details_mbpc
                    string nombreBuqueParaFiltrar = viajeId;

                    if (viajeId.Length == 24 && MongoDB.Bson.ObjectId.TryParse(viajeId, out var objectIdPosicion))
                    {
                        var viajePos = await _viajesCollection
                            .Find(Builders<ViajePosicionMongo>.Filter.Eq("_id", objectIdPosicion))
                            .FirstOrDefaultAsync();

                        if (viajePos != null && !string.IsNullOrWhiteSpace(viajePos.VesselName))
                        {
                            nombreBuqueParaFiltrar = viajePos.VesselName;
                        }
                    }

                    _logger.LogInformation("Sincronizando eliminación en details_mbpc para buque: '{Buque}' | Carga: '{CargaId}'", nombreBuqueParaFiltrar, cargaId);

                    // PASO B: Filtro por VesselName (que es la clave de unión)
                    var filtroDoc = Builders<ViajeDetalleMongo>.Filter.Eq(x => x.VesselName, nombreBuqueParaFiltrar);

                    // PASO C: Load-Mutate-Save (Evitamos el bug del $pull de MongoDB en arrays anidados)
                    var doc = await _detailsCollection.Find(filtroDoc).FirstOrDefaultAsync();

                    if (doc != null)
                    {
                        bool modificado = false;
                        
                        // Recorremos todas las etapas y borramos la carga que coincida
                        foreach (var etapa in doc.Etapas)
                        {
                            if (etapa.Barcazas != null)
                            {
                                int removidos = etapa.Barcazas.RemoveAll(b => b.Nombre == cargaId);
                                if (removidos > 0) modificado = true;
                            }
                        }

                        if (modificado)
                        {
                            // Reemplazamos el documento entero
                            await _detailsCollection.ReplaceOneAsync(filtroDoc, doc);
                            _logger.LogInformation("¡CQRS Exitoso! Carga '{CargaId}' eliminada de MongoDB (buque '{Buque}').", cargaId, nombreBuqueParaFiltrar);
                            
                            // Invalidamos caché por ambas llaves para que el frontend se entere al toque
                            _cache.Remove($"{CacheKeyPrefixCargas}{viajeId}");
                            _cache.Remove($"{CacheKeyPrefixCargas}{nombreBuqueParaFiltrar}");
                            return true;
                        }
                        else
                        {
                            _logger.LogWarning("No se encontró la carga '{CargaId}' en las etapas del buque '{Buque}'.", cargaId, nombreBuqueParaFiltrar);
                        }
                    }
                    else
                    {
                        _logger.LogWarning("No se encontró el documento con VesselName='{Buque}' en details_mbpc.", nombreBuqueParaFiltrar);
                    }
                    return false;
                }
                catch (Exception mongoEx)
                {
                    _logger.LogError(mongoEx, "Error al sincronizar eliminación en MongoDB para carga {CargaId}", cargaId);
                    return false;
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
            if (string.IsNullOrWhiteSpace(vesselName)) return;

            // 1. Invalidar la llave por nombre del buque
            _cache.Remove($"{CacheKeyPrefixCargas}{vesselName}");

            // 2. Buscar el Viaje ID asociado a ese buque e invalidar también esa llave (la que usa el frontend)
            try
            {
                var filtro   = Builders<ViajePosicionMongo>.Filter.Eq("VesselName", vesselName);
                var viajePos = _viajesCollection.Find(filtro).FirstOrDefault();

                if (viajePos != null)
                {
                    var bson = viajePos.ToBsonDocument();
                    if (bson.Contains("_id"))
                    {
                        var objectId = bson["_id"].ToString();
                        _cache.Remove($"{CacheKeyPrefixCargas}{objectId}");
                        _logger.LogInformation("Caché invalidada doble: Buque '{Buque}' y ObjectId '{Id}'.", vesselName, objectId);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Error al invalidar caché cruzada por ID: {Msg}", ex.Message);
            }
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
