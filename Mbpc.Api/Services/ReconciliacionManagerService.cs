using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Mbpc.Api.DTOs;
using Mbpc.Api.Services;

namespace Mbpc.Api.Services
{
    public class ReconciliacionManagerService : IReconciliacionService
    {
        private readonly IViajeService _viajeService;
        private readonly ICosteraService _costeraService;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<ReconciliacionManagerService> _logger;

        public ReconciliacionManagerService(
            IViajeService viajeService, 
            ICosteraService costeraService, 
            IHttpContextAccessor httpContextAccessor,
            ILogger<ReconciliacionManagerService> logger)
        {
            _viajeService = viajeService;
            _costeraService = costeraService;
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
        }

        public async Task EjecutarCicloReconciliacionAsync(CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Iniciando ciclo de reconciliación espacial global...");
            
            // Simular contexto de superusuario (CosteraId = 0) para leer todos los viajes
            var originalUser = _httpContextAccessor.HttpContext?.User;
            var identity = new ClaimsIdentity(new[] { new Claim("CosteraId", "0") }, "BackgroundSystem");
            if (_httpContextAccessor.HttpContext == null)
            {
                _httpContextAccessor.HttpContext = new DefaultHttpContext();
            }
            _httpContextAccessor.HttpContext.User = new ClaimsPrincipal(identity);

            try
            {
                var viajes = await _viajeService.GetMapaViajesAsync(null, null);
                var costeras = await _costeraService.ObtenerLimitesJurisdiccionalesAsync();

                _logger.LogInformation("Procesando {CantViajes} viajes activos y {CantCosteras} jurisdicciones...", viajes.Count(), costeras.Count());

                foreach (var viaje in viajes)
                {
                    if (cancellationToken.IsCancellationRequested) break;
                    if (viaje.Latitud == 0 && viaje.Longitud == 0) continue;

                    try 
                    {
                        int nuevaCosteraId = DeterminarJurisdicionCorrespondiente(viaje.Latitud, viaje.Longitud, costeras);

                        if (nuevaCosteraId > 0 && nuevaCosteraId != viaje.CosteraId)
                        {
                            _logger.LogInformation(
                                "RECONCILIACIÓN DETECTADA: Buque {Buque} (TravelId: {TravelId}) pasa de Costera {ActualId} a Costera {NuevaId}.",
                                viaje.NombreBuque, viaje.Id, viaje.CosteraId, nuevaCosteraId);

                            // Para transferir, simulamos temporalmente la costera del viaje original
                            var identityTransfer = new ClaimsIdentity(new[] { new Claim("CosteraId", viaje.CosteraId.ToString()) }, "BackgroundSystem");
                            _httpContextAccessor.HttpContext.User = new ClaimsPrincipal(identityTransfer);

                            var dto = new TransferirJurisdiccionDto
                            {
                                NuevaCosteraId = nuevaCosteraId,
                                Velocidad = viaje.Velocidad,
                                Rumbo = viaje.Rumbo
                            };
                            
                            try 
                            {
                                bool exito = await _viajeService.TransferirJurisdiccionAsync(viaje.Id, dto);
                                if (exito) _logger.LogInformation("Transferencia automática OK para {Buque}.", viaje.NombreBuque);
                            }
                            catch (Exception transferEx)
                            {
                                _logger.LogWarning("Descartando transferencia de {Buque}: {Msg}", viaje.NombreBuque, transferEx.Message);
                            }
                            finally
                            {
                                // Restaurar superusuario para continuar el loop
                                _httpContextAccessor.HttpContext.User = new ClaimsPrincipal(identity);
                            }
                        }
                    }
                    catch (Exception itemEx)
                    {
                        _logger.LogError("Error evaluando la geometría del buque {Buque}: {Msg}", viaje.NombreBuque, itemEx.Message);
                    }
                }
                _logger.LogInformation("Ciclo de reconciliación global completado.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Falla en el ciclo de reconciliación global del servicio.");
            }
            finally
            {
                // Restaurar el principal original
                if (originalUser != null)
                {
                    _httpContextAccessor.HttpContext.User = originalUser;
                }
            }
        }

        public async Task<bool> ForzarReconciliacionViajeAsync(long travelId, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation($"Forzando reconciliación reactiva para el viaje: {travelId}");

            // Simular contexto de superusuario (CosteraId = 0) para leer todos los viajes
            var originalUser = _httpContextAccessor.HttpContext?.User;
            var identity = new ClaimsIdentity(new[] { new Claim("CosteraId", "0") }, "BackgroundSystem");
            if (_httpContextAccessor.HttpContext == null)
            {
                _httpContextAccessor.HttpContext = new DefaultHttpContext();
            }
            _httpContextAccessor.HttpContext.User = new ClaimsPrincipal(identity);

            try
            {
                var viajesMongo = await _viajeService.GetViajesAsync(null, 1, 100);
                var viajePos = viajesMongo.FirstOrDefault(v => v.TravelId == travelId);
                if (viajePos == null)
                {
                    _logger.LogWarning($"No se encontró la posición para el TravelId {travelId} para realizar la reconciliación forzada.");
                    return false;
                }

                _logger.LogInformation($"Viaje encontrado. Nombre: {viajePos.VesselName}, CosteraId actual en DB: {viajePos.CosteraId}, Coords: [{viajePos.Latitude}, {viajePos.Longitude}]");

                if (viajePos.Latitude == 0 && viajePos.Longitude == 0) return false;

                var costeras = await _costeraService.ObtenerLimitesJurisdiccionalesAsync();
                int nuevaCosteraId = DeterminarJurisdicionCorrespondiente(viajePos.Latitude, viajePos.Longitude, costeras);

                _logger.LogInformation($"Determinación de Jurisdicción para Coords [{viajePos.Latitude}, {viajePos.Longitude}] retornó CosteraId: {nuevaCosteraId}");

                if (nuevaCosteraId > 0 && nuevaCosteraId != viajePos.CosteraId)
                {
                    _logger.LogInformation(
                        "RECONCILIACIÓN DETECTADA (FORZADA): Buque {Buque} (TravelId: {TravelId}) pasa de Costera {ActualId} a Costera {NuevaId}.",
                        viajePos.VesselName, viajePos.TravelId, viajePos.CosteraId, nuevaCosteraId);

                    // Para transferir, simulamos temporalmente la costera del viaje original
                    var identityTransfer = new ClaimsIdentity(new[] { new Claim("CosteraId", (viajePos.CosteraId ?? 0).ToString()) }, "BackgroundSystem");
                    _httpContextAccessor.HttpContext.User = new ClaimsPrincipal(identityTransfer);

                    var dto = new TransferirJurisdiccionDto
                    {
                        NuevaCosteraId = nuevaCosteraId,
                        Velocidad = viajePos.SpeedOverGround,
                        Rumbo = viajePos.CourseOverGround
                    };

                    return await _viajeService.TransferirJurisdiccionAsync(viajePos.TravelId.ToString(), dto);
                }

                return false;
            }
            finally
            {
                // Restaurar el principal original
                if (originalUser != null)
                {
                    _httpContextAccessor.HttpContext.User = originalUser;
                }
            }
        }

        // --- LÓGICA GEOESPACIAL PURA ---

        private int DeterminarJurisdicionCorrespondiente(double latitud, double longitud, IEnumerable<CosteraDto> costeras)
        {
            int mejorCosteraId = 0;
            double distanciaMinima = double.MaxValue;

            foreach (var costera in costeras)
            {
                if (costera.Geometry?.Coordinates == null) continue;

                var type = costera.Geometry.Type;
                double minDistanciaLocal = double.MaxValue;

                var poligonos = ObtenerPoligonosDeGeometry(costera.Geometry.Coordinates, type);
                if (poligonos == null || poligonos.Count == 0) continue;

                foreach (var poligono in poligonos)
                {
                    if (type.Equals("Polygon", StringComparison.OrdinalIgnoreCase) || type.Equals("MultiPolygon", StringComparison.OrdinalIgnoreCase))
                    {
                        if (IsPointInPolygon(latitud, longitud, poligono))
                            return costera.Properties.CosteraId;
                    }

                    double dist = DistanciaMinimaAPoligono(latitud, longitud, poligono);
                    if (dist < minDistanciaLocal) minDistanciaLocal = dist;
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

        private List<double[][]> ObtenerPoligonosDeGeometry(object coordinatesObj, string type)
        {
            var resultado = new List<double[][]>();

            if (coordinatesObj is System.Text.Json.JsonElement element)
            {
                if (element.ValueKind != System.Text.Json.JsonValueKind.Array) return resultado;

                if (type.Equals("Polygon", StringComparison.OrdinalIgnoreCase) || type.Equals("LineString", StringComparison.OrdinalIgnoreCase))
                {
                    if (type.Equals("Polygon", StringComparison.OrdinalIgnoreCase))
                    {
                        if (element.GetArrayLength() > 0)
                        {
                            var ring = ParsearAnilloJson(element[0]);
                            if (ring != null) resultado.Add(ring);
                        }
                    }
                    else // LineString
                    {
                        var ring = ParsearAnilloJson(element);
                        if (ring != null) resultado.Add(ring);
                    }
                }
                else if (type.Equals("MultiPolygon", StringComparison.OrdinalIgnoreCase))
                {
                    foreach (var polyElem in element.EnumerateArray())
                    {
                        if (polyElem.ValueKind == System.Text.Json.JsonValueKind.Array && polyElem.GetArrayLength() > 0)
                        {
                            var ring = ParsearAnilloJson(polyElem[0]);
                            if (ring != null) resultado.Add(ring);
                        }
                    }
                }
            }
            else
            {
                // Caso arrays nativos (de MongoDB mapping)
                if (coordinatesObj is double[][] poly2d)
                {
                    resultado.Add(poly2d);
                }
                else if (coordinatesObj is double[][][] poly3d)
                {
                    if (type.Equals("MultiPolygon", StringComparison.OrdinalIgnoreCase))
                    {
                        foreach (var poly in poly3d) resultado.Add(poly);
                    }
                    else if (poly3d.Length > 0)
                    {
                        resultado.Add(poly3d[0]);
                    }
                }
            }

            return resultado;
        }

        private double[][] ParsearAnilloJson(System.Text.Json.JsonElement ringElement)
        {
            if (ringElement.ValueKind != System.Text.Json.JsonValueKind.Array) return null;
            var list = new List<double[]>();
            foreach (var pt in ringElement.EnumerateArray())
            {
                if (pt.ValueKind == System.Text.Json.JsonValueKind.Array && pt.GetArrayLength() >= 2)
                {
                    list.Add(new double[] { pt[0].GetDouble(), pt[1].GetDouble() });
                }
            }
            return list.ToArray();
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
    }
}
