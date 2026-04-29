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
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Oracle.ManagedDataAccess.Client;

namespace Mbpc.Api.Services;

/// <inheritdoc cref="IConvoyManagerService"/>
public sealed class ConvoyManagerService : IConvoyManagerService
{
    private readonly IViajeService                 _viajeService;
    private readonly ICargaService                 _cargaService;
    private readonly IHostEnvironment              _env;
    private readonly ILogger<ConvoyManagerService> _logger;

    private readonly IMongoCollection<ViajeDetalleMongo> _detallesCollection;
    private readonly string _oracleConnectionString;

    public ConvoyManagerService(
        IViajeService                  viajeService,
        ICargaService                  cargaService,
        IMongoClient                   mongoClient,
        IOptions<MongoDbSettings>      mongoSettings,
        IOptions<OracleDbSettings>     oracleSettings,
        IHostEnvironment               env,
        ILogger<ConvoyManagerService>  logger)
    {
        _viajeService = viajeService ?? throw new ArgumentNullException(nameof(viajeService));
        _cargaService = cargaService ?? throw new ArgumentNullException(nameof(cargaService));
        _env          = env          ?? throw new ArgumentNullException(nameof(env));
        _logger       = logger       ?? throw new ArgumentNullException(nameof(logger));

        ArgumentNullException.ThrowIfNull(mongoClient);
        ArgumentNullException.ThrowIfNull(mongoSettings?.Value);
        ArgumentNullException.ThrowIfNull(oracleSettings?.Value);

        var database = mongoClient.GetDatabase(mongoSettings.Value.DatabaseName);
        _detallesCollection = database.GetCollection<ViajeDetalleMongo>("viajes_detalle");

        _oracleConnectionString = oracleSettings.Value.ConnectionString
                                  ?? throw new ArgumentException("Oracle connection string cannot be null.");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // CONSULTAS (Armado del Convoy desde MongoDB + Fallback a Oracle)
    // ─────────────────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<ConvoyDto?> ObtenerConvoyPorViajeIdAsync(
        string            viajeId,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(viajeId);

        // BYPASS DE CACHÉ: Leemos directo de Mongo para tener la foto real del Convoy ahora mismo
        var detalle = await _detallesCollection
            .Find(Builders<ViajeDetalleMongo>.Filter.Eq(x => x.Id, viajeId))
            .FirstOrDefaultAsync(ct);

        long travelId = detalle?.IdViaje ?? 0;

        // Si por algún motivo no existe en Mongo, usamos el servicio base como red de seguridad
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
        var remolcador = MapearRemolcador(detalle);

        return new ConvoyDto
        {
            ViajeId     = detalle?.Id ?? viajeId,
            NombreBuque = detalle?.VesselName ?? "Sin nombre",
            Remolcador  = remolcador,
            Barcazas    = barcazas.AsReadOnly()
        };
    }

    // ─────────────────────────────────────────────────────────────────────────
    // MUTACIONES LEGACY (Mapeo a métodos síncronos legacy)
    // ─────────────────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public Task AmarrarBarcazaAsync(
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

        var exito = _cargaService.AmarrarBarcaza(barcazaId, request.NuevoMuelle);
        if (!exito)
            throw new InvalidOperationException(
                "El sistema legacy rechazó la operación de amarre.");

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task FondearBarcazaAsync(
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

        var exito = _cargaService.FondearBarcaza(barcazaId, request.ZonaFondeo);
        if (!exito)
            throw new InvalidOperationException(
                "El sistema legacy rechazó la operación de fondeo.");

        return Task.CompletedTask;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // MUTACIONES CQRS — ADJUNTAR / SEPARAR (Load-Mutate-Save + Oracle sync)
    // ─────────────────────────────────────────────────────────────────────────

    /// <inheritdoc/>
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
            viajeId,
            string.Join(',', request.BarcazasIds),
            request.Ubicacion);

        // ── Búsqueda en MongoDB — si no existe, se inicializa un documento vacío ────
        var detalle = await _detallesCollection
            .Find(Builders<ViajeDetalleMongo>.Filter.Eq(x => x.Id, viajeId))
            .FirstOrDefaultAsync(ct);

        if (detalle is null)
        {
            _logger.LogWarning(
                "AdjuntarBarcazasAsync: Documento BSON no encontrado para ViajeId={ViajeId}. " +
                "Se inicializa un nuevo ViajeDetalleMongo hidratando el estado legacy de Oracle (anti Split-Brain).",
                viajeId);

            var barcazasHidratadas = new List<BarcazaMongo>();
            long travelIdHidratado = 0;

            try
            {
                var (_, travelId) = await _viajeService.GetViajeDetalleByIdAsync(viajeId, ct);
                travelIdHidratado = travelId;

                if (travelId > 0)
                {
                    var cargasLegacy = await _cargaService.ObtenerCargasPorViaje(travelId.ToString());

                    if (cargasLegacy is not null && cargasLegacy.Any())
                    {
                        barcazasHidratadas = cargasLegacy
                            .Where(c => c is not null)
                            .Select(c => new BarcazaMongo
                            {
                                NombreModern      = c.Id,
                                CargaModern       = c.NivelRiesgo ?? "General",
                                CantidadModern    = c.Tonelaje,
                                UnidadModern      = "TON",
                                MuelleActualModern = c.MuelleActual
                            })
                            .ToList();
                    }
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(
                    ex,
                    "AdjuntarBarcazasAsync: Error al hidratar estado legacy desde Oracle para ViajeId={ViajeId}. " +
                    "Se continúa con etapa vacía.",
                    viajeId);
            }

            detalle = new ViajeDetalleMongo
            {
                Id      = viajeId,
                IdViaje = travelIdHidratado,
                Etapas  = new List<EtapaMongo>
                {
                    new EtapaMongo
                    {
                        EtapaId     = 1,
                        FechaInicio = DateTime.UtcNow,
                        Remolcador  = null,
                        Barcazas    = barcazasHidratadas
                    }
                }
            };

            await _detallesCollection.InsertOneAsync(detalle, cancellationToken: ct);
        }

        var etapaAnterior = detalle.Etapas?.LastOrDefault()
                            ?? throw new InvalidOperationException(
                                   $"El viaje {viajeId} no posee etapas activas. " +
                                   "No es posible adjuntar barcazas.");

        var barcazasNuevas = request.BarcazasIds
            .Select(id => new BarcazaMongo
            {
                Nombre   = id,
                Carga    = "A Definir",
                Cantidad = 0
            })
            .ToList();

        var barcazasCombinadas = (etapaAnterior.Barcazas ?? [])
            .Concat(barcazasNuevas)
            .ToList();

        var nuevaEtapa = new EtapaMongo
        {
            EtapaId     = etapaAnterior.EtapaId + 1,
            FechaInicio = DateTime.UtcNow,
            Remolcador  = etapaAnterior.Remolcador,
            Barcazas    = barcazasCombinadas
        };

        detalle.Etapas!.Add(nuevaEtapa);

        await _detallesCollection.ReplaceOneAsync(
            Builders<ViajeDetalleMongo>.Filter.Eq(x => x.Id, viajeId),
            detalle,
            cancellationToken: ct);

        _logger.LogInformation(
            "AdjuntarBarcazasAsync: Nueva EtapaId={EtapaId} persistida en MongoDB para ViajeId={ViajeId}.",
            nuevaEtapa.EtapaId, viajeId);

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
                new
                {
                    p_BARCAZAS  = barcazasParam,
                    p_UBICACION = request.Ubicacion
                },
                commandType: CommandType.StoredProcedure);
        }
        catch (OracleException oraEx)
        {
            return ManejarErrorOracle(oraEx, "adjuntar_barcazas", viajeId);
        }

        return true;
    }

    /// <inheritdoc/>
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

        var detalle = await _detallesCollection
            .Find(Builders<ViajeDetalleMongo>.Filter.Eq(x => x.Id, viajeId))
            .FirstOrDefaultAsync(ct);

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
            Builders<ViajeDetalleMongo>.Filter.Eq(x => x.Id, viajeId),
            detalle,
            cancellationToken: ct);

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

    // ─────────────────────────────────────────────────────────────────────────
    // HELPERS PRIVADOS
    // ─────────────────────────────────────────────────────────────────────────

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

    private async Task<List<BarcazaConvoyDto>> ResolverBarcazasAsync(
        string             viajeId,
        long               travelId,
        ViajeDetalleMongo? detalle)
    {
        // 1. REGLA DE NEGOCIO: Tomar SOLO las barcazas de la última etapa registrada
        if (detalle?.Etapas != null && detalle.Etapas.Any())
        {
            var ultimaEtapa = detalle.Etapas.OrderByDescending(e => e.EtapaId).First();
            
            var barcazasDeUltimaEtapa = ultimaEtapa.Barcazas
                ?.Where(b => b is not null)
                .ToList() ?? new List<BarcazaMongo>();

            _logger.LogDebug(
                "ResolverBarcazas: Usando la última etapa activa (EtapaId={EtapaId}) " +
                "con {Count} barcaza(s) para ViajeId={ViajeId}.",
                ultimaEtapa.EtapaId, barcazasDeUltimaEtapa.Count, viajeId);

            // Retornamos sin importar si está vacía. Un convoy vacío es válido post-separación.
            return barcazasDeUltimaEtapa
                .Select(MapearBarcazaDesdeMongo)
                .ToList();
        }

        // 2. Fallback a propiedad legacy en la raíz del documento (Documentos no migrados a CQRS)
        if (detalle?.BarcazasLegacy != null && detalle.BarcazasLegacy.Any())
        {
            var barcazasRoot = detalle.BarcazasLegacy.Where(b => b is not null).ToList();

            _logger.LogDebug(
                "ResolverBarcazas: Usando {Count} barcaza(s) de Mongo desde la propiedad legacy raíz para ViajeId={ViajeId}.",
                barcazasRoot.Count, viajeId);

            return barcazasRoot
                .Select(MapearBarcazaDesdeMongo)
                .ToList();
        }

        // 3. Fallback a Oracle: NUNCA usar el ObjectId, buscar estrictamente el Id relacional
        long idViajeRelacional = detalle?.IdViaje > 0 ? detalle.IdViaje : travelId;

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

    private static BarcazaConvoyDto MapearBarcazaDesdeMongo(BarcazaMongo b) =>
        new(
            Id:           string.IsNullOrWhiteSpace(b.Matricula) ? b.Nombre : b.Matricula,
            Nombre:       b.Nombre,
            Bandera:      b.Bandera,
            Matricula:    b.Matricula,
            TipoCarga:    b.Carga,
            Tonelaje:     b.Cantidad,
            Unidad:       b.Unidad,
            MuelleActual: b.MuelleActual,
            Estado:       string.IsNullOrWhiteSpace(b.MuelleActual)
                              ? EstadoBarcaza.EnTransito
                              : EstadoBarcaza.Amarrada
        );

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

    private static RemolcadorConvoyDto? MapearRemolcador(ViajeDetalleMongo? detalle)
    {
        var remolcador = detalle?.Etapas?.OrderByDescending(e => e.EtapaId).FirstOrDefault()?.Remolcador;
        if (remolcador is null) return null;

        var id = remolcador.Matricula
              ?? remolcador.Nombre
              ?? "SIN_IDENTIFICADOR";

        return new RemolcadorConvoyDto(
            Id:          id,
            Nombre:      remolcador.Nombre ?? "Desconocido",
            Estado:      "Operativo",
            FechaSalida: null
        );
    }
}