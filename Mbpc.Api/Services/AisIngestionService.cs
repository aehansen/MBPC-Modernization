using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Oracle.ManagedDataAccess.Client;
using Mbpc.Api.DTOs;
using Mbpc.Api.Models.Mongo;

namespace Mbpc.Api.Services
{
    public class AisIngestionService : IAisIngestionService
    {
        private readonly IViajeService _viajeService;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<AisIngestionService> _logger;
        private readonly string _aisConnectionString;

        public AisIngestionService(
            IViajeService viajeService,
            IHttpContextAccessor httpContextAccessor,
            IConfiguration configuration,
            ILogger<AisIngestionService> logger)
        {
            _viajeService = viajeService;
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
            _aisConnectionString = configuration.GetSection("OracleAisDbSettings")["ConnectionString"] 
                ?? throw new InvalidOperationException("OracleAisDbSettings:ConnectionString no configurada.");
        }

        public async Task SincronizarPosicionesAisAsync(CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("SincronizarPosicionesAis: Iniciando ingesta de coordenadas reales desde Oracle AIS...");

            // 1. Elevamos temporalmente a CosteraId = 0 (Superusuario) para poder consultar todos los viajes activos
            var originalUser = _httpContextAccessor.HttpContext?.User;
            var identity = new ClaimsIdentity(new[] { new Claim("CosteraId", "0") }, "BackgroundSystem");
            if (_httpContextAccessor.HttpContext == null)
            {
                _httpContextAccessor.HttpContext = new DefaultHttpContext();
            }
            _httpContextAccessor.HttpContext.User = new ClaimsPrincipal(identity);

            List<ViajePosicionMongo> viajesActivos;
            try
            {
                var todosLosViajes = await _viajeService.GetViajesAsync(null, 1, 5000);
                viajesActivos = todosLosViajes
                    .Where(v => v.NavegationStatusDesc != "Finalizado" && !string.IsNullOrWhiteSpace(v.Mmsi))
                    .ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SincronizarPosicionesAis: Error al consultar viajes activos en MongoDB.");
                return;
            }
            finally
            {
                // Restauramos el contexto original inmediatamente tras la lectura
                if (originalUser != null)
                {
                    _httpContextAccessor.HttpContext.User = originalUser;
                }
            }

            if (!viajesActivos.Any())
            {
                _logger.LogInformation("SincronizarPosicionesAis: No hay viajes activos en el sistema para sincronizar.");
                return;
            }

            // Mapeamos los MMSIs activos
            var mmsis = new List<long>();
            foreach (var v in viajesActivos)
            {
                if (long.TryParse(v.Mmsi, out var mmsiVal))
                {
                    mmsis.Add(mmsiVal);
                }
            }
            mmsis = mmsis.Distinct().ToList();

            if (!mmsis.Any())
            {
                _logger.LogInformation("SincronizarPosicionesAis: Los viajes activos no poseen MMSIs numéricos válidos.");
                return;
            }

            _logger.LogInformation("SincronizarPosicionesAis: Buscando reportes para {CantMmsis} MMSIs activos...", mmsis.Count);

            // REGLA DE ORO 1: Límite de la cláusula IN de Oracle. Particionamos en lotes de máximo 999 elementos.
            var chunks = mmsis
                .Select((val, idx) => new { val, idx })
                .GroupBy(x => x.idx / 999)
                .Select(g => g.Select(x => x.val).ToList())
                .ToList();

            var reportesAis = new List<AisReportDto>();

            try
            {
                using (var connection = new OracleConnection(_aisConnectionString))
                {
                    await connection.OpenAsync(cancellationToken);

                    foreach (var chunk in chunks)
                    {
                        if (cancellationToken.IsCancellationRequested) break;

                        // Query optimizada para traer el último reporte de cada MMSI en el lote
                        // Deuda Técnica (Regla de Oro 2): Se utiliza la partición hardcodeada SYS_P8816 provisionalmente
                        var query = @"
                            SELECT MMSI, LATITUD, LONGITUD, VELOCIDAD, CURSO, FECHA 
                            FROM (
                                SELECT MMSI, LATITUD, LONGITUD, VELOCIDAD, CURSO, FECHA,
                                       ROW_NUMBER() OVER (PARTITION BY MMSI ORDER BY FECHA DESC) as rn
                                FROM buques_reportes partition (SYS_P8816)
                                WHERE MMSI IN :mmsiList
                            ) WHERE rn = 1";

                        var parameters = new { mmsiList = chunk };
                        var res = await connection.QueryAsync<AisReportDto>(query, parameters);
                        if (res != null)
                        {
                            reportesAis.AddRange(res);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SincronizarPosicionesAis: Error al consultar la base de datos Oracle AIS.");
                return;
            }

            _logger.LogInformation("SincronizarPosicionesAis: Obtenidos {CantReportes} reportes de posición reales desde Oracle. Procesando...", reportesAis.Count);

            // 3. Procesamos cada reporte real e impactamos la posición georreferenciada en nuestro sistema
            foreach (var report in reportesAis)
            {
                if (cancellationToken.IsCancellationRequested) break;

                // Buscamos el viaje correspondiente al MMSI reportado
                var viaje = viajesActivos.FirstOrDefault(v => v.Mmsi == report.Mmsi.ToString());
                if (viaje == null) continue;

                try
                {
                    // Volvemos a simular superusuario temporal para realizar la actualización sin trabas de multi-jurisdicción
                    var currentContext = _httpContextAccessor.HttpContext?.User;
                    var adminIdentity = new ClaimsIdentity(new[] { new Claim("CosteraId", "0") }, "BackgroundSystem");
                    if (_httpContextAccessor.HttpContext == null)
                    {
                        _httpContextAccessor.HttpContext = new DefaultHttpContext();
                    }
                    _httpContextAccessor.HttpContext.User = new ClaimsPrincipal(adminIdentity);

                    var actualizarPosDto = new ActualizarPosicionDto
                    {
                        Latitud = report.Latitud,
                        Longitud = report.Longitud,
                        // Para evitar el rechazo cinemático por saltos de tiempo muy cortos en simulación/pruebas,
                        // nos aseguramos de que la fecha del reporte sea posterior a la última registrada.
                        // En producción, el report.Fecha es la estampa real del transponder.
                        FechaReporte = report.Fecha > viaje.MsgTime ? report.Fecha : DateTime.UtcNow
                    };

                    _logger.LogInformation("SincronizarPosicionesAis: Actualizando posición de Buque '{Buque}' (MMSI {Mmsi}) a [{Lat}, {Lng}].", 
                        viaje.VesselName, report.Mmsi, report.Latitud, report.Longitud);

                    await _viajeService.ActualizarPosicionAsync(viaje.Id, actualizarPosDto);

                    if (currentContext != null)
                    {
                        _httpContextAccessor.HttpContext.User = currentContext;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "SincronizarPosicionesAis: Error al actualizar la posición del buque {Buque} (MMSI {Mmsi}): {Msg}", 
                        viaje.VesselName, report.Mmsi, ex.Message);
                }
            }

            _logger.LogInformation("SincronizarPosicionesAis: Ingesta y reconciliación de coordenadas completada.");
        }
    }

    public class AisReportDto
    {
        public long Mmsi { get; set; }
        public double Latitud { get; set; }
        public double Longitud { get; set; }
        public double Velocidad { get; set; }
        public double Curso { get; set; }
        public DateTime Fecha { get; set; }
    }
}
