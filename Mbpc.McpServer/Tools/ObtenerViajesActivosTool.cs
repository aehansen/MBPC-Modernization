using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Mbpc.Api.Services;

namespace Mbpc.McpServer.Tools
{
    /// <summary>
    /// Herramienta MCP encargada de obtener los viajes activos.
    /// Utiliza un Scope para resolver IViajeService de forma segura.
    /// </summary>
    public class ObtenerViajesActivosTool
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<ObtenerViajesActivosTool> _logger;

        public ObtenerViajesActivosTool(IServiceProvider serviceProvider, ILogger<ObtenerViajesActivosTool> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        public async Task<string> EjecutarAsync(CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Bot consultando: 'obtener_viajes_activos'...");

            try
            {
                // Importante: IViajeService es Scoped, necesitamos un scope manual.
                using var scope = _serviceProvider.CreateScope();
                var viajeService = scope.ServiceProvider.GetRequiredService<IViajeService>();

                // Llamamos a MongoDB (con el bot actuando como Costera 0 / Admin)
                var viajes = await viajeService.GetViajesAsync();

                return JsonSerializer.Serialize(viajes, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fallo en la Tool MCP al consultar viajes.");
                return JsonSerializer.Serialize(new { error = "No se pudieron recuperar los viajes.", detalle = ex.Message });
            }
        }
    }
}