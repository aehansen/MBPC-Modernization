using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using Mbpc.Api.DTOs;
using Mbpc.Api.Services;
using Microsoft.Extensions.Logging;

namespace Mbpc.Api.Controllers
{
    [ApiController]
    [Route("api/simulador")]
    [AllowAnonymous]
    public class SimuladorController : ControllerBase
    {
        private readonly IViajeService _viajeService;
        private readonly IReconciliacionService _reconciliacionService;
        private readonly IAisIngestionService _aisIngestionService;
        private readonly ILogger<SimuladorController> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;

        private const string OracleAisConnectionString = "User Id=AISC;Password=AISCbu2016;Data Source=(DESCRIPTION =(LOAD_BALANCE = ON)(ADDRESS = (PROTOCOL = TCP)(HOST = exa1-scan-01)(PORT = 1521))(CONNECT_DATA =(SERVER = DEDICATED)(SERVICE_NAME = svc_bp)));";

        public SimuladorController(
            IViajeService viajeService,
            IReconciliacionService reconciliacionService,
            IAisIngestionService aisIngestionService,
            IHttpContextAccessor httpContextAccessor,
            ILogger<SimuladorController> logger)
        {
            _viajeService = viajeService;
            _reconciliacionService = reconciliacionService;
            _aisIngestionService = aisIngestionService;
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
        }

        /// <summary>
        /// Testea la conectividad con la base de datos Oracle AIS y retorna las primeras 5 filas.
        /// </summary>
        [HttpGet("test-conexion-ais")]
        public async Task<IActionResult> TestConexionAis()
        {
            _logger.LogInformation("Simulador: Probando conexión con Oracle AIS...");
            var resultados = new List<Dictionary<string, object>>();

            try
            {
                using (var connection = new OracleConnection(OracleAisConnectionString))
                {
                    await connection.OpenAsync();
                    _logger.LogInformation("Simulador: ¡Conexión con Oracle AIS abierta con éxito!");

                    using (var command = connection.CreateCommand())
                    {
                        // Traemos solo 5 registros para analizar las columnas y datos sin saturar
                        command.CommandText = "SELECT * FROM buques_reportes partition (SYS_P8816) FETCH FIRST 5 ROWS ONLY";
                        
                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                var fila = new Dictionary<string, object>();
                                for (int i = 0; i < reader.FieldCount; i++)
                                {
                                    var nombreColumna = reader.GetName(i);
                                    var valor = reader.IsDBNull(i) ? null : reader.GetValue(i);
                                    fila[nombreColumna] = valor ?? "NULL";
                                }
                                resultados.Add(fila);
                            }
                        }
                    }
                }

                return Ok(new
                {
                    conexionOk = true,
                    mensaje = "Conexión exitosa a Oracle AIS",
                    filasTraidas = resultados.Count,
                    datosMuestra = resultados
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Simulador: Error al intentar conectar a Oracle AIS");
                return StatusCode(500, new
                {
                    conexionOk = false,
                    error = ex.Message,
                    tipoExcepcion = ex.GetType().FullName,
                    stackTrace = ex.StackTrace
                });
            }
        }

        [HttpPost("iniciar-viaje-prueba")]
        public async Task<IActionResult> IniciarViajePrueba([FromBody] NuevoViajePruebaDto dto)
        {
            _logger.LogInformation($"Iniciando viaje de prueba {dto.NombreBuque}");
            
            var nuevoViajeDto = new NuevoViajeDto
            {
                BuqueId = dto.TravelId,
                NombreBuque = dto.NombreBuque ?? "MOCK_SHIP_TEST",
                Mmsi = dto.Mmsi,
                Origen = "Gualeguaychú",
                Destino = "Buenos Aires",
                FechaPartida = DateTime.UtcNow,
                ETA = DateTime.UtcNow.AddDays(2),
                Latitud = (decimal)dto.Latitud,
                Longitud = (decimal)dto.Longitud,
                CosteraId = dto.CosteraIdInicial.ToString(),
                DeclaracionMalvinas = DeclaracionMalvinasEnum.NoVaAMalvinas_NoPresentoDeclaracion_N,
                ProximoPuntoControl = "Proximo Punto de Control"
            };

            bool exito = await _viajeService.IniciarViajeAsync(nuevoViajeDto);
            if (!exito)
            {
                return StatusCode(500, new { mensaje = "No se pudo iniciar el viaje de prueba en base de datos." });
            }

            // Elevar privilegios a CosteraId = 0 para buscar el viaje sin importar el token actual
            var originalUser = _httpContextAccessor.HttpContext?.User;
            var identity = new System.Security.Claims.ClaimsIdentity(new[] { new System.Security.Claims.Claim("CosteraId", "0") }, "BackgroundSystem");
            if (_httpContextAccessor.HttpContext == null)
            {
                _httpContextAccessor.HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext();
            }
            _httpContextAccessor.HttpContext.User = new System.Security.Claims.ClaimsPrincipal(identity);

            long travelIdReal = dto.TravelId;
            try
            {
                var viajes = await _viajeService.GetViajesAsync(nuevoViajeDto.NombreBuque, 1, 1);
                var viajeCreado = viajes.FirstOrDefault();
                if (viajeCreado != null)
                {
                    travelIdReal = viajeCreado.TravelId;
                }
            }
            finally
            {
                if (originalUser != null)
                {
                    _httpContextAccessor.HttpContext.User = originalUser;
                }
            }

            return Ok(new { mensaje = "Viaje de prueba iniciado con éxito.", travelId = travelIdReal });
        }

        [HttpPost("simular-movimiento")]
        public async Task<IActionResult> SimularMovimiento([FromBody] SimularMovimientoDto dto)
        {
            var actualizarPosDto = new ActualizarPosicionDto
            {
                Latitud = dto.Latitud,
                Longitud = dto.Longitud,
                // Avanzamos el reloj 5 horas para que la velocidad calculada (Distancia / Tiempo)
                // no supere el límite cinemático de 60 nudos ante un salto geográfico grande.
                FechaReporte = DateTime.UtcNow.AddHours(5)
            };

            await _viajeService.ActualizarPosicionAsync(dto.TravelId.ToString(), actualizarPosDto);
            
            // Forzar el cruce geoespacial inyectando el IReconciliacionService
            bool reconciliado = await _reconciliacionService.ForzarReconciliacionViajeAsync(dto.TravelId);

            return Ok(new
            {
                mensaje = "Movimiento simulado correctamente",
                reconciliado = reconciliado
            });
        }
        
        [HttpPost("ejecutar-reconciliacion")]
        public async Task<IActionResult> EjecutarReconciliacionManual()
        {
            await _reconciliacionService.EjecutarCicloReconciliacionAsync();
            return Ok(new { mensaje = "Reconciliación global disparada manualmente." });
        }

        [HttpPost("ejecutar-ingesta-ais")]
        public async Task<IActionResult> EjecutarIngestaAisManual()
        {
            await _aisIngestionService.SincronizarPosicionesAisAsync();
            return Ok(new { mensaje = "Ingesta de datos AIS disparada manualmente." });
        }
    }

    public class NuevoViajePruebaDto
    {
        public long TravelId { get; set; }
        public string? NombreBuque { get; set; }
        public string? Mmsi { get; set; }
        public double Latitud { get; set; }
        public double Longitud { get; set; }
        public int CosteraIdInicial { get; set; }
    }

    public class SimularMovimientoDto
    {
        public long TravelId { get; set; }
        public double Latitud { get; set; }
        public double Longitud { get; set; }
    }
}
