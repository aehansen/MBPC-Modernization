using Dapper;
using Oracle.ManagedDataAccess.Client;
using Oracle.ManagedDataAccess.Types;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Caching.Memory;
using Mbpc.Api.Models.Config;
using Mbpc.Api.Models.Mongo;
using Mbpc.Api.DTOs;
using Mbpc.Api.DTOs.Convoy;
using System.Data;
using Microsoft.Extensions.DependencyInjection;

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
        private readonly IServiceProvider _serviceProvider;

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
            ITipoCargaService               tipoCargaService,
            IServiceProvider                serviceProvider)
        {
            var database        = mongoClient.GetDatabase(mongoSettings.Value.DatabaseName);
            _detailsCollection  = database.GetCollection<ViajeDetalleMongo>(mongoSettings.Value.DetailsMbpcCollectionName);
            _viajesCollection   = database.GetCollection<ViajePosicionMongo>(mongoSettings.Value.LastMbpcCollectionName);
            _oracleConnectionString = oracleSettings.Value.ConnectionString;
            _logger           = logger;
            _env              = env;
            _cache            = cache;
            _tipoCargaService = tipoCargaService;
            _serviceProvider  = serviceProvider;
            if (!BsonClassMap.IsClassMapRegistered(typeof(ViajeDetalleMongo)))
            BsonClassMap.RegisterClassMap<ViajeDetalleMongo>(cm => { cm.AutoMap(); cm.SetIgnoreExtraElements(true); });

        if (!BsonClassMap.IsClassMapRegistered(typeof(EtapaMongo)))
            BsonClassMap.RegisterClassMap<EtapaMongo>(cm => { cm.AutoMap(); cm.SetIgnoreExtraElements(true); });

        if (!BsonClassMap.IsClassMapRegistered(typeof(BarcazaMongo)))
            BsonClassMap.RegisterClassMap<BarcazaMongo>(cm => { cm.AutoMap(); cm.SetIgnoreExtraElements(true); });
        }

        // ── LECTURA (Oracle Fallback + MongoDB + Caché) ──────────────────────

        public async Task<IEnumerable<CargaDto>> ObtenerCargasPorViaje(string viajeId)
        {
            var cacheKey = $"{CacheKeyPrefixCargas}{viajeId}";
            if (_cache.TryGetValue(cacheKey, out IEnumerable<CargaDto>? cachedCargas) && cachedCargas != null)
            {
                _logger.LogDebug("CACHE HIT — Devolviendo cargas para viajeId: {ViajeId}", viajeId);
                return cachedCargas;
            }

            if (long.TryParse(viajeId, out long travelId))
            {
                _logger.LogInformation(
                    "ORACLE FALLBACK — ViajeId '{ViajeId}' es un TravelId numérico. Consultando legacy Oracle.",
                    viajeId);

                return await ObtenerCargasDesdeOracleAsync(travelId, cacheKey);
            }

            _logger.LogInformation(
                "CACHE MISS — ViajeId '{ViajeId}' resuelve a MongoDB.",
                viajeId);

            return await ObtenerCargasDesdeMongoDb(viajeId, cacheKey);
        }

        private async Task<IEnumerable<CargaDto>> ObtenerCargasDesdeOracleAsync(long travelId, string cacheKey)
        {
            if (_env.IsDevelopment())
            {
                _logger.LogWarning("[DEV BYPASS] Omitiendo conexión a Oracle en ObtenerCargasDesdeOracle para evitar timeouts de red. Retornando lista vacía.");
                return Enumerable.Empty<CargaDto>();
            }

            try
            {
                using var connection = new OracleConnection(_oracleConnectionString);
                await connection.OpenAsync();

                var etapaId = await connection.ExecuteScalarAsync<long?>(
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

                var spParams = new OracleDynamicParameters();
                spParams.Add("vEtapaId", etapaId.Value,              OracleDbType.Int64,     ParameterDirection.Input);
                spParams.Add("vCursor",  dbType: OracleDbType.RefCursor, direction: ParameterDirection.Output);

                var rawRows = await connection.QueryAsync<OracleCargaRow>(
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
                _logger.LogError(ex, "Error de Oracle en producción al obtener cargas para TravelId {TravelId}.", travelId);
                throw;
            }
        }

        // ── Hito 6.1: ResolverVesselNameAsync se conserva solo para SincronizarAmarreConvoyAsync ──
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

            return parametro;
        }

        /// <summary>
        /// Hito 6.1 — Búsqueda por Id (ObjectId) en lugar de VesselName.
        /// El parámetro recibido ES el viajeId; no se realiza ninguna resolución de nombre.
        /// </summary>
        private async Task<IEnumerable<CargaDto>> ObtenerCargasDesdeMongoDb(string viajeId, string cacheKey)
        {
            var convoyService = _serviceProvider.GetRequiredService<IConvoyManagerService>();
            var buqueService  = _serviceProvider.GetRequiredService<IBuqueService>();
            var convoy        = await convoyService.ObtenerConvoyPorViajeIdAsync(viajeId);
            var barcazasConvoy = convoy?.Barcazas?.ToList() ?? new List<BarcazaConvoyDto>();

            // Hito 6.1 — Filtro por Id, no por VesselName.
            _logger.LogDebug("Buscando en details_mbpc resolviendo Id: {ViajeId}", viajeId);

            using var scope = _serviceProvider.CreateScope();
            var viajeService = scope.ServiceProvider.GetRequiredService<IViajeService>();

            var (detalleConCargas, _) = await viajeService.GetViajeDetalleByIdAsync(viajeId);

            var ultimaEtapa      = detalleConCargas?.Etapas?.LastOrDefault();
            var todasLasBarcazas = (ultimaEtapa?.Barcazas ?? new List<BarcazaMongo>())
                .Where(b => b is not null)
                .ToList();

            if (detalleConCargas == null && !barcazasConvoy.Any())
            {
                _logger.LogInformation("No se encontraron cargas ni convoy para viajeId: {ViajeId}", viajeId);
                return Enumerable.Empty<CargaDto>();
            }

            // Paso 1: Recolectar TODOS los IDs numéricos únicos de ambas fuentes
            var barcazasSinDetalle = barcazasConvoy
                .Where(bc => !todasLasBarcazas.Any(b => b.Nombre == bc.Id))
                .ToList();

            var idsNumericos = todasLasBarcazas
                .Select(b => barcazasConvoy.FirstOrDefault(bc => bc.Id == b.Nombre)?.Nombre ?? b.Nombre)
                .Concat(barcazasSinDetalle.Select(bc => bc.Nombre ?? bc.Id))
                .Where(nombre => !string.IsNullOrEmpty(nombre) && long.TryParse(nombre, out _))
                .Select(nombre => long.Parse(nombre!))
                .Distinct()
                .ToList();

            // Paso 2: Una sola llamada al catálogo
            var catalogoBarcazas = idsNumericos.Any()
                ? await buqueService.ObtenerBuquesPorIdsAsync(idsNumericos)
                : new Dictionary<long, BuqueAutocompleteDto>();

            _logger.LogDebug(
                "Hito 5.9 — Batch lookup resolvió {Resueltos}/{Total} ID(s) numéricos en 1 round-trip.",
                catalogoBarcazas.Count, idsNumericos.Count);

            // Helper local: resuelve nombre display a partir del raw string
            string ResolverNombreDisplay(string? rawNombre, string? matriculaFallback)
            {
                if (string.IsNullOrWhiteSpace(rawNombre)) return "S/N";

                if (long.TryParse(rawNombre, out long id) && catalogoBarcazas.TryGetValue(id, out var info))
                {
                    var mat = !string.IsNullOrWhiteSpace(info.Matricula) ? info.Matricula : "S/N";
                    return $"{info.Nombre} ({mat})";
                }

                if (long.TryParse(rawNombre, out _))
                    return !string.IsNullOrWhiteSpace(matriculaFallback) ? matriculaFallback : $"BZA-{rawNombre}";

                return rawNombre;
            }

            // Paso 3: Construir DTOs
            var tareasTipoCarga = todasLasBarcazas
                .Select(b => b.MercaderiaId.HasValue && b.MercaderiaId.Value > 0
                    ? _tipoCargaService.ObtenerPorIdAsync(b.MercaderiaId.Value)
                    : Task.FromResult<TipoCargaDto?>(null))
                .ToList();

            var tiposCarga = await Task.WhenAll(tareasTipoCarga);

            var cargasConDetalle = todasLasBarcazas
                .Select((b, index) =>
                {
                    var tipoCarga = tiposCarga[index];
                    bool esBodega = b.Nombre == "0";

                    string descripcion;
                    string nivelRiesgo = tipoCarga?.EsPeligrosa == true ? "Alto" : "Bajo";
                    string cargaNombre = !string.IsNullOrWhiteSpace(b.Carga) && b.Carga != "A Definir" 
                        ? b.Carga 
                        : (tipoCarga?.Nombre ?? "A Definir");

                    if (esBodega)
                    {
                        descripcion = $"Bodega - {cargaNombre} ({b.Cantidad} {b.Unidad})";
                    }
                    else
                    {
                        var etiquetaBarcaza = string.IsNullOrWhiteSpace(b.Matricula) ? b.Nombre : b.Matricula;

                        if (long.TryParse(etiquetaBarcaza, out long barcazaIdNum)
                            && catalogoBarcazas.TryGetValue(barcazaIdNum, out var barcazaInfo))
                        {
                            etiquetaBarcaza = !string.IsNullOrWhiteSpace(barcazaInfo.Matricula)
                                ? barcazaInfo.Matricula
                                : barcazaInfo.Nombre;
                        }

                        descripcion =
                            $"[Barcaza] {etiquetaBarcaza} - {cargaNombre} ({b.Cantidad} {b.Unidad})";
                    }

                    return new CargaDto
                    {
                        Id               = b.Nombre ?? Guid.NewGuid().ToString(),
                        ViajeId          = viajeId,
                        DescripcionLista = descripcion,
                        NivelRiesgo      = nivelRiesgo,
                        MuelleActual     = b.MuelleActual,
                        Tonelaje         = b.Cantidad ?? 0d,
                        TipoUnidad       = esBodega ? "Bodega" : "Barcaza",
                        MercaderiaId     = b.MercaderiaId,
                        MercaderiaNombre = cargaNombre
                    };
                })
                .ToList();

            var cargasSinDetalle = barcazasSinDetalle
                .Select(bc =>
                {
                    var nombreReal = ResolverNombreDisplay(bc.Nombre ?? bc.Id, null);

                    return new CargaDto
                    {
                        Id               = bc.Id,
                        ViajeId          = viajeId,
                        DescripcionLista = $"{nombreReal} - A Definir",
                        Tonelaje         = 0,
                        NivelRiesgo      = "Bajo",
                        TipoUnidad       = "Barcaza",
                        MercaderiaId     = null,
                        MercaderiaNombre = null
                    };
                })
                .ToList();

            var resultado = cargasConDetalle.Concat(cargasSinDetalle).ToList();

            var cacheOptions = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5),
                SlidingExpiration               = TimeSpan.FromMinutes(2)
            };
            _cache.Set(cacheKey, resultado, cacheOptions);

            return resultado;
        }

        private sealed class OracleCargaRow
        {
            public long?   Id               { get; init; }
            public string? DescripcionLista { get; init; }
            public string? MuelleActual     { get; init; }
            public double  Tonelaje         { get; init; }
        }

        // ── ESCRITURA (Oracle + CQRS Mongo Load-Mutate-Save + Invalidación Caché) ──

        public async Task<bool> AmarrarBarcaza(string id, string nuevoMuelle, CancellationToken cancellationToken = default)
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

                await connection.ExecuteAsync(
                    "PKG_MBPC_CARGAS.SP_AMARRAR", parameters, commandType: CommandType.StoredProcedure);
                exitoOracle = parameters.Get<int>("p_RESULTADO") == 1;
            }
            catch (OracleException ex)
            {
                if (!_env.IsDevelopment()) throw;
                _logger.LogWarning(ex, "[DEV BYPASS] OracleException en AmarrarBarcaza para Id={Id}. Marcando éxito.", id);
                exitoOracle = true;
            }

            if (exitoOracle)
            {
                try
                {
                    var filtro = Builders<ViajeDetalleMongo>.Filter.ElemMatch(
                        d => d.Etapas,
                        etapa => etapa.Barcazas != null && etapa.Barcazas.Any(b => b.Nombre == id));

                    var doc = await _detailsCollection.Find(filtro).FirstOrDefaultAsync(cancellationToken);
                    if (doc is not null)
                    {
                        var barcazaTarget = (doc.Etapas?.SelectMany(e => e.Barcazas ?? new List<BarcazaMongo>())
                            ?? Enumerable.Empty<BarcazaMongo>())
                            .FirstOrDefault(b => b.Nombre == id);

                        if (barcazaTarget is not null)
                        {
                            barcazaTarget.MuelleActual = nuevoMuelle;
                            var filtroId = Builders<ViajeDetalleMongo>.Filter.Eq(d => d.Id, doc.Id);
                            await _detailsCollection.ReplaceOneAsync(filtroId, doc, cancellationToken: cancellationToken);
                            InvalidarCacheViajePorBuque(doc.VesselName);
                        }
                    }
                }
                catch (Exception mongoEx)
                {
                    _logger.LogError(mongoEx, "Fallo al sincronizar MongoDB (amarre) para la barcaza {Id}.", id);
                }
            }

            return exitoOracle;
        }

        public async Task<bool> FondearBarcaza(string id, string zonaFondeo, CancellationToken cancellationToken = default)
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

                await connection.ExecuteAsync(
                    "PKG_MBPC_CARGAS.SP_FONDEAR", parameters, commandType: CommandType.StoredProcedure);
                exitoOracle = parameters.Get<int>("p_RESULTADO") == 1;
            }
            catch (OracleException ex)
            {
                if (!_env.IsDevelopment()) throw;
                _logger.LogWarning(ex, "[DEV BYPASS] OracleException en FondearBarcaza para Id={Id}. Marcando éxito.", id);
                exitoOracle = true;
            }

            if (exitoOracle)
            {
                try
                {
                    var filtro = Builders<ViajeDetalleMongo>.Filter.ElemMatch(
                        d => d.Etapas,
                        etapa => etapa.Barcazas != null && etapa.Barcazas.Any(b => b.Nombre == id));

                    var doc = await _detailsCollection.Find(filtro).FirstOrDefaultAsync(cancellationToken);
                    if (doc is not null)
                    {
                        var barcazaTarget = (doc.Etapas?.SelectMany(e => e.Barcazas ?? new List<BarcazaMongo>())
                            ?? Enumerable.Empty<BarcazaMongo>())
                            .FirstOrDefault(b => b.Nombre == id);

                        if (barcazaTarget is not null)
                        {
                            barcazaTarget.MuelleActual = zonaFondeo;
                            var filtroId = Builders<ViajeDetalleMongo>.Filter.Eq(d => d.Id, doc.Id);
                            await _detailsCollection.ReplaceOneAsync(filtroId, doc, cancellationToken: cancellationToken);
                            InvalidarCacheViajePorBuque(doc.VesselName);
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

        public async Task<bool> CargarBarcaza(string id, double toneladas, CancellationToken cancellationToken = default)
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

                await connection.ExecuteAsync(
                    "PKG_MBPC_CARGAS.SP_CARGAR", parameters, commandType: CommandType.StoredProcedure);
                exitoOracle = parameters.Get<int>("p_RESULTADO") == 1;
            }
            catch (OracleException ex)
            {
                if (!_env.IsDevelopment()) throw;
                _logger.LogWarning(ex, "[DEV BYPASS] OracleException en CargarBarcaza para Id={Id}. Marcando éxito.", id);
                exitoOracle = true;
            }

            if (exitoOracle)
            {
                try
                {
                    var filtro = Builders<ViajeDetalleMongo>.Filter.ElemMatch(
                        d => d.Etapas,
                        etapa => etapa.Barcazas != null && etapa.Barcazas.Any(b => b.Nombre == id));

                    var doc = await _detailsCollection.Find(filtro).FirstOrDefaultAsync(cancellationToken);
                    if (doc is not null)
                    {
                        var barcazaTarget = (doc.Etapas?.SelectMany(e => e.Barcazas ?? new List<BarcazaMongo>())
                            ?? Enumerable.Empty<BarcazaMongo>())
                            .FirstOrDefault(b => b.Nombre == id);

                        if (barcazaTarget is not null)
                        {
                            barcazaTarget.Cantidad = toneladas;
                            var filtroId = Builders<ViajeDetalleMongo>.Filter.Eq(d => d.Id, doc.Id);
                            await _detailsCollection.ReplaceOneAsync(filtroId, doc, cancellationToken: cancellationToken);
                            InvalidarCacheViajePorBuque(doc.VesselName);
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

        public async Task<bool> DescargarBarcaza(string id, double toneladas, CancellationToken cancellationToken = default)
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

                await connection.ExecuteAsync(
                    "PKG_MBPC_CARGAS.SP_DESCARGAR", parameters, commandType: CommandType.StoredProcedure);
                exitoOracle = parameters.Get<int>("p_RESULTADO") == 1;
            }
            catch (OracleException ex)
            {
                if (!_env.IsDevelopment()) throw;
                _logger.LogWarning(ex, "[DEV BYPASS] OracleException en DescargarBarcaza para Id={Id}. Marcando éxito.", id);
                exitoOracle = true;
            }

            if (exitoOracle)
            {
                try
                {
                    var filtro = Builders<ViajeDetalleMongo>.Filter.ElemMatch(
                        d => d.Etapas,
                        etapa => etapa.Barcazas != null && etapa.Barcazas.Any(b => b.Nombre == id));

                    var doc = await _detailsCollection.Find(filtro).FirstOrDefaultAsync(cancellationToken);
                    if (doc is not null)
                    {
                        var barcazaTarget = (doc.Etapas?.SelectMany(e => e.Barcazas ?? new List<BarcazaMongo>())
                            ?? Enumerable.Empty<BarcazaMongo>())
                            .FirstOrDefault(b => b.Nombre == id);

                        if (barcazaTarget is not null)
                        {
                            barcazaTarget.Cantidad = toneladas;
                            if (toneladas == 0)
                            {
                                barcazaTarget.Carga = "EN LASTRE";
                                barcazaTarget.Descargada = true;
                            }

                            var filtroId = Builders<ViajeDetalleMongo>.Filter.Eq(d => d.Id, doc.Id);
                            await _detailsCollection.ReplaceOneAsync(filtroId, doc, cancellationToken: cancellationToken);
                            InvalidarCacheViajePorBuque(doc.VesselName);
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

        public async Task<bool> AgregarCargaAsync(string viajeId, NuevaCargaDto nuevaCarga)
        {
            _logger.LogInformation(
                "Agregando carga BarcazaId={BarcazaId} al viajeId='{ViajeId}'.", nuevaCarga.BarcazaId, viajeId);

            // ── Fase 0: Resolver detalle vía dominio (cruza posición ↔ details por TravelId/VesselName) ──
            using var scope = _serviceProvider.CreateScope();
            var viajeService = scope.ServiceProvider.GetRequiredService<IViajeService>();

            ViajeDetalleMongo? doc;
            long travelId;

            try
            {
                (doc, travelId) = await viajeService.GetViajeDetalleByIdAsync(viajeId);
            }
            catch (Exception mongoEx)
            {
                _logger.LogError(mongoEx,
                    "Error al consultar detalle operativo antes de agregar carga al viaje '{ViajeId}'.", viajeId);
                return false;
            }

            // ── Fase 0b: Hidratación resiliente — documento no existe ─────────────────
            if (doc is null)
            {
                _logger.LogWarning(
                    "AgregarCargaAsync: No se encontró documento MongoDB para ViajeId='{ViajeId}'. " +
                    "Iniciando hidratación desde Oracle.", viajeId);

                try
                {
                    if (travelId <= 0)
                    {
                        _logger.LogError(
                            "AgregarCargaAsync: IViajeService no pudo resolver un travelId válido " +
                            "para ViajeId='{ViajeId}'. Abortando.", viajeId);
                        return false;
                    }

                    FilterDefinition<ViajePosicionMongo> filtroPosicion =
                        viajeId.Length == 24 && ObjectId.TryParse(viajeId, out var objectId)
                            ? Builders<ViajePosicionMongo>.Filter.Eq("_id", objectId)
                            : Builders<ViajePosicionMongo>.Filter.Eq(v => v.VesselName, viajeId);

                    var posicion = await _viajesCollection.Find(filtroPosicion).FirstOrDefaultAsync();

                    var cargasOracle = (await ObtenerCargasPorViaje(travelId.ToString())).ToList();

                    _logger.LogInformation(
                        "AgregarCargaAsync: Hidratación — {Count} carga(s) encontrada(s) en Oracle " +
                        "para TravelId={TravelId}.", cargasOracle.Count, travelId);

                    var barcazasHidratadas = cargasOracle
                        .Select(c => new BarcazaMongo
                        {
                            Nombre       = c.Id,
                            Carga        = c.MercaderiaNombre ?? c.DescripcionLista,
                            Cantidad     = c.Tonelaje,
                            Unidad       = "Tn",
                            MuelleActual = c.MuelleActual,
                            MercaderiaId = c.MercaderiaId
                        })
                        .ToList();

                    doc = new ViajeDetalleMongo
                    {
                        IdViaje    = travelId,
                        VesselName = posicion?.VesselName,
                        Etapas = new List<EtapaMongo>
                        {
                            new EtapaMongo
                            {
                                EtapaId  = 1,
                                Barcazas = barcazasHidratadas
                            }
                        }
                    };

                    await _detailsCollection.InsertOneAsync(doc);

                    _logger.LogInformation(
                        "AgregarCargaAsync: Documento hidratado insertado en MongoDB con Id='{DocId}' " +
                        "(posición ViajeId='{ViajeId}', TravelId={TravelId}).",
                        doc.Id, viajeId, travelId);
                }
                catch (Exception hydrationEx)
                {
                    _logger.LogError(hydrationEx,
                        "AgregarCargaAsync: Fallo en la hidratación del documento para ViajeId='{ViajeId}'. " +
                        "Abortando.", viajeId);
                    return false;
                }
            }

            // El SP Oracle legado requiere el VesselName; lo leemos del documento ya cargado/hidratado.
            string nombreBuque = doc.VesselName ?? string.Empty;

            // ── Fase 1: Persistir en Oracle ───────────────────────────────────────────
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
                    "PKG_MBPC_CARGAS.SP_AGREGAR_CARGA", parameters, commandType: CommandType.StoredProcedure);
                exitoOracle = parameters.Get<int>("p_RESULTADO") == 1;
            }
            catch (OracleException)
            {
                if (!_env.IsDevelopment()) throw;
                exitoOracle = true;
            }

            // ── Fase 2: CQRS — mutar el documento en MongoDB ──────────────────────────
            if (exitoOracle)
            {
                try
                {
                    var tipoCarga = await _tipoCargaService.ObtenerPorIdAsync(nuevaCarga.MercaderiaId);

                    var nuevaBarcazaDoc = new BarcazaMongo
                    {
                        Nombre       = !string.IsNullOrWhiteSpace(nuevaCarga.BarcazaNombre)
                                        ? nuevaCarga.BarcazaNombre
                                        : nuevaCarga.BarcazaId.ToString(),
                        Carga        = tipoCarga?.Nombre ?? "A Definir",
                        Cantidad     = nuevaCarga.Tonelaje,
                        Unidad       = "Tn",
                        MuelleActual = null,
                        MercaderiaId = nuevaCarga.MercaderiaId
                    };

                    doc.Etapas ??= new List<EtapaMongo>();
                    if (doc.Etapas.Count == 0)
                        doc.Etapas.Add(new EtapaMongo { EtapaId = 1, Barcazas = new List<BarcazaMongo>() });

                    var ultimaEtapa = doc.Etapas.Last();
                    ultimaEtapa.Barcazas ??= new List<BarcazaMongo>();
                    ultimaEtapa.Barcazas.Add(nuevaBarcazaDoc);

                    var filtroId = Builders<ViajeDetalleMongo>.Filter.Eq(d => d.Id, doc.Id);
                    await _detailsCollection.ReplaceOneAsync(filtroId, doc);

                    _cache.Remove($"{CacheKeyPrefixCargas}{viajeId}");
                    _logger.LogInformation(
                        "¡CQRS Exitoso! Carga BarcazaId={BarcazaId} agregada al documento MongoDB '{DocId}' " +
                        "(ViajeId='{ViajeId}').", nuevaCarga.BarcazaId, doc.Id, viajeId);
                }
                catch (Exception mongoEx)
                {
                    _logger.LogError(mongoEx,
                        "Fallo al sincronizar MongoDB (AgregarCarga) para el viajeId '{ViajeId}'.", viajeId);
                }
            }

            return exitoOracle;
        }

        public async Task<bool> ModificarCargaAsync(string id, ModificarCargaDto dto)
        {
            bool exitoOracle = false;
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

                await connection.ExecuteAsync("PKG_MBPC_CARGAS.SP_MODIFICAR_CARGA", parameters, commandType: CommandType.StoredProcedure);
                exitoOracle = parameters.Get<int>("p_RESULTADO") == 1;
            }
            catch (OracleException)
            {
                if (!_env.IsDevelopment()) throw;
                exitoOracle = true;
            }

            if (exitoOracle)
            {
                try
                {
                    // FIX HITO 10.5 — Resolvemos IViajeService en tiempo de ejecución para evitar dependencia circular
                    using var scope = _serviceProvider.CreateScope();
                    var viajeService = scope.ServiceProvider.GetRequiredService<IViajeService>();
                    
                    // Delegamos en el servicio de viajes unificado
                    var (detalleEncontrado, _) = await viajeService.GetViajeDetalleByIdAsync(dto.ViajeId);
                    
                    if (detalleEncontrado == null)
                    {
                        _logger.LogWarning("ModificarCargaAsync: No se encontró detalle operativo para ViajeId='{ViajeId}'.", dto.ViajeId);
                        return false;
                    }

                    var doc = detalleEncontrado;

                    // Buscar la carga en la última etapa (scoping de etapa)
                    var ultimaEtapa = doc.Etapas?.LastOrDefault();
                    var barcazaTarget = ultimaEtapa?.Barcazas?.FirstOrDefault(b => b.Nombre == id);

                    if (barcazaTarget is not null)
                    {
                        var tipoCarga = await _tipoCargaService.ObtenerPorIdAsync(dto.MercaderiaId);
                        barcazaTarget.Carga        = tipoCarga?.Nombre ?? "A Definir";
                        barcazaTarget.Cantidad     = dto.Tonelaje;
                        barcazaTarget.MercaderiaId = dto.MercaderiaId;

                        var filtroId = Builders<ViajeDetalleMongo>.Filter.Eq(d => d.Id, doc.Id);
                        await _detailsCollection.ReplaceOneAsync(filtroId, doc);

                        _logger.LogInformation(
                            "¡CQRS Exitoso! Barcaza '{BarcazaId}' modificada en MongoDB para el documento '{DocId}' (ViajeId='{ViajeId}').",
                            id, doc.Id, dto.ViajeId);

                        _cache.Remove($"{CacheKeyPrefixCargas}{dto.ViajeId}");
                    }
                    else
                    {
                        _logger.LogWarning(
                            "ModificarCargaAsync: Barcaza '{BarcazaId}' no encontrada en la última etapa del documento MongoDB '{DocId}'.",
                            id, doc.Id);
                    }
                }
                catch (Exception mongoEx)
                {
                    _logger.LogError(mongoEx, "Fallo al sincronizar MongoDB (ModificarCarga) para la barcaza '{Id}'.", id);
                }
            }

            return exitoOracle;
        }

        private async Task<bool> EliminarCargaDesdeMongoDb(string viajeId, string cargaId)
        {
            using var scope = _serviceProvider.CreateScope();
            var viajeService = scope.ServiceProvider.GetRequiredService<IViajeService>();

            ViajeDetalleMongo? doc;
            try
            {
                (doc, _) = await viajeService.GetViajeDetalleByIdAsync(viajeId);
            }
            catch (Exception mongoEx)
            {
                _logger.LogError(
                    mongoEx,
                    "EliminarCargaDesdeMongoDb: Error al consultar MongoDB para ViajeId='{ViajeId}', CargaId='{CargaId}'.",
                    viajeId, cargaId);
                return false;
            }

            if (doc is null)
            {
                _logger.LogWarning(
                    "EliminarCargaDesdeMongoDb: No se encontró documento con carga '{CargaId}' en viaje '{ViajeId}'.",
                    cargaId, viajeId);
                return false;
            }

            var ultimaEtapa = doc.Etapas?.LastOrDefault();
            if (ultimaEtapa?.Barcazas is null)
            {
                _logger.LogWarning(
                    "EliminarCargaDesdeMongoDb: El documento '{DocId}' no tiene barcazas en la última etapa.",
                    doc.Id);
                return false;
            }

            int removidos = ultimaEtapa.Barcazas.RemoveAll(b => b.Nombre == cargaId);
            if (removidos == 0)
            {
                _logger.LogWarning(
                    "EliminarCargaDesdeMongoDb: La carga '{CargaId}' no estaba en la última etapa del documento '{DocId}'.",
                    cargaId, doc.Id);
                return false;
            }

            var filtroPersistir = Builders<ViajeDetalleMongo>.Filter.Eq(x => x.Id, doc.Id);
            var replaceResult   = await _detailsCollection.ReplaceOneAsync(filtroPersistir, doc);

            if (replaceResult.ModifiedCount == 0 && replaceResult.MatchedCount == 0)
            {
                _logger.LogWarning(
                    "EliminarCargaDesdeMongoDb: ReplaceOne no persistió la eliminación de '{CargaId}' en '{DocId}'.",
                    cargaId, doc.Id);
                return false;
            }

            _logger.LogInformation(
                "EliminarCargaDesdeMongoDb: Carga '{CargaId}' eliminada del documento '{DocId}' (ViajeId='{ViajeId}').",
                cargaId, doc.Id, viajeId);

            return true;
        }

        public async Task<bool> EliminarCargaAsync(string viajeId, string cargaId)
        {
            _logger.LogInformation("Eliminando carga '{CargaId}' del viaje '{ViajeId}'.", cargaId, viajeId);

            if (string.IsNullOrWhiteSpace(viajeId) || string.IsNullOrWhiteSpace(cargaId))
                return false;

            using var scope = _serviceProvider.CreateScope();
            var viajeService = scope.ServiceProvider.GetRequiredService<IViajeService>();

            ViajeDetalleMongo? doc;
            try
            {
                (doc, _) = await viajeService.GetViajeDetalleByIdAsync(viajeId);
            }
            catch (Exception mongoEx)
            {
                _logger.LogError(
                    mongoEx,
                    "EliminarCargaAsync: Error al consultar MongoDB antes de eliminar la carga '{CargaId}' del viaje '{ViajeId}'.",
                    cargaId, viajeId);
                return false;
            }

            if (doc is null)
            {
                _logger.LogWarning(
                    "EliminarCargaAsync: No se encontró documento MongoDB para ViajeId='{ViajeId}'. Abortando eliminación.",
                    viajeId);
                return false;
            }

            long idViajeOracle = doc.IdViaje ?? 0L;

            bool exitoOracle = false;

            try
            {
                using var connection = new OracleConnection(_oracleConnectionString);
                var parameters = new DynamicParameters();
                parameters.Add("p_ID_VIAJE",   idViajeOracle, dbType: DbType.Int64);
                parameters.Add("p_ID_BARCAZA", cargaId);
                parameters.Add("p_RESULTADO",  dbType: DbType.Int32, direction: ParameterDirection.Output);

                await connection.ExecuteAsync(
                    "PKG_MBPC_CARGAS.SP_ELIMINAR_CARGA",
                    parameters,
                    commandType: CommandType.StoredProcedure);
                exitoOracle = parameters.Get<int>("p_RESULTADO") == 1;
            }
            catch (OracleException)
            {
                if (!_env.IsDevelopment()) throw;
                exitoOracle = true;
            }

            if (!exitoOracle)
                return false;

            try
            {
                var exitoMongo = await EliminarCargaDesdeMongoDb(viajeId, cargaId);

                if (exitoMongo)
                {
                    _logger.LogInformation(
                        "¡CQRS Exitoso! Carga '{CargaId}' eliminada (IdViaje Oracle={IdViaje}) del documento MongoDB '{DocId}' (ViajeId='{ViajeId}').",
                        cargaId, idViajeOracle, doc.Id, viajeId);

                    _cache.Remove($"{CacheKeyPrefixCargas}{viajeId}");
                    return true;
                }

                _logger.LogWarning(
                    "EliminarCargaAsync: Oracle reportó éxito pero MongoDB no eliminó la carga '{CargaId}' del viaje '{ViajeId}'.",
                    cargaId, viajeId);
                return false;
            }
            catch (Exception mongoEx)
            {
                _logger.LogError(
                    mongoEx,
                    "Error al sincronizar eliminación en MongoDB para carga '{CargaId}' del viaje '{ViajeId}'.",
                    cargaId, viajeId);
                return false;
            }
        }

        public async Task<bool> SincronizarAmarreConvoyAsync(string viajeId)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var viajeService = scope.ServiceProvider.GetRequiredService<IViajeService>();
                var (doc, _)     = await viajeService.GetViajeDetalleByIdAsync(viajeId);

                if (doc is null)
                {
                    var filtroId = Builders<ViajeDetalleMongo>.Filter.Eq(d => d.Id, viajeId);
                    doc = await _detailsCollection.Find(filtroId).FirstOrDefaultAsync();
                }

                if (doc is null)
                {
                    _logger.LogWarning(
                        "SincronizarAmarreConvoyAsync: No se encontró documento MongoDB para ViajeId='{ViajeId}'. " +
                        "No se actualiza el estado de las barcazas.", viajeId);
                    return false;
                }

                var ultimaEtapa = doc.Etapas?.LastOrDefault();
                if (ultimaEtapa?.Barcazas == null || !ultimaEtapa.Barcazas.Any())
                {
                    _logger.LogDebug(
                        "SincronizarAmarreConvoyAsync: El viaje '{ViajeId}' no tiene barcazas en la etapa activa. " +
                        "No hay nada que sincronizar.", viajeId);
                    return true;
                }

                var muelleDestino = doc.Destination;
                if (string.IsNullOrWhiteSpace(muelleDestino))
                {
                    FilterDefinition<ViajePosicionMongo> filtroPos;
                    if (viajeId.Length == 24 && ObjectId.TryParse(viajeId, out var objectId))
                        filtroPos = Builders<ViajePosicionMongo>.Filter.Eq("_id", objectId);
                    else
                    {
                        var vesselName = await ResolverVesselNameAsync(viajeId);
                        filtroPos = Builders<ViajePosicionMongo>.Filter.Eq(v => v.VesselName, vesselName);
                    }

                    var posicion = await _viajesCollection.Find(filtroPos).FirstOrDefaultAsync();
                    muelleDestino = posicion?.Destination ?? doc.Origin;
                }

                if (string.IsNullOrWhiteSpace(muelleDestino))
                {
                    _logger.LogWarning(
                        "SincronizarAmarreConvoyAsync: No se pudo determinar muelle destino para el viaje '{ViajeId}'. " +
                        "Las barcazas sin MuelleActual no se actualizan.", viajeId);
                    return true;
                }

                bool modificado = false;
                foreach (var barcaza in ultimaEtapa.Barcazas)
                {
                    // Al amarrar el remolcador, las barcazas en tránsito pasan a Amarrada:
                    // se asigna el muelle de destino si aún no tienen uno.
                    if (string.IsNullOrWhiteSpace(barcaza.MuelleActual))
                    {
                        barcaza.MuelleActual = muelleDestino;
                        modificado = true;
                    }
                }

                if (modificado)
                {
                    var filtroId = Builders<ViajeDetalleMongo>.Filter.Eq(d => d.Id, doc.Id);
                    await _detailsCollection.ReplaceOneAsync(filtroId, doc);

                    _cache.Remove($"{CacheKeyPrefixCargas}{viajeId}");
                    InvalidarCacheViajePorBuque(doc.VesselName);

                    _logger.LogInformation(
                        "SincronizarAmarreConvoyAsync: Barcazas del viaje '{ViajeId}' actualizadas a Amarrada (MuelleActual → '{Muelle}').",
                        viajeId, muelleDestino);
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error al sincronizar amarre en cascada para el viaje '{ViajeId}'.", viajeId);
                return false;
            }
        }

        public async Task<bool> SincronizarZarpeConvoyAsync(string viajeId)
        {
            try
            {
                var viajeService = _serviceProvider.GetRequiredService<IViajeService>();
                var (doc, _)     = await viajeService.GetViajeDetalleByIdAsync(viajeId);

                if (doc is null)
                {
                    var filtroId = Builders<ViajeDetalleMongo>.Filter.Eq(d => d.Id, viajeId);
                    doc = await _detailsCollection.Find(filtroId).FirstOrDefaultAsync();
                }

                if (doc is null)
                {
                    _logger.LogWarning(
                        "SincronizarZarpeConvoyAsync: No se encontró documento MongoDB para ViajeId='{ViajeId}'. " +
                        "No se actualiza el estado de las barcazas.", viajeId);
                    return false;
                }

                var ultimaEtapa = doc.Etapas?.LastOrDefault();
                if (ultimaEtapa?.Barcazas == null || !ultimaEtapa.Barcazas.Any())
                {
                    _logger.LogDebug(
                        "SincronizarZarpeConvoyAsync: El viaje '{ViajeId}' no tiene barcazas en la etapa activa. " +
                        "No hay nada que sincronizar.", viajeId);
                    return true;
                }

                bool modificado = false;
                foreach (var barcaza in ultimaEtapa.Barcazas)
                {
                    // Al zarpar, las barcazas pasan a EnTransito:
                    // se limpia el muelle actual (ya no están amarradas).
                    if (!string.IsNullOrEmpty(barcaza.MuelleActual))
                    {
                        barcaza.MuelleActual = null;
                        modificado = true;
                    }
                }

                if (modificado)
                {
                    var filtroId = Builders<ViajeDetalleMongo>.Filter.Eq(d => d.Id, doc.Id);
                    await _detailsCollection.ReplaceOneAsync(filtroId, doc);

                    _cache.Remove($"{CacheKeyPrefixCargas}{viajeId}");
                    InvalidarCacheViajePorBuque(doc.VesselName);

                    _logger.LogInformation(
                        "SincronizarZarpeConvoyAsync: Barcazas del viaje '{ViajeId}' actualizadas a EnTransito (MuelleActual → null).",
                        viajeId);
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error al sincronizar zarpe en cascada para el viaje '{ViajeId}'.", viajeId);
                return false;
            }
        }

        private void InvalidarCacheViajePorBuque(string? vesselName)
        {
            if (string.IsNullOrWhiteSpace(vesselName)) return;

            _cache.Remove($"{CacheKeyPrefixCargas}{vesselName}");

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
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Error al invalidar caché cruzada por ID: {Msg}", ex.Message);
            }
        }

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

        public void Add(string name, object? value, OracleDbType dbType,
                        ParameterDirection direction = ParameterDirection.Input)
            => _params.Add(new OracleParameterInfo
            {
                Name      = name,
                Value     = value,
                DbType    = dbType,
                Direction = direction
            });

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
