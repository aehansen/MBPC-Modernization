using System;
using System.ComponentModel;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection; // <-- ¡Faltaba este using!
using ModelContextProtocol.Server;
using Mbpc.Api.Services;

namespace Mbpc.McpServer.Tools;

// Le avisa al SDK de Microsoft que esta clase contiene herramientas MCP
[McpServerToolType]
public class ViajeTools
{
    private readonly IServiceProvider _sp;

    // Inyectamos solo el proveedor maestro, esto nunca falla.
    public ViajeTools(IServiceProvider sp)
    {
        _sp = sp;
    }

    [McpServerTool]
    [Description("Obtiene la lista de todos los buques que están actualmente en viaje o amarrados en jurisdicción de Prefectura.")]
    public async Task<string> ObtenerViajesActivos()
    {
        try
        {
            // Instanciamos el servicio ACÁ ADENTRO. Si algo explota (Oracle, Mongo, config nula), lo atrapamos.
            var viajeService = _sp.GetRequiredService<IViajeService>();
            
            var viajes = await viajeService.GetViajesAsync();
            return System.Text.Json.JsonSerializer.Serialize(viajes);
        }
        catch (Exception ex)
        {
            return $"ERROR_CRITICO_VISTO_POR_ARQUITECTO: {ex.Message} | CAUSA INTERNA: {ex.InnerException?.Message}";
        }
    }
}