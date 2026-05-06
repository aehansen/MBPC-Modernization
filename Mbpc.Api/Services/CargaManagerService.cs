using Dapper;
using Oracle.ManagedDataAccess.Client;
using Oracle.ManagedDataAccess.Types;
using MongoDB.Driver;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Caching.Memory;
using Mbpc.Api.Models.Config;
using Mbpc.Api.Models.Mongo;
using Mbpc.Api.DTOs;
using Mbpc.Api.DTOs.Convoy;
using System.Data;
using MongoDB.Bson;
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
        }

        // ── LECTURA (Oracle Fallback + MongoDB + Caché) ──────────────────────

        public async Task<IEnumerable<CargaDto>> ObtenerCargasPorViaje(string parametroBusqueda)
        {
            var cacheKey = $"{CacheKeyPrefixCargas}{parametroBusqueda}";
            if (_cache.TryGetValue(cacheKey, out IEnumerable<CargaDto>? cachedCargas) && cachedCargas != null)
            {
                _logger.LogDebug("CACHE HIT — Devolviendo cargas para parámetro: {Parametro}", parametroBusqueda);
                return cachedCargas;
            }

            if (long.TryParse(parametroBusqueda, out long travelId))
            {
                _logger.LogInformation(
                    "ORACLE FALLBACK — Parámetro '{Parametro}' es un TravelId numérico. Consultando legacy Oracle.",
                    parametroBusqueda);

                return await ObtenerCargasDesdeOracleAsync(travelId, cacheKey);
            }

            _logger.LogInformation(
                "CACHE MISS — Parámetro '{Parametro}' resuelve a MongoDB.",
                parametroBusqueda);

            return await ObtenerCargasDesdeMongoDb(parametroBusqueda, cacheKey);
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

        private async Task<IEnumerable<CargaDto>> ObtenerCargasDesdeMongoDb(string parametroBusqueda, string cacheKey)
        {
            var convoyService = _serviceProvider.GetRequiredService<IConvoyManagerService>();
            var buqueService  = _serviceProvider.GetRequiredService<IBuqueService>();
            var convoy = await convoyService.ObtenerConvoyPorViajeIdAsync(parametroBusqueda);
            var barcazasConvoy = convoy?.Barcazas?.ToList() ?? new List<BarcazaConvoyDto>();

            string nombreBuque = await ResolverVesselNameAsync(parametroBusqueda);

            _logger.LogDebug("Buscando en details_mbpc por VesselName: {NombreBuque}", nombreBuque);

            var filtroDetalles = Builders<ViajeDetalleMongo>.Filter.Eq(d => d.VesselName, nombreBuque);
            var detalles       = await _detailsCollection.Find(filtroDetalles).ToListAsync();

            var detalleConCargas = detalles.FirstOrDefault(d => d.Etapas != null && d.Etapas.Any());

            var ultimaEtapa = detalleConCargas?.Etapas?.LastOrDefault();
            var todasLasBarcazas = (ultimaEtapa?.Barcazas ?? new List<BarcazaMongo>())
                .Where(b => b is not null)
                .ToList();

            if (detalleConCargas == null && !barcazasConvoy.Any())
            {
                _logger.LogInformation("No se encontraron cargas ni convoy para: {NombreBuque}", nombreBuque);
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

                    var nombreCrudo = barcazasConvoy.FirstOrDefault(bc => bc.Id == b.Nombre)?.Nombre ?? b.Nombre;
                    var nombreReal  = ResolverNombreDisplay(nombreCrudo, b.Matricula);

                    string descripcion;
                    string nivelRiesgo = tipoCarga?.EsPeligrosa == true ? "Alto" : "Bajo";
                    string cargaNombre = tipoCarga?.Nombre ?? b.Carga ?? "Sin Descripción";

                    // Regla: si el nombreCrudo es un ID numérico puro, el catálogo no resolvió
                    // el nombre real → obligatoriamente usar matrícula para evitar el leak del ID.
                    // Se verifica solo nombreCrudo (no nombreReal) para ser resiliente a fallbacks
                    // del tipo "BZA-XXXXXXX" que tampoco son nombres legibles.
                    if (esBodega)
                    {
                        descripcion = $"Bodega - {cargaNombre} ({b.Cantidad} {b.Unidad})";
                    }
                    else if (long.TryParse(nombreCrudo, out _))
                    {
                        // El catálogo no resolvió el ID numérico → forzar matrícula como identificador
                        descripcion = $"Barcaza Mat. {b.Matricula ?? "S/N"} - {cargaNombre} ({b.Cantidad} {b.Unidad})";
                    }
                    else
                    {
                        descripcion = $"{nombreReal} - {cargaNombre} ({b.Cantidad} {b.Unidad})";
                    }

                    return new CargaDto
                    {
                        Id               = b.Nombre ?? Guid.NewGuid().ToString(),
                        ViajeId          = parametroBusqueda,
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
                        ViajeId          = parametroBusqueda,
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

                connection.Execute("PKG_MBPC_CARGAS.SP_AMARRAR", parameters, commandType: CommandType.StoredProcedure);
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
                    var filtro = Builders<ViajeDetalleMongo>.Filter.ElemMatch(
                        d => d.Etapas,
                        etapa => etapa.Barcazas != null && etapa.Barcazas.Any(b => b.Nombre == id));

                    var doc = _detailsCollection.Find(filtro).FirstOrDefault();
                    if (doc is not null)
                    {
                        var barcazaTarget = (doc.Etapas?.SelectMany(e => e.Barcazas ?? new List<BarcazaMongo>())
                            ?? Enumerable.Empty<BarcazaMongo>())
                            .FirstOrDefault(b => b.Nombre == id);

                        if (barcazaTarget is not null)
                        {
                            barcazaTarget.MuelleActual = nuevoMuelle;
                            var filtroId = Builders<ViajeDetalleMongo>.Filter.Eq(d => d.Id, doc.Id);
                            _detailsCollection.ReplaceOne(filtroId, doc);
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

                connection.Execute("PKG_MBPC_CARGAS.SP_FONDEAR", parameters, commandType: CommandType.StoredProcedure);
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
                    var filtro = Builders<ViajeDetalleMongo>.Filter.ElemMatch(
                        d => d.Etapas,
                        etapa => etapa.Barcazas != null && etapa.Barcazas.Any(b => b.Nombre == id));

                    var doc = _detailsCollection.Find(filtro).FirstOrDefault();
                    if (doc is not null)
                    {
                        var barcazaTarget = (doc.Etapas?.SelectMany(e => e.Barcazas ?? new List<BarcazaMongo>())
                            ?? Enumerable.Empty<BarcazaMongo>())
                            .FirstOrDefault(b => b.Nombre == id);

                        if (barcazaTarget is not null)
                        {
                            barcazaTarget.MuelleActual = zonaFondeo;
                            var filtroId = Builders<ViajeDetalleMongo>.Filter.Eq(d => d.Id, doc.Id);
                            _detailsCollection.ReplaceOne(filtroId, doc);
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

                connection.Execute("PKG_MBPC_CARGAS.SP_CARGAR", parameters, commandType: CommandType.StoredProcedure);
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
                    var filtro = Builders<ViajeDetalleMongo>.Filter.ElemMatch(
                        d => d.Etapas,
                        etapa => etapa.Barcazas != null && etapa.Barcazas.Any(b => b.Nombre == id));

                    var doc = _detailsCollection.Find(filtro).FirstOrDefault();
                    if (doc is not null)
                    {
                        var barcazaTarget = (doc.Etapas?.SelectMany(e => e.Barcazas ?? new List<BarcazaMongo>())
                            ?? Enumerable.Empty<BarcazaMongo>())
                            .FirstOrDefault(b => b.Nombre == id);

                        if (barcazaTarget is not null)
                        {
                            barcazaTarget.Cantidad = toneladas;
                            var filtroId = Builders<ViajeDetalleMongo>.Filter.Eq(d => d.Id, doc.Id);
                            _detailsCollection.ReplaceOne(filtroId, doc);
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

                connection.Execute("PKG_MBPC_CARGAS.SP_DESCARGAR", parameters, commandType: CommandType.StoredProcedure);
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
                    var filtro = Builders<ViajeDetalleMongo>.Filter.ElemMatch(
                        d => d.Etapas,
                        etapa => etapa.Barcazas != null && etapa.Barcazas.Any(b => b.Nombre == id));

                    var doc = _detailsCollection.Find(filtro).FirstOrDefault();
                    if (doc is not null)
                    {
                        var barcazaTarget = (doc.Etapas?.SelectMany(e => e.Barcazas ?? new List<BarcazaMongo>())
                            ?? Enumerable.Empty<BarcazaMongo>())
                            .FirstOrDefault(b => b.Nombre == id);

                        if (barcazaTarget is not null)
                        {
                            barcazaTarget.Cantidad = toneladas;
                            if (toneladas == 0) barcazaTarget.Carga = "EN LASTRE";

                            var filtroId = Builders<ViajeDetalleMongo>.Filter.Eq(d => d.Id, doc.Id);
                            _detailsCollection.ReplaceOne(filtroId, doc);
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

        public async Task<bool> AgregarCargaAsync(string nombreBuque, NuevaCargaDto nuevaCarga)
        {
            _logger.LogInformation(
                "Agregando carga BarcazaId={BarcazaId} al buque '{Buque}'.", nuevaCarga.BarcazaId, nombreBuque);
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

                await connection.ExecuteAsync("PKG_MBPC_CARGAS.SP_AGREGAR_CARGA", parameters, commandType: CommandType.StoredProcedure);
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
                    var nuevaBarcazaDoc = new BarcazaMongo
                    {
                        Nombre       = !string.IsNullOrWhiteSpace(nuevaCarga.BarcazaNombre) ? nuevaCarga.BarcazaNombre : nuevaCarga.BarcazaId.ToString(),
                        Carga        = nuevaCarga.Tipo,
                        Cantidad     = nuevaCarga.Tonelaje,
                        Unidad       = "Tn",
                        MuelleActual = null,
                        MercaderiaId = nuevaCarga.MercaderiaId
                    };

                    var filtro = Builders<ViajeDetalleMongo>.Filter.Eq(d => d.VesselName, nombreBuque);
                    var doc    = await _detailsCollection.Find(filtro).FirstOrDefaultAsync();

                    if (doc is not null)
                    {
                        doc.Etapas ??= new List<EtapaMongo>();
                        if (doc.Etapas.Count == 0) doc.Etapas.Add(new EtapaMongo { Barcazas = new List<BarcazaMongo>() });

                        var ultimaEtapa = doc.Etapas.Last();
                        ultimaEtapa.Barcazas ??= new List<BarcazaMongo>();
                        ultimaEtapa.Barcazas.Add(nuevaBarcazaDoc);

                        var filtroId = Builders<ViajeDetalleMongo>.Filter.Eq(d => d.Id, doc.Id);
                        await _detailsCollection.ReplaceOneAsync(filtroId, doc);
                        InvalidarCacheViajePorBuque(doc.VesselName);
                    }
                }
                catch (Exception mongoEx)
                {
                    _logger.LogError(mongoEx, "Fallo al sincronizar MongoDB (AgregarCarga) para el buque '{Buque}'.", nombreBuque);
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
                    FilterDefinition<ViajeDetalleMongo> filtroDoc;

                    if (!string.IsNullOrWhiteSpace(dto.ViajeId)
                        && dto.ViajeId.Length == 24
                        && MongoDB.Bson.ObjectId.TryParse(dto.ViajeId, out _))
                    {
                        filtroDoc = Builders<ViajeDetalleMongo>.Filter.Eq(x => x.Id, dto.ViajeId);
                    }
                    else
                    {
                        filtroDoc = Builders<ViajeDetalleMongo>.Filter.Eq(x => x.VesselName, dto.ViajeId);
                    }

                    var doc = await _detailsCollection.Find(filtroDoc).FirstOrDefaultAsync();

                    if (doc is not null)
                    {
                        var barcazaTarget = (doc.Etapas?.SelectMany(e => e.Barcazas ?? new List<BarcazaMongo>())
                            ?? Enumerable.Empty<BarcazaMongo>())
                            .FirstOrDefault(b => b.Nombre == id);

                        if (barcazaTarget is not null)
                        {
                            barcazaTarget.Carga        = dto.Tipo;
                            barcazaTarget.Cantidad     = dto.Tonelaje;
                            barcazaTarget.MercaderiaId = dto.MercaderiaId;

                            var filtroId = Builders<ViajeDetalleMongo>.Filter.Eq(d => d.Id, doc.Id);
                            await _detailsCollection.ReplaceOneAsync(filtroId, doc);

                            _logger.LogInformation(
                                "¡CQRS Exitoso! Barcaza '{BarcazaId}' modificada en MongoDB para el documento '{DocId}'.",
                                id, doc.Id);

                            InvalidarCacheViajePorBuque(doc.VesselName);
                            _cache.Remove($"{CacheKeyPrefixCargas}{dto.ViajeId}");
                        }
                        else
                        {
                            _logger.LogWarning(
                                "ModificarCargaAsync: Barcaza '{BarcazaId}' no encontrada en el documento '{DocId}' de MongoDB.",
                                id, doc.Id);
                        }
                    }
                    else
                    {
                        _logger.LogWarning(
                            "ModificarCargaAsync: No se encontró documento MongoDB para ViajeId/VesselName='{ViajeId}'.",
                            dto.ViajeId);
                    }
                }
                catch (Exception mongoEx)
                {
                    _logger.LogError(mongoEx, "Fallo al sincronizar MongoDB (ModificarCarga) para la barcaza '{Id}'.", id);
                }
            }

            return exitoOracle;
        }

        public async Task<bool> EliminarCargaAsync(string viajeId, string cargaId)
        {
            _logger.LogInformation("Eliminando carga '{CargaId}' del viaje '{ViajeId}'.", cargaId, viajeId);

            if (string.IsNullOrWhiteSpace(viajeId) || string.IsNullOrWhiteSpace(cargaId))
                return false;

            // ── FASE 1: Consultar MongoDB para obtener el documento y el IdViaje relacional ──
            // Es obligatorio hacer esto ANTES de llamar a Oracle para poder enviar p_ID_VIAJE
            // y evitar el bug de scoping donde Oracle borra la bodega (ID "0") de cualquier viaje.
            FilterDefinition<ViajeDetalleMongo> filtroDoc;

            if (viajeId.Length == 24 && MongoDB.Bson.ObjectId.TryParse(viajeId, out _))
            {
                filtroDoc = Builders<ViajeDetalleMongo>.Filter.Eq(x => x.Id, viajeId);
            }
            else
            {
                filtroDoc = Builders<ViajeDetalleMongo>.Filter.Eq(x => x.VesselName, viajeId);
            }

            ViajeDetalleMongo? doc;
            try
            {
                doc = await _detailsCollection.Find(filtroDoc).FirstOrDefaultAsync();
            }
            catch (Exception mongoEx)
            {
                _logger.LogError(mongoEx, "Error al consultar MongoDB antes de eliminar la carga '{CargaId}' del viaje '{ViajeId}'.", cargaId, viajeId);
                return false;
            }

            if (doc == null)
            {
                _logger.LogWarning(
                    "EliminarCargaAsync: No se encontró documento MongoDB para ViajeId='{ViajeId}'. Abortando eliminación.",
                    viajeId);
                return false;
            }

            long idViajeOracle = doc.IdViaje ?? 0L;

            // ── FASE 2: Llamar a Oracle con scoping doble (p_ID_VIAJE + p_ID_BARCAZA) ──
            bool exitoOracle = false;

            try
            {
                using var connection = new OracleConnection(_oracleConnectionString);
                var parameters = new DynamicParameters();
                parameters.Add("p_ID_VIAJE",   idViajeOracle, dbType: DbType.Int64);
                parameters.Add("p_ID_BARCAZA", cargaId);
                parameters.Add("p_RESULTADO",  dbType: DbType.Int32, direction: ParameterDirection.Output);

                await connection.ExecuteAsync("PKG_MBPC_CARGAS.SP_ELIMINAR_CARGA", parameters, commandType: CommandType.StoredProcedure);
                exitoOracle = parameters.Get<int>("p_RESULTADO") == 1;
            }
            catch (OracleException)
            {
                if (!_env.IsDevelopment()) throw;
                exitoOracle = true;
            }

            if (!exitoOracle) return false;

            // ── FASE 3: Mutar el documento en MongoDB (Load-Mutate-Save) ──
            try
            {
                bool modificado = false;

                if (doc.Etapas != null)
                {
                    foreach (var etapa in doc.Etapas)
                    {
                        if (etapa.Barcazas != null)
                        {
                            int removidos = etapa.Barcazas.RemoveAll(b => b.Nombre == cargaId);
                            if (removidos > 0) modificado = true;
                        }
                    }
                }

                if (modificado)
                {
                    await _detailsCollection.ReplaceOneAsync(filtroDoc, doc);
                    _logger.LogInformation(
                        "¡CQRS Exitoso! Carga '{CargaId}' eliminada con scoping (IdViaje Oracle={IdViaje}) del documento MongoDB '{DocId}'.",
                        cargaId, idViajeOracle, doc.Id);

                    _cache.Remove($"{CacheKeyPrefixCargas}{viajeId}");
                    _cache.Remove($"{CacheKeyPrefixCargas}{doc.VesselName}");
                    return true;
                }

                _logger.LogWarning(
                    "EliminarCargaAsync: Oracle reportó éxito pero la carga '{CargaId}' no fue encontrada en las etapas del documento MongoDB '{DocId}'.",
                    cargaId, doc.Id);
                return false;
            }
            catch (Exception mongoEx)
            {
                _logger.LogError(mongoEx, "Error al sincronizar eliminación en MongoDB para carga {CargaId}", cargaId);
                return false;
            }
        }

        public async Task<bool> SincronizarAmarreConvoyAsync(string viajeId)
        {
            try
            {
                string nombreBuque = await ResolverVesselNameAsync(viajeId);
                var filtro = Builders<ViajeDetalleMongo>.Filter.Eq(d => d.VesselName, nombreBuque);
                var doc = await _detailsCollection.Find(filtro).FirstOrDefaultAsync();

                if (doc is null) return false;

                var ultimaEtapa = doc.Etapas?.LastOrDefault();
                if (ultimaEtapa?.Barcazas == null || !ultimaEtapa.Barcazas.Any()) return true;

                bool modificado = false;
                foreach (var barcaza in ultimaEtapa.Barcazas)
                {
                    if (barcaza.MuelleActual != "Amarre de Convoy")
                    {
                        barcaza.MuelleActual = "Amarre de Convoy";
                        modificado = true;
                    }
                }

                if (modificado)
                {
                    var filtroId = Builders<ViajeDetalleMongo>.Filter.Eq(d => d.Id, doc.Id);
                    await _detailsCollection.ReplaceOneAsync(filtroId, doc);
                    InvalidarCacheViajePorBuque(doc.VesselName);
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al sincronizar amarre en cascada para el viaje {ViajeId}", viajeId);
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