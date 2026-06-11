using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Mbpc.Api.Services;

namespace Mbpc.Api.Workers
{
    public class ReconciliacionEspacialWorker : BackgroundService
    {
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly ILogger<ReconciliacionEspacialWorker> _logger;

        public ReconciliacionEspacialWorker(
            IServiceScopeFactory serviceScopeFactory,
            ILogger<ReconciliacionEspacialWorker> logger)
        {
            _serviceScopeFactory = serviceScopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("ReconciliacionEspacialWorker iniciado.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    _logger.LogInformation("Iniciando ciclo de reconciliación espacial (automático)...");

                    using (var scope = _serviceScopeFactory.CreateScope())
                    {
                        var reconciliacionService = scope.ServiceProvider.GetRequiredService<IReconciliacionService>();
                        await reconciliacionService.EjecutarCicloReconciliacionAsync(stoppingToken);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error crítico en el ciclo de reconciliación espacial.");
                }

                // Pausa de 5 minutos según regla de negocio
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }

            _logger.LogInformation("ReconciliacionEspacialWorker detenido.");
        }
    }
}