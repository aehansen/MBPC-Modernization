using System;
using System.ComponentModel;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server; // ¡Este es el namespace oficial de Microsoft!
using Mbpc.Api.Services;

namespace Mbpc.McpServer.Tools;

// Le avisa al SDK de Microsoft que esta clase contiene herramientas MCP
[McpServerToolType] 
public class ViajeTools
{
    private readonly IViajeService _viajeService;
    private readonly ILogger<ViajeTools> _logger;

    // El SDK de MCP se encarga de crear el Scope e inyectarnos el IViajeService automáticamente
    public ViajeTools(IViajeService viajeService, ILogger<ViajeTools> logger)
    {
        _viajeService = viajeService;
        _logger = logger;
    }

    // Le avisa al SDK que este método es una Tool que debe exponerse al LLM
    [McpServerTool] 
    [Description("Obtiene la lista de todos los buques que están actualmente en viaje o amarrados en jurisdicción de Prefectura.")]
    public async Task<string> ObtenerViajesActivos()
    {
        _logger.LogInformation("La IA solicitó ejecutar: 'obtener_viajes_activos'");

        try
        {
            var viajes = await _viajeService.GetViajesAsync();

            return JsonSerializer.Serialize(viajes, new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fallo en la Tool MCP al consultar viajes.");
            return JsonSerializer.Serialize(new { status = "error", mensaje = ex.Message });
        }
    }
}