using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Mbpc.Api.DTOs;
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
                    _logger.LogInformation("Iniciando ciclo de reconciliación espacial...");

                    using (var scope = _serviceScopeFactory.CreateScope())
                    {
                        var httpContextAccessor = scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>();
                        var viajeService = scope.ServiceProvider.GetRequiredService<IViajeService>();
                        var costeraService = scope.ServiceProvider.GetRequiredService<ICosteraService>();

                        // 1. Simular contexto de superusuario (CosteraId = 0)
                        var identity = new ClaimsIdentity(new[] { new Claim("CosteraId", "0") }, "BackgroundSystem");
                        httpContextAccessor.HttpContext = new DefaultHttpContext
                        {
                            User = new ClaimsPrincipal(identity)
                        };

                        // 2. Obtener datos
                        var viajes = await viajeService.GetMapaViajesAsync(null, null);
                        var costeras = await costeraService.ObtenerLimitesJurisdiccionalesAsync();

                        _logger.LogInformation("Procesando {CantViajes} viajes activos y {CantCosteras} jurisdicciones...", viajes.Count(), costeras.Count());

                        foreach (var viaje in viajes)
                        {
                            if (viaje.Latitud == 0 && viaje.Longitud == 0) continue;

                            try 
                            {
                                // 3. Evaluar cruce espacial con algoritmo Híbrido
                                int nuevaCosteraId = DeterminarJurisdicionCorrespondiente(viaje.Latitud, viaje.Longitud, costeras);

                                if (nuevaCosteraId > 0 && nuevaCosteraId != ObtenerCosteraIdActual(viaje))
                                {
                                    _logger.LogInformation(
                                        "RECONCILIACIÓN DETECTADA: Buque {Buque} pasa de Costera {ActualId} a Costera {NuevaId}.",
                                        viaje.NombreBuque, ObtenerCosteraIdActual(viaje), nuevaCosteraId);

                                    var identityTransfer = new ClaimsIdentity(new[] { new Claim("CosteraId", ObtenerCosteraIdActual(viaje).ToString()) }, "BackgroundSystem");
                                    httpContextAccessor.HttpContext!.User = new ClaimsPrincipal(identityTransfer);

                                    var dto = new TransferirJurisdiccionDto
                                    {
                                        NuevaCosteraId = nuevaCosteraId,
                                        Velocidad = viaje.Velocidad,
                                        Rumbo = viaje.Rumbo
                                    };
                                    
                                    try 
                                    {
                                        bool exito = await viajeService.TransferirJurisdiccionAsync(viaje.Id, dto);
                                        if (exito) _logger.LogInformation("Transferencia automática OK para {Buque}.", viaje.NombreBuque);
                                    }
                                    catch (Exception transferEx)
                                    {
                                        _logger.LogWarning("Descartando transferencia de {Buque} (Posible fantasma de caché): {Msg}", viaje.NombreBuque, transferEx.Message);
                                    }
                                    finally
                                    {
                                        // Restaurar SIEMPRE el identity de superusuario para el próximo buque del loop
                                        httpContextAccessor.HttpContext.User = new ClaimsPrincipal(identity);
                                    }
                                }
                            }
                            catch (Exception itemEx)
                            {
                                _logger.LogError("Error evaluando la geometría del buque {Buque}: {Msg}", viaje.NombreBuque, itemEx.Message);
                            }
                        }
                    }

                    _logger.LogInformation("Ciclo de reconciliación completado.");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Falla masiva en el ciclo de reconciliación.");
                }

                // TIEMPO DE PRODUCCIÓN: 5 minutos
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }

            _logger.LogInformation("ReconciliacionEspacialWorker detenido.");
        }

        private int DeterminarJurisdicionCorrespondiente(double latitud, double longitud, IEnumerable<CosteraDto> costeras)
        {
            int mejorCosteraId = 0;
            double distanciaMinima = double.MaxValue;

            foreach (var costera in costeras)
            {
                if (costera.Geometry?.Coordinates == null) continue;

                var type = costera.Geometry.Type;
                double minDistanciaLocal = double.MaxValue;

                if (type.Equals("Polygon", StringComparison.OrdinalIgnoreCase) || 
                    type.Equals("LineString", StringComparison.OrdinalIgnoreCase))
                {
                    if (costera.Geometry.Coordinates is double[][] poligono)
                    {
                        if (type.Equals("Polygon", StringComparison.OrdinalIgnoreCase) && IsPointInPolygon(latitud, longitud, poligono))
                            return costera.Properties.CosteraId;
                        
                        minDistanciaLocal = DistanciaMinimaAPoligono(latitud, longitud, poligono);
                    }
                }
                else if (type.Equals("MultiPolygon", StringComparison.OrdinalIgnoreCase))
                {
                    if (costera.Geometry.Coordinates is double[][][] multiPoligono)
                    {
                        foreach (var poligono in multiPoligono)
                        {
                            if (IsPointInPolygon(latitud, longitud, poligono))
                                return costera.Properties.CosteraId; 
                            
                            double dist = DistanciaMinimaAPoligono(latitud, longitud, poligono);
                            if (dist < minDistanciaLocal) minDistanciaLocal = dist;
                        }
                    }
                }

                if (minDistanciaLocal < distanciaMinima)
                {
                    distanciaMinima = minDistanciaLocal;
                    mejorCosteraId = costera.Properties.CosteraId;
                }
            }

            if (distanciaMinima < 0.01) return mejorCosteraId;
            return 0; 
        }

        private double DistanciaMinimaAPoligono(double lat, double lon, double[][] poligono)
        {
            double minD = double.MaxValue;
            if (poligono == null || poligono.Length == 0) return minD;

            for (int i = 0; i < poligono.Length - 1; i++)
            {
                double dist = DistanciaPuntoASegmento(lon, lat, poligono[i][0], poligono[i][1], poligono[i + 1][0], poligono[i + 1][1]);
                if (dist < minD) minD = dist;
            }
            
            double distCierre = DistanciaPuntoASegmento(lon, lat, poligono[poligono.Length - 1][0], poligono[poligono.Length - 1][1], poligono[0][0], poligono[0][1]);
            if (distCierre < minD) minD = distCierre;

            return minD;
        }

        private double DistanciaPuntoASegmento(double px, double py, double x1, double y1, double x2, double y2)
        {
            double dx = x2 - x1;
            double dy = y2 - y1;
            double lengthSquared = dx * dx + dy * dy;

            if (lengthSquared == 0.0) return DistanciaCuadrada(px, py, x1, y1);

            double t = Math.Max(0, Math.Min(1, ((px - x1) * dx + (py - y1) * dy) / lengthSquared));
            return DistanciaCuadrada(px, py, x1 + t * dx, y1 + t * dy);
        }

        private double DistanciaCuadrada(double x1, double y1, double x2, double y2)
        {
            return (x1 - x2) * (x1 - x2) + (y1 - y2) * (y1 - y2); 
        }

        private bool IsPointInPolygon(double puntoLat, double puntoLon, double[][] poligono)
        {
            if (poligono == null || poligono.Length < 3) return false;
            bool inside = false;
            for (int i = 0, j = poligono.Length - 1; i < poligono.Length; j = i++)
            {
                if ((poligono[i][1] > puntoLat) != (poligono[j][1] > puntoLat) &&
                    (puntoLon < (poligono[j][0] - poligono[i][0]) * (puntoLat - poligono[i][1]) / (poligono[j][1] - poligono[i][1]) + poligono[i][0]))
                {
                    inside = !inside;
                }
            }
            return inside;
        }

        private int ObtenerCosteraIdActual(MapaViajeDto viaje) => viaje.CosteraId;
    }
}