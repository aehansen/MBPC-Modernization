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
using Mbpc.Api.Models.Config; // <--- EL USING QUE FALTABA
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

        var (detalle, travelId) = await _viajeService.GetViajeDetalleByIdAsync(viajeId, ct);

        if (detalle is null && travelId == 0)
        {
            _logger.LogWarning(
                "ObtenerConvoyPorViajeIdAsync: No se encontró posición base ni detalle " +
                "para ViajeId={ViajeId}. Se retorna null.",
                viajeId);
            return null;
        }

        var barcazas = await ResolverBarcazasAsync(viajeId, travelId, detalle);
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
        string               barcazaId,
        AmarrarBarcazaRequest request,
        CancellationToken    ct = default)
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
        string              barcazaId,
        FondearBarcazaRequest request,
        CancellationToken   ct = default)
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
        string                 viajeId,
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

        var detalle = await _detallesCollection
            .Find(Builders<ViajeDetalleMongo>.Filter.Eq(x => x.Id, viajeId))
            .FirstOrDefaultAsync(ct)
            ?? throw new KeyNotFoundException($"No se encontró el viaje con Id={viajeId} en MongoDB.");

        var etapaAnterior = detalle.Etapas?.LastOrDefault()
                            ?? throw new InvalidOperationException(
                                   $"El viaje {viajeId} no posee etapas activas. " +
                                   "No es posible adjuntar barcazas.");

        var barcazasNuevas = request.BarcazasIds
            .Select(id => new BarcazaMongo
            {
                Nombre    = id.ToString(),
                Carga     = "A Definir",
                Cantidad  = 0
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

            _logger.LogInformation(
                "AdjuntarBarcazasAsync: SP Oracle ejecutado correctamente para ViajeId={ViajeId}.",
                viajeId);
        }
        catch (OracleException oraEx)
        {
            return ManejarErrorOracle(
                oraEx,
                operacion: "adjuntar_barcazas",
                viajeId:   viajeId);
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

        _logger.LogInformation(
            "SepararConvoyAsync: ViajeId={ViajeId} | Barcazas=[{Ids}] | Ubicacion={Ubicacion}.",
            viajeId,
            string.Join(',', request.BarcazasIds),
            request.Ubicacion);

        var detalle = await _detallesCollection
            .Find(Builders<ViajeDetalleMongo>.Filter.Eq(x => x.Id, viajeId))
            .FirstOrDefaultAsync(ct)
            ?? throw new KeyNotFoundException($"No se encontró el viaje con Id={viajeId} en MongoDB.");

        var etapaAnterior = detalle.Etapas?.LastOrDefault()
                            ?? throw new InvalidOperationException(
                                   $"El viaje {viajeId} no posee etapas activas. " +
                                   "No es posible separar barcazas.");

        var idsAExcluir = new HashSet<long>(request.BarcazasIds);

        var barcazasResultantes = (etapaAnterior.Barcazas ?? [])
            .Where(b =>
            {
                if (long.TryParse(b.Nombre, out var idNombre))
                    return !idsAExcluir.Contains(idNombre);

                _logger.LogDebug(
                    "SepararConvoyAsync: Barcaza con Nombre='{Nombre}' no es numérica; " +
                    "se conserva en el convoy (ViajeId={ViajeId}).",
                    b.Nombre, viajeId);
                return true;
            })
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

        _logger.LogInformation(
            "SepararConvoyAsync: Nueva EtapaId={EtapaId} persistida en MongoDB para ViajeId={ViajeId}. " +
            "Barcazas restantes: {Count}.",
            nuevaEtapa.EtapaId, viajeId, barcazasResultantes.Count);

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

            _logger.LogInformation(
                "SepararConvoyAsync: SP Oracle ejecutado correctamente para ViajeId={ViajeId}.",
                viajeId);
        }
        catch (OracleException oraEx)
        {
            return ManejarErrorOracle(
                oraEx,
                operacion: "separar_convoy",
                viajeId:   viajeId);
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
        var barcazasDeMongo = detalle?.Etapas
            ?.SelectMany(e => e.Barcazas ?? new List<BarcazaMongo>())
            .Where(b => b is not null)
            .ToList()
            ?? new List<BarcazaMongo>();

        if (barcazasDeMongo.Count > 0)
        {
            _logger.LogDebug(
                "ResolverBarcazas: Usando {Count} barcaza(s) de MongoDB (vía Etapas) para ViajeId={ViajeId}.",
                barcazasDeMongo.Count, viajeId);

            return barcazasDeMongo
                .Select(MapearBarcazaDesdeMongo)
                .ToList();
        }

        if (travelId > 0)
        {
            _logger.LogWarning(
                "ResolverBarcazas: MongoDB no devolvió barcazas para ViajeId={ViajeId}. " +
                "Activando fallback Oracle con TravelId={TravelId}.",
                viajeId, travelId);

            var cargasLegacy = await _cargaService.ObtenerCargasPorViaje(travelId.ToString());

            if (cargasLegacy is null || !cargasLegacy.Any())
            {
                _logger.LogWarning(
                    "ResolverBarcazas: El fallback a Oracle tampoco devolvió cargas " +
                    "para TravelId={TravelId}.",
                    travelId);
                return [];
            }

            return cargasLegacy
                .Where(c => c is not null)
                .Select(MapearBarcazaDesdeOracle)
                .ToList();
        }

        _logger.LogWarning(
            "ResolverBarcazas: Sin barcazas en Mongo y TravelId=0 para ViajeId={ViajeId}. " +
            "No es posible consultar Oracle.",
            viajeId);

        return [];
    }

    private static BarcazaConvoyDto MapearBarcazaDesdeMongo(BarcazaMongo b) =>
        new(
            Id:           b.Nombre,
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
        var remolcador = detalle?.Etapas?.LastOrDefault()?.Remolcador;
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