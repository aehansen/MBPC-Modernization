// Mbpc.Api/Services/ConvoyManagerService.cs

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Mbpc.Api.DTOs;
using Mbpc.Api.DTOs.Convoy;
using Mbpc.Api.Models.Mongo;
using Mbpc.Api.Models.Config;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Caching.Memory;
using MongoDB.Driver;
using Oracle.ManagedDataAccess.Client;

namespace Mbpc.Api.Services;

public sealed class ConvoyManagerService : IConvoyManagerService
{
    private readonly IViajeService                 _viajeService;
    private readonly ICargaService                 _cargaService;
    private readonly IServiceProvider              _serviceProvider;
    private readonly IHostEnvironment              _env;
    private readonly ILogger<ConvoyManagerService> _logger;
    private readonly IMemoryCache                  _cache;

    private readonly IMongoCollection<ViajeDetalleMongo>  _detallesCollection;
    private readonly IMongoCollection<ViajePosicionMongo> _viajesCollection;   // BUG 2 FIX
    private readonly string _oracleConnectionString;

    public ConvoyManagerService(
        IViajeService                  viajeService,
        ICargaService                  cargaService,
        IServiceProvider               serviceProvider,
        IMongoClient                   mongoClient,
        IOptions<MongoDbSettings>      mongoSettings,
        IOptions<OracleDbSettings>     oracleSettings,
        IHostEnvironment               env,
        ILogger<ConvoyManagerService>  logger,
        IMemoryCache                   cache)
    {
        _viajeService    = viajeService    ?? throw new ArgumentNullException(nameof(viajeService));
        _cargaService    = cargaService    ?? throw new ArgumentNullException(nameof(cargaService));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _env             = env             ?? throw new ArgumentNullException(nameof(env));
        _logger          = logger          ?? throw new ArgumentNullException(nameof(logger));
        _cache           = cache           ?? throw new ArgumentNullException(nameof(cache));

        ArgumentNullException.ThrowIfNull(mongoClient);
        ArgumentNullException.ThrowIfNull(mongoSettings?.Value);
        ArgumentNullException.ThrowIfNull(oracleSettings?.Value);

        var database = mongoClient.GetDatabase(mongoSettings.Value.DatabaseName);
        _detallesCollection = database.GetCollection<ViajeDetalleMongo>(mongoSettings.Value.DetailsMbpcCollectionName);
        _viajesCollection   = database.GetCollection<ViajePosicionMongo>(mongoSettings.Value.LastMbpcCollectionName); // BUG 2 FIX

        _oracleConnectionString = oracleSettings.Value.ConnectionString
                                  ?? throw new ArgumentException("Oracle connection string cannot be null.");
    }

    public async Task<ConvoyDto?> ObtenerConvoyPorViajeIdAsync(
        string            viajeId,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(viajeId);

        var detalle = await _detallesCollection
            .Find(Builders<ViajeDetalleMongo>.Filter.Eq(x => x.Id, viajeId))
            .FirstOrDefaultAsync(ct);

        long travelId = detalle?.IdViaje ?? 0;

        if (detalle is null)
        {
            var (detalleCache, tId) = await _viajeService.GetViajeDetalleByIdAsync(viajeId, ct);
            detalle = detalleCache;
            travelId = tId;
        }

        if (detalle is null && travelId == 0)
        {
            _logger.LogWarning("ObtenerConvoyPorViajeIdAsync: No se encontró detalle para ViajeId={ViajeId}.", viajeId);
            return null;
        }

        var barcazas   = await ResolverBarcazasAsync(viajeId, travelId, detalle);

        string? estadoNavegacion = null;
        try
        {
            // 1. Alineación exacta con ViajeManagerService.BuildFiltroViaje
            FilterDefinition<ViajePosicionMongo> filtroPosicion;
            if (viajeId.Length == 24 && MongoDB.Bson.ObjectId.TryParse(viajeId, out var objectId))
            {
                filtroPosicion = Builders<ViajePosicionMongo>.Filter.Eq("_id", objectId);
            }
            else
            {
                filtroPosicion = Builders<ViajePosicionMongo>.Filter.Eq(v => v.VesselName, viajeId);
            }

            var posicion = await _viajesCollection.Find(filtroPosicion).FirstOrDefaultAsync(ct);

            // 2. Fallback robusto por si el viajeId pertenecía a la colección de detalles
            if (posicion == null && !string.IsNullOrWhiteSpace(detalle?.VesselName))
            {
                _logger.LogInformation(
                    "ObtenerConvoy: No se halló posición por ID. Buscando por VesselName='{VesselName}'",
                    detalle.VesselName);
                filtroPosicion = Builders<ViajePosicionMongo>.Filter.Eq(v => v.VesselName, detalle.VesselName);
                posicion = await _viajesCollection.Find(filtroPosicion).FirstOrDefaultAsync(ct);
            }

            if (posicion != null)
            {
                estadoNavegacion = posicion.NavegationStatusDesc;
                _logger.LogInformation(
                    "ObtenerConvoy: Estado resuelto exitosamente a '{Estado}' para ViajeId={ViajeId}.",
                    estadoNavegacion, viajeId);
            }
            else
            {
                _logger.LogWarning(
                    "ObtenerConvoy: 'posicion' es NULL tras agotar búsquedas para ViajeId={ViajeId}.",
                    viajeId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "ObtenerConvoy: Excepción al intentar resolver estado de navegación para ViajeId={ViajeId}.",
                viajeId);
        }

        var remolcador = MapearRemolcador(detalle, estadoNavegacion);

        return new ConvoyDto
        {
            ViajeId     = detalle?.Id ?? viajeId,
            NombreBuque = detalle?.VesselName ?? "Sin nombre",
            Remolcador  = remolcador,
            Barcazas    = barcazas.AsReadOnly()
        };
    }

    public async Task AmarrarBarcazaAsync(
        string                barcazaId,
        AmarrarBarcazaRequest request,
        CancellationToken     ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(barcazaId);

        if (string.IsNullOrWhiteSpace(request?.NuevoMuelle))
            throw new ValidationException(
                "El muelle de destino es requerido para amarrar la barcaza.");

        _logger.LogInformation(
            "Iniciando amarre de barcaza {BarcazaId} en muelle {Muelle}.",
            barcazaId, request.NuevoMuelle);

        var exito = await _cargaService.AmarrarBarcaza(barcazaId, request.NuevoMuelle, ct);
        if (!exito)
            throw new InvalidOperationException(
                "El sistema legacy rechazó la operación de amarre.");
    }

    public async Task FondearBarcazaAsync(
        string               barcazaId,
        FondearBarcazaRequest request,
        CancellationToken    ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(barcazaId);

        if (string.IsNullOrWhiteSpace(request?.ZonaFondeo))
            throw new ValidationException("La zona de fondeo es requerida.");

        _logger.LogInformation(
            "Iniciando fondeo de barcaza {BarcazaId} en zona {Zona}.",
            barcazaId, request.ZonaFondeo);

        var exito = await _cargaService.FondearBarcaza(barcazaId, request.ZonaFondeo, ct);
        if (!exito)
            throw new InvalidOperationException(
                "El sistema legacy rechazó la operación de fondeo.");
    }

    public async Task<bool> AdjuntarBarcazasAsync(
        string                  viajeId,
        AdjuntarBarcazasRequest request,
        CancellationToken       ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(viajeId);
        ArgumentNullException.ThrowIfNull(request);

        if (request.BarcazasIds is null || request.BarcazasIds.Count == 0)
            throw new ValidationException("Debe especificar al menos una barcaza para adjuntar.");

        if (string.IsNullOrWhiteSpace(request.Ubicacion))
            throw new ValidationException("La ubicación es requerida para adjuntar barcazas.");

        _logger.LogInformation(
            "AdjuntarBarcazasAsync: ViajeId={ViajeId} | Barcazas=[{Ids}] | Ubicacion={Ubicacion}.",
            viajeId, string.Join(',', request.BarcazasIds), request.Ubicacion);

        // 🔥 EL ARREGLO DEL SPLIT-BRAIN: Buscamos a través del servicio de viajes unificado
        var (detalle, travelId) = await _viajeService.GetViajeDetalleByIdAsync(viajeId, ct);

        if (detalle is null)
        {
            // Ya no creamos un fantasma. Si no está, explotamos rápido (Fail-Fast).
            throw new InvalidOperationException(
                $"No se encontró el detalle operativo del viaje '{viajeId}'. No es posible adjuntar barcazas.");
        }

        var etapaAnterior = detalle.Etapas?.LastOrDefault()
                            ?? throw new InvalidOperationException(
                                   $"El viaje {viajeId} no posee etapas activas. No es posible adjuntar barcazas.");

        var barcazasNuevas = request.BarcazasIds
            .Select(id => new BarcazaMongo { Nombre = id, Carga = "A Definir", Cantidad = 0 })
            .ToList();

        // FIX HITO 10.5: En lugar de crear una Etapa 2, 3, etc., 
        // simplemente agregamos las barcazas a la etapa actual (la Etapa 1 si está amarrado).
        etapaAnterior.Barcazas ??= new List<BarcazaMongo>();
        etapaAnterior.Barcazas.AddRange(barcazasNuevas);

        // 🔥 IMPORTANTE: Reemplazamos usando el detalle.Id real (el MongoDb ObjectId del detalle).
        await _detallesCollection.ReplaceOneAsync(
            Builders<ViajeDetalleMongo>.Filter.Eq(x => x.Id, detalle.Id),
            detalle,
            cancellationToken: ct);

        _logger.LogInformation(
            "AdjuntarBarcazasAsync: {Count} barcaza(s) adjuntadas a EtapaId={EtapaId} en MongoDB para ViajeId={ViajeId}.",
            barcazasNuevas.Count, etapaAnterior.EtapaId, viajeId);

        _cache.Remove($"cargas_viaje_{viajeId}");

        if (_env.IsDevelopment())
        {
            return true;
        }

        try
        {
            var barcazasParam = string.Join(',', request.BarcazasIds);
            using var connection = new OracleConnection(_oracleConnectionString);
            await connection.ExecuteAsync(
                "mbpc.adjuntar_barcazas",
                new { p_BARCAZAS = barcazasParam, p_UBICACION = request.Ubicacion },
                commandType: CommandType.StoredProcedure);
        }
        catch (OracleException oraEx)
        {
            return ManejarErrorOracle(oraEx, "adjuntar_barcazas", viajeId);
        }

        return true;
    }

    public async Task<bool> SepararConvoyAsync(
        string               viajeId,
        SepararConvoyRequest request,
        CancellationToken    ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(viajeId);
        ArgumentNullException.ThrowIfNull(request);

        if (request.BarcazasIds is null || request.BarcazasIds.Count == 0)
            throw new ValidationException("Debe especificar al menos una barcaza para separar.");

        if (string.IsNullOrWhiteSpace(request.Ubicacion))
            throw new ValidationException("La ubicación es requerida para separar barcazas.");

        var (detalle, travelId) = await _viajeService.GetViajeDetalleByIdAsync(viajeId, ct);

        if (detalle is null)
            throw new InvalidOperationException(
                $"El viaje con Id='{viajeId}' no posee un convoy activo para separar.");

        var etapaAnterior = detalle.Etapas?.LastOrDefault()
                            ?? throw new InvalidOperationException(
                                   $"El viaje {viajeId} no posee etapas activas. " +
                                   "No es posible separar barcazas.");

        var idsAExcluir = new HashSet<string>(
            request.BarcazasIds,
            StringComparer.OrdinalIgnoreCase);

        var barcazasResultantes = (etapaAnterior.Barcazas ?? [])
            .Where(b => !idsAExcluir.Contains(b.Nombre ?? string.Empty))
            .ToList();

        var nuevaEtapa = new EtapaMongo
        {
            EtapaId     = etapaAnterior.EtapaId + 1,
            FechaInicio = DateTime.UtcNow,
            Remolcador  = etapaAnterior.Remolcador,
            Barcazas    = barcazasResultantes
        };

        detalle.Etapas!.Add(nuevaEtapa);

        await _detallesCollection.ReplaceOneAsync(
            Builders<ViajeDetalleMongo>.Filter.Eq(x => x.Id, detalle.Id),
            detalle,
            cancellationToken: ct);

        // INVALIDACIÓN DE CACHÉ DE CARGAS
        _cache.Remove($"cargas_viaje_{viajeId}");

        if (_env.IsDevelopment())
        {
            return true;
        }

        try
        {
            var barcazasParam = string.Join(',', request.BarcazasIds);

            using var connection = new OracleConnection(_oracleConnectionString);
            await connection.ExecuteAsync(
                "mbpc.separar_convoy",
                new
                {
                    p_BARCAZAS  = barcazasParam,
                    p_UBICACION = request.Ubicacion
                },
                commandType: CommandType.StoredProcedure);
        }
        catch (OracleException oraEx)
        {
            return ManejarErrorOracle(oraEx, "separar_convoy", viajeId);
        }

        return true;
    }



    private bool ManejarErrorOracle(OracleException ex, string operacion, string viajeId)
    {
        if (_env.IsDevelopment())
        {
            _logger.LogWarning(
                ex,
                "ManejarErrorOracle: [DEV BYPASS] Oracle falló en SP '{Operacion}' " +
                "para ViajeId={ViajeId}. La mutación en MongoDB ya fue aplicada. " +
                "Código Oracle: {OraCode}.",
                operacion, viajeId, ex.Number);

            return true;
        }

        _logger.LogError(
            ex,
            "ManejarErrorOracle: Error en SP '{Operacion}' para ViajeId={ViajeId}. " +
            "Código Oracle: {OraCode}.",
            operacion, viajeId, ex.Number);

        throw new InvalidOperationException(
            $"Oracle rechazó la operación '{operacion}' (código {ex.Number}). " +
            "Contacte al administrador de base de datos.",
            ex);
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // ResolverBarcazasAsync
    //
    // Cadena de fuentes con red de seguridad garantizada:
    //
    //   1. Última etapa (formato canónico nuevo)   → barcazasRaw con datos
    //   2. Propiedad raíz legacy "barcazas"        → barcazasRaw con datos
    //   3. Oracle                                  → retorno directo (early-return)
    //
    // El punto crítico del fix: la decisión de ir a Oracle se toma DESPUÉS de
    // intentar ambas fuentes Mongo, en un único bloque de control al final.
    // Esto elimina el bug donde entrar por la rama de Etapas con barcazas vacías
    // saltaba el fallback Oracle y continuaba con una lista vacía hacia el catálogo.
    // ─────────────────────────────────────────────────────────────────────────────
    private async Task<List<BarcazaConvoyDto>> ResolverBarcazasAsync(
        string             viajeId,
        long               travelId,
        ViajeDetalleMongo? detalle)
    {
        // ── Paso 1: intentar resolver barcazas desde MongoDB (ambas fuentes) ────
        var barcazasRaw = ResolverBarcazasDesdeMongo(detalle, viajeId);

        // ── Paso 2: si MongoDB no aportó nada, activar fallback Oracle ───────────
        // Este bloque es el único punto de decisión para Oracle, sin importar
        // si se llegó aquí desde la rama de Etapas o desde la raíz legacy.
        if (barcazasRaw.Count == 0)
        {
            long idViajeRelacional = detalle?.IdViaje > 0 ? detalle.IdViaje.GetValueOrDefault() : travelId;

            if (idViajeRelacional > 0)
            {
                _logger.LogWarning(
                    "ResolverBarcazas: MongoDB no devolvió barcazas para ViajeId={ViajeId}. " +
                    "Activando fallback Oracle con IdViaje Relacional={IdViajeRelacional}.",
                    viajeId, idViajeRelacional);

                var cargasLegacy = await _cargaService.ObtenerCargasPorViaje(idViajeRelacional.ToString());

                if (cargasLegacy is null || !cargasLegacy.Any())
                {
                    _logger.LogWarning(
                        "ResolverBarcazas: El fallback a Oracle no devolvió cargas " +
                        "para IdViaje Relacional={IdViajeRelacional}.",
                        idViajeRelacional);
                    return [];
                }

                return cargasLegacy
                    .Where(c => c is not null)
                    .Select(MapearBarcazaDesdeOracle)
                    .ToList();
            }

            _logger.LogWarning(
                "ResolverBarcazas: Sin barcazas en Mongo y sin IdViaje Relacional para ViajeId={ViajeId}. " +
                "No es posible consultar Oracle.",
                viajeId);

            return [];
        }

        // ── Paso 3: Hito 5.9 — Patrón Anti-N+1 ─────────────────────────────────
        // Solo se ejecuta si barcazasRaw tiene datos. Oracle ya hizo early-return.
        // Resolución diferida para evitar ciclo de DI (mismo patrón que CargaManagerService).
        var buqueService = _serviceProvider.GetRequiredService<IBuqueService>();

        // Recolectar todos los IDs numéricos únicos de las barcazas
        var idsNumericos = barcazasRaw
            .Select(b => b.Nombre)
            .Where(nombre => !string.IsNullOrWhiteSpace(nombre) && long.TryParse(nombre, out _))
            .Select(nombre => long.Parse(nombre!))
            .Distinct()
            .ToList();

        // Una sola llamada al catálogo → Dictionary<long, BuqueAutocompleteDto>
        var catalogoBarcazas = idsNumericos.Any()
            ? await buqueService.ObtenerBuquesPorIdsAsync(idsNumericos)
            : new Dictionary<long, BuqueAutocompleteDto>();

        _logger.LogDebug(
            "ResolverBarcazas Hito 5.9 — Batch lookup resolvió {Resueltos}/{Total} ID(s) numéricos en 1 round-trip para ViajeId={ViajeId}.",
            catalogoBarcazas.Count, idsNumericos.Count, viajeId);

        // Mapear con lookups O(1)
        return barcazasRaw
            .Select(b => MapearBarcazaDesdeMongo(b, catalogoBarcazas))
            .ToList();
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // ResolverBarcazasDesdeMongo
    //
    // Responsabilidad única: determinar qué lista de BarcazaMongo usar desde
    // el documento BSON, consultando las dos fuentes posibles en orden de
    // prioridad. Nunca toca Oracle ni servicios externos.
    //
    // Prioridad:
    //   1. Última etapa (Etapas[last].Barcazas)  → formato canónico nuevo
    //   2. Propiedad raíz (detalle.Barcazas)     → formato legacy "barcazas"
    //   3. Lista vacía                            → el llamador decide qué hacer
    // ─────────────────────────────────────────────────────────────────────────────
    private List<BarcazaMongo> ResolverBarcazasDesdeMongo(
        ViajeDetalleMongo? detalle,
        string             viajeId)
    {
        if (detalle?.Etapas is { Count: > 0 })
        {
            var ultimaEtapa   = detalle.Etapas.OrderByDescending(e => e.EtapaId).First();
            var barcazasEtapa = ultimaEtapa.Barcazas?.Where(b => b is not null).ToList() ?? [];

            if (barcazasEtapa.Count > 0)
            {
                _logger.LogDebug(
                    "ResolverBarcazasDesdeMongo: Usando la última etapa activa (EtapaId={EtapaId}) " +
                    "con {Count} barcaza(s) para ViajeId={ViajeId}.",
                    ultimaEtapa.EtapaId, barcazasEtapa.Count, viajeId);

                return barcazasEtapa;
            }

            // La etapa existe pero está vacía → intentar raíz legacy antes de
            // dejar que el llamador active el fallback Oracle.
            _logger.LogDebug(
                "ResolverBarcazasDesdeMongo: Última etapa (EtapaId={EtapaId}) sin barcazas. " +
                "Intentando propiedad raíz legacy para ViajeId={ViajeId}.",
                ultimaEtapa.EtapaId, viajeId);
        }

        var barcazasRaiz = detalle?.Barcazas?.Where(b => b is not null).ToList() ?? [];

        if (barcazasRaiz.Count > 0)
        {
            _logger.LogDebug(
                "ResolverBarcazasDesdeMongo: Usando {Count} barcaza(s) desde propiedad raíz legacy para ViajeId={ViajeId}.",
                barcazasRaiz.Count, viajeId);
        }

        // Devuelve lista vacía si ninguna fuente Mongo tiene datos.
        // ResolverBarcazasAsync evaluará si corresponde ir a Oracle.
        return barcazasRaiz;
    }

    private static BarcazaConvoyDto MapearBarcazaDesdeMongo(
        BarcazaMongo b,
        Dictionary<long, BuqueAutocompleteDto> catalogo)
    {
        // Hito 5.9: Si el Nombre es un ID numérico, resolverlo desde el catálogo batch (lookup O(1))
        string nombreDisplay    = b.Nombre ?? "S/N";
        string? matriculaDisplay = b.Matricula;

        if (!string.IsNullOrWhiteSpace(b.Nombre)
            && long.TryParse(b.Nombre, out long idNum)
            && catalogo.TryGetValue(idNum, out var info))
        {
            nombreDisplay    = info.Nombre   ?? b.Nombre;
            matriculaDisplay = info.Matricula ?? b.Matricula;
        }

        return new BarcazaConvoyDto(
            Id:           string.IsNullOrWhiteSpace(b.Matricula) ? b.Nombre! : b.Matricula,
            Nombre:       nombreDisplay,
            Bandera:      b.Bandera    ?? "N/A",
            Matricula:    matriculaDisplay,
            TipoCarga:    b.Carga      ?? "A Definir",
            Tonelaje:     b.Cantidad   ?? 0d,
            Unidad:       b.Unidad     ?? "TON",
            MuelleActual: b.MuelleActual,
            Estado:       string.IsNullOrWhiteSpace(b.MuelleActual)
                              ? EstadoBarcaza.EnTransito
                              : EstadoBarcaza.Amarrada
        );
    }

    private static BarcazaConvoyDto MapearBarcazaDesdeOracle(CargaDto c) =>
        new(
            Id:           c.Id,
            Nombre:       string.IsNullOrWhiteSpace(c.DescripcionLista)
                              ? "Unidad sin nombre"
                              : c.DescripcionLista,
            Bandera:      "N/A",
            Matricula:    null,
            TipoCarga:    string.IsNullOrWhiteSpace(c.NivelRiesgo)
                              ? "General"
                              : c.NivelRiesgo,
            Tonelaje:     c.Tonelaje,
            Unidad:       "TON",
            MuelleActual: c.MuelleActual,
            Estado:       string.IsNullOrWhiteSpace(c.MuelleActual)
                              ? EstadoBarcaza.EnTransito
                              : EstadoBarcaza.Amarrada
        );

    private static RemolcadorConvoyDto? MapearRemolcador(ViajeDetalleMongo? detalle, string? estadoNavegacion = null)
    {
        var remolcador = detalle?.Etapas
                            ?.OrderByDescending(e => e.EtapaId)
                            .FirstOrDefault()
                            ?.Remolcador
                         ?? detalle?.RemolcadorLegacy;

        // Bug Fix: Resolver el estado primero con el valor real
        var estadoRemolcador = !string.IsNullOrWhiteSpace(estadoNavegacion)
            ? estadoNavegacion
            : "Operativo";

        // Si no hay un sub-objeto remolcador, el buque principal ES la cabeza del convoy.
        // Retornamos el DTO usando VesselName para no perder el estado hacia el frontend.
        if (remolcador is null)
        {
            if (detalle is null) return null;

            return new RemolcadorConvoyDto(
                Id:          detalle.VesselName ?? "SIN_ID",
                Nombre:      detalle.VesselName ?? "Desconocido",
                Estado:      estadoRemolcador,
                FechaSalida: null
            );
        }

        var id = remolcador.Matricula
              ?? remolcador.Nombre
              ?? "SIN_IDENTIFICADOR";

        return new RemolcadorConvoyDto(
            Id:          id,
            Nombre:      remolcador.Nombre ?? "Desconocido",
            Estado:      estadoRemolcador,
            FechaSalida: null
        );
    }
}