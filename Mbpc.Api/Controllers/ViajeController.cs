// Mbpc.Api/Controllers/ViajesController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Mbpc.Api.Services;
using Mbpc.Api.Models.Mongo;
using Mbpc.Api.DTOs;
using System.Security.Claims;

namespace Mbpc.Api.Controllers
{
    [ApiController]
    [Route("api/viajes")]
    [Authorize]
    public class ViajesController : ControllerBase
    {
        private readonly IViajeService             _viajeService;
        private readonly ILogger<ViajesController> _logger;

        public ViajesController(IViajeService viajeService, ILogger<ViajesController> logger)
        {
            _viajeService = viajeService;
            _logger       = logger;
        }

        [HttpGet]
        public async Task<ActionResult<List<ViajeDto>>> GetViajes(
            [FromQuery] string? nombre  = null,
            [FromQuery] int pagina      = 1,
            [FromQuery] int tamanio     = 50)
        {
            var costeraIdClaim = User.FindFirstValue("CosteraId");

            if (string.IsNullOrWhiteSpace(costeraIdClaim))
            {
                _logger.LogWarning(
                    "GetViajes rechazado: el token no contiene el Claim 'CosteraId'. Usuario: {User}",
                    User.Identity?.Name ?? "desconocido");
                return Forbid();
            }

            if (pagina < 1) pagina = 1;
            tamanio = Math.Clamp(tamanio, 1, 200);

            _logger.LogInformation(
                "GetViajes — CosteraId: {CosteraId} | Nombre: '{Nombre}' | Página: {Pagina} | Tamaño: {Tamanio}",
                costeraIdClaim, nombre ?? "TODOS", pagina, tamanio);

            // Controlador anoréxico: El mapeo ahora ocurre en el Service (Hito 5.8)
            var viajesDto = await _viajeService.ObtenerViajesDtoAsync(nombre, pagina, tamanio);

            return Ok(viajesDto);
        }

        [HttpGet("{mmsi}")]
        public async Task<ActionResult<ViajePosicionMongo>> GetViajeByMmsi(string mmsi)
        {
            var costeraIdClaim = User.FindFirstValue("CosteraId");
            if (string.IsNullOrWhiteSpace(costeraIdClaim)) return Forbid();

            if (string.IsNullOrWhiteSpace(mmsi))
                return BadRequest(new { mensaje = "El MMSI no puede estar vacío." });

            var viaje = await _viajeService.GetViajeByMmsiAsync(mmsi);

            if (viaje == null)
                return NotFound(new { mensaje = $"No se encontró posición para el buque con MMSI {mmsi}." });

            return Ok(viaje);
        }

        [HttpGet("puerto")]
        public async Task<ActionResult<List<BarcoPuertoDto>>> GetBarcosEnPuerto()
        {
            var costeraIdClaim = User.FindFirstValue("CosteraId");

            if (string.IsNullOrWhiteSpace(costeraIdClaim))
            {
                _logger.LogWarning(
                    "GetBarcosEnPuerto rechazado: el token no contiene el Claim 'CosteraId'. Usuario: {User}",
                    User.Identity?.Name ?? "desconocido");
                return Forbid();
            }

            _logger.LogInformation(
                "Consultando barcos en puerto — CosteraId: {CosteraId}", costeraIdClaim);

            var barcos = await _viajeService.GetBarcosEnPuertoAsync();
            return Ok(barcos);
        }

        [HttpGet("historico")]
        public async Task<ActionResult<List<ViajeHistoricoDto>>> GetHistorico(
            [FromQuery] string?   nombre    = null,
            [FromQuery] string?   omi       = null,
            [FromQuery] string?   matricula = null,
            [FromQuery] string?   origen    = null,
            [FromQuery] string?   destino   = null,
            [FromQuery] DateTime? desde     = null,
            [FromQuery] DateTime? hasta     = null)
        {
            var costeraIdClaim = User.FindFirstValue("CosteraId");

            if (string.IsNullOrWhiteSpace(costeraIdClaim))
            {
                _logger.LogWarning(
                    "GetHistorico rechazado: el token no contiene el Claim 'CosteraId'. Usuario: {User}",
                    User.Identity?.Name ?? "desconocido");
                return Forbid();
            }

            _logger.LogInformation(
                "Búsqueda histórica — CosteraId: {CosteraId} | Nombre:{Nombre} OMI:{Omi} Matrícula:{Matricula}",
                costeraIdClaim, nombre, omi, matricula);

            var filtro = new FiltroHistoricoDto
            {
                Nombre    = nombre,
                Omi       = omi,
                Matricula = matricula,
                Origen    = origen,
                Destino   = destino,
                Desde     = desde,
                Hasta     = hasta
            };

            var historico = await _viajeService.GetHistoricoAsync(filtro);
            return Ok(historico);
        }

        [HttpGet("mapa")]
        public async Task<ActionResult<List<MapaViajeDto>>> GetMapaViajes(
            [FromQuery] string? mmsi        = null,
            [FromQuery] string? nombreBuque = null)
        {
            var costeraIdClaim = User.FindFirstValue("CosteraId");

            if (string.IsNullOrWhiteSpace(costeraIdClaim))
            {
                _logger.LogWarning(
                    "GetMapaViajes rechazado: el token no contiene el Claim 'CosteraId'. Usuario: {User}",
                    User.Identity?.Name ?? "desconocido");
                return Forbid();
            }

            _logger.LogInformation(
                "Consulta mapa AIS — CosteraId: '{CosteraId}' | MMSI: '{Mmsi}' | Nombre: '{Nombre}'",
                costeraIdClaim, mmsi ?? "TODOS", nombreBuque ?? "TODOS");

            var puntos = await _viajeService.GetMapaViajesAsync(mmsi, nombreBuque);
            return Ok(puntos);
        }

        [HttpPost]
        public async Task<ActionResult> IniciarViaje([FromBody] NuevoViajeDto nuevoViaje)
        {
            if (!ModelState.IsValid)
            {
                _logger.LogWarning(
                    "IniciarViaje rechazado por ModelState inválido. Errores: {@Errors}",
                    ModelState.Values
                              .SelectMany(v => v.Errors)
                              .Select(e => e.ErrorMessage));

                return ValidationProblem(ModelState);
            }

            var costeraIdClaim = User.FindFirstValue("CosteraId");

            if (string.IsNullOrWhiteSpace(costeraIdClaim))
            {
                _logger.LogWarning(
                    "IniciarViaje rechazado: el token no contiene el Claim 'CosteraId'. Usuario: {User}",
                    User.Identity?.Name ?? "desconocido");
                return Forbid();
            }

            nuevoViaje.CosteraId = costeraIdClaim;

            _logger.LogInformation(
                "IniciarViaje — BuqueId: '{BuqueId}' | Origen: '{Origen}' | Destino: '{Destino}' | Latitud: {Latitud} | Longitud: {Longitud} | CosteraId: {CosteraId}",
                nuevoViaje.BuqueId, nuevoViaje.Origen, nuevoViaje.Destino, nuevoViaje.Latitud, nuevoViaje.Longitud, costeraIdClaim);

            var exito = await _viajeService.IniciarViajeAsync(nuevoViaje);

            if (!exito)
            {
                _logger.LogError(
                    "IniciarViajeAsync retornó false para BuqueId: '{BuqueId}' CosteraId: {CosteraId}.",
                    nuevoViaje.BuqueId, costeraIdClaim);

                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    mensaje = "Error interno al procesar el inicio de viaje. Intente nuevamente."
                });
            }

            return Ok(new
            {
                mensaje       = $"Viaje para el buque con Id '{nuevoViaje.BuqueId}' iniciado correctamente con estado 'Amarrado'.",
                buqueId       = nuevoViaje.BuqueId,
                origen        = nuevoViaje.Origen,
                destino       = nuevoViaje.Destino,
                estadoInicial = "Amarrado"
            });
        }

        [HttpPut("{id}/zarpar")]
        public async Task<ActionResult> ZarparViaje(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return BadRequest(new { mensaje = "El ID del viaje/buque es requerido." });

            _logger.LogInformation("ZARPAR recibido para: {Id}", id);

            var exito = await _viajeService.ZarparAsync(id);
            if (!exito)
                return UnprocessableEntity(new { mensaje = $"No se pudo zarpar el buque '{id}'. Verifique que exista y que su estado actual permita esta transición (No puede estar Fondeado sin antes Reanudar)." });

            return Ok(new { mensaje = $"Buque '{id}' zarpó. Estado → 'Navegando'." });
        }

        [HttpPut("{id}/amarrar")]
        public async Task<ActionResult> AmarrarViaje(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return BadRequest(new { mensaje = "El ID del viaje/buque es requerido." });

            _logger.LogInformation("AMARRAR VIAJE recibido para: {Id}", id);

            var exito = await _viajeService.AmarrarViajeAsync(id);
            if (!exito)
                return UnprocessableEntity(new { mensaje = $"No se pudo amarrar el buque '{id}'. Verifique que el estado actual permita esta transición." });

            return Ok(new { mensaje = $"Buque '{id}' amarrado. Estado → 'Amarrado'." });
        }

        [HttpPut("{id}/fondear")]
        public async Task<ActionResult> FondearViaje(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return BadRequest(new { mensaje = "El ID del viaje/buque es requerido." });

            _logger.LogInformation("FONDEAR VIAJE recibido para: {Id}", id);

            var exito = await _viajeService.FondearViajeAsync(id);
            if (!exito)
                return UnprocessableEntity(new { mensaje = $"No se pudo fondear el buque '{id}'. Verifique que el estado actual permita esta transición." });

            return Ok(new { mensaje = $"Buque '{id}' fondeado. Estado → 'Fondeado'." });
        }

        [HttpPut("{id}/reanudar")]
        public async Task<ActionResult> ReanudarViaje(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return BadRequest(new { mensaje = "El ID del viaje/buque es requerido." });

            _logger.LogInformation("REANUDAR VIAJE recibido para: {Id}", id);

            var exito = await _viajeService.ReanudarViajeAsync(id);
            if (!exito)
                return UnprocessableEntity(new { mensaje = $"No se pudo reanudar el buque '{id}'. El buque debe estar en estado 'Fondeado' para ejecutar esta acción." });

            return Ok(new { mensaje = $"Buque '{id}' reanudado. Estado → 'Reanudado'." });
        }

        [HttpPut("{id}/posicion")]
        public async Task<ActionResult> ActualizarPosicion(
            string id,
            [FromBody] ActualizarPosicionDto dto)
        {
            if (!ModelState.IsValid)
            {
                _logger.LogWarning(
                    "ActualizarPosicion rechazado por ModelState inválido para Id: {Id}. Errores: {@Errors}",
                    id,
                    ModelState.Values
                              .SelectMany(v => v.Errors)
                              .Select(e => e.ErrorMessage));

                return ValidationProblem(ModelState);
            }

            var costeraIdClaim = User.FindFirstValue("CosteraId");

            if (string.IsNullOrWhiteSpace(costeraIdClaim))
            {
                _logger.LogWarning(
                    "ActualizarPosicion rechazado: token sin Claim 'CosteraId'. Usuario: {User}",
                    User.Identity?.Name ?? "desconocido");
                return Forbid();
            }

            if (string.IsNullOrWhiteSpace(id))
                return BadRequest(new { mensaje = "El ID del viaje es requerido." });

            if (dto.FechaReporte > DateTime.UtcNow.AddMinutes(5))
            {
                return BadRequest(new
                {
                    mensaje = $"FechaReporte ({dto.FechaReporte:O}) es futura. Verificá el transponder AIS."
                });
            }

            _logger.LogInformation(
                "ActualizarPosicion — Id: '{Id}' | Lat: {Lat} | Lng: {Lng} | FechaReporte: {Fecha} | CosteraId: {CosteraId}",
                id, dto.Latitud, dto.Longitud, dto.FechaReporte, costeraIdClaim);

            PosicionActualizadaResultDto? resultado;

            try
            {
                resultado = await _viajeService.ActualizarPosicionAsync(id, dto);
            }
            catch (InvalidOperationException ex) when (ex.Message.StartsWith("Cinemática inválida"))
            {
                _logger.LogWarning(
                    "ActualizarPosicion bloqueada por cinemática inválida para Id: '{Id}'. Detalle: {Msg}",
                    id, ex.Message);

                return BadRequest(new { mensaje = ex.Message });
            }

            if (resultado == null)
            {
                _logger.LogError(
                    "ActualizarPosicionAsync retornó null para Id: '{Id}' CosteraId: {CosteraId}.",
                    id, costeraIdClaim);

                return NotFound(new
                {
                    mensaje = $"No se encontró el viaje con Id '{id}' para la costera {costeraIdClaim}."
                });
            }

            return Ok(new
            {
                mensaje              = $"Posición del buque '{resultado.VesselName}' actualizada correctamente.",
                vesselName           = resultado.VesselName,
                latitud              = resultado.Latitud,
                longitud             = resultado.Longitud,
                velocidadCalculadaKn = resultado.VelocidadCalculadaKn,
                distanciaRecorridaNM = resultado.DistanciaRecorridaNM,
                tracklogId           = resultado.TracklogId,
                fechaReporte         = dto.FechaReporte,
            });
        }
    }
}