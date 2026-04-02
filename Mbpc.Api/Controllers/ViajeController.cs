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

        // ── GETs de lectura ───────────────────────────────────────────────────

        /// <summary>
        /// Lista paginada de posiciones activas desde MongoDB (max 200 por página).
        /// El filtrado multitenant por CosteraId se resuelve internamente en el servicio
        /// a partir del Claim del JWT; el Controller solo valida que el Claim exista.
        /// </summary>
        [HttpGet]
        [Authorize]
        public async Task<ActionResult<List<ViajeDto>>> GetViajes(
            [FromQuery] int pagina  = 1,
            [FromQuery] int tamanio = 50)
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
                "GetViajes — CosteraId: {CosteraId} | Página: {Pagina} | Tamaño: {Tamanio}",
                costeraIdClaim, pagina, tamanio);

            // El costeraId ya NO se pasa: el servicio lo resuelve internamente.
            var posicionesMongo = await _viajeService.GetViajesAsync(pagina, tamanio);

            var viajesDto = posicionesMongo.Select(p => new ViajeDto
            {
                Id                    = p.Id,
                Buque                 = p.VesselName ?? "DESCONOCIDO",
                Ruta                  = $"{p.Origin ?? "Sin Origen"} ➔ {p.Destination ?? "Sin Destino"} | Pos: {Math.Round(p.Latitude, 4)}, {Math.Round(p.Longitude, 4)}",
                FechaInicioFormateada = p.MsgTime.ToString("dd/MM/yyyy HH:mm"),
                EstadoActual          = p.NavegationStatusDesc ?? "N/A"
            }).ToList();

            return Ok(viajesDto);
        }

        /// <summary>
        /// Busca un buque específico por MMSI.
        /// El filtro de costera se aplica internamente en el servicio.
        /// </summary>
        [HttpGet("{mmsi}")]
        [Authorize]
        public async Task<ActionResult<ViajePosicionMongo>> GetViajeByMmsi(string mmsi)
        {
            var costeraIdClaim = User.FindFirstValue("CosteraId");
            if (string.IsNullOrWhiteSpace(costeraIdClaim)) return Forbid();

            if (string.IsNullOrWhiteSpace(mmsi))
                return BadRequest(new { mensaje = "El MMSI no puede estar vacío." });

            // El costeraId ya NO se pasa: el servicio lo resuelve internamente.
            var viaje = await _viajeService.GetViajeByMmsiAsync(mmsi);

            if (viaje == null)
                return NotFound(new { mensaje = $"No se encontró posición para el buque con MMSI {mmsi}." });

            return Ok(viaje);
        }

        /// <summary>
        /// Barcos actualmente en puerto (filtro por estado de navegación + datos Oracle).
        /// El filtro de costera se aplica internamente en el servicio.
        /// </summary>
        [HttpGet("puerto")]
        [Authorize]
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

            // El costeraId ya NO se pasa: el servicio lo resuelve internamente.
            var barcos = await _viajeService.GetBarcosEnPuertoAsync();
            return Ok(barcos);
        }

        /// <summary>
        /// Búsqueda de viajes históricos por criterios múltiples. Fuente: Oracle.
        /// El filtro de costera se aplica internamente en el servicio.
        /// </summary>
        [HttpGet("historico")]
        [Authorize]
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

            // El costeraId ya NO se pasa: el servicio lo resuelve internamente.
            var historico = await _viajeService.GetHistoricoAsync(filtro);
            return Ok(historico);
        }

        // ── Endpoint del Mapa AIS ─────────────────────────────────────────────

        /// <summary>
        /// Retorna los puntos GeoJSON-ready para el mapa ArcGIS.
        /// El filtro multitenant por CosteraId se aplica internamente en el servicio.
        /// </summary>
        [HttpGet("mapa")]
        [Authorize]
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

            // El costeraId ya NO se pasa: el servicio lo resuelve internamente.
            var puntos = await _viajeService.GetMapaViajesAsync(mmsi, nombreBuque);
            return Ok(puntos);
        }

        // ── POSTs y PUTs de escritura (CQRS + MÁQUINA DE ESTADOS) ─────────────

        /// <summary>
        /// Inicia un nuevo viaje (escribe en Oracle + inserta en MongoDB).
        /// El buque nace con estado "Amarrado" según regla de negocio.
        /// El CosteraId se inyecta en el DTO desde el Claim del token.
        /// </summary>
        [HttpPost]
        [Authorize]
        public async Task<ActionResult> IniciarViaje([FromBody] NuevoViajeDto nuevoViaje)
        {
            var costeraIdClaim = User.FindFirstValue("CosteraId");

            if (string.IsNullOrWhiteSpace(costeraIdClaim))
            {
                _logger.LogWarning(
                    "IniciarViaje rechazado: el token no contiene el Claim 'CosteraId'. Usuario: {User}",
                    User.Identity?.Name ?? "desconocido");
                return Forbid();
            }

            _logger.LogInformation(
                "Despacho para buque: {Buque} | CosteraId: {CosteraId}",
                nuevoViaje?.NombreBuque, costeraIdClaim);

            if (nuevoViaje == null
                || string.IsNullOrWhiteSpace(nuevoViaje.NombreBuque)
                || string.IsNullOrWhiteSpace(nuevoViaje.Origen)
                || string.IsNullOrWhiteSpace(nuevoViaje.Destino))
            {
                return BadRequest(new
                {
                    mensaje = "Todos los campos son requeridos: NombreBuque, Origen y Destino."
                });
            }

            // El Controller sigue siendo responsable de inyectar el CosteraId en el DTO
            // para que el registro en MongoDB quede correctamente etiquetado con su costera.
            nuevoViaje.CosteraId = costeraIdClaim;

            var exito = await _viajeService.IniciarViajeAsync(nuevoViaje);

            if (!exito)
                return StatusCode(500, new { mensaje = "Error interno al procesar el inicio de viaje." });

            return Ok(new { mensaje = $"Viaje para {nuevoViaje.NombreBuque} iniciado correctamente con estado 'Amarrado'." });
        }

        /// <summary>
        /// Zarpa el buque → NavegationStatusDesc = "Navegando".
        /// Valida que el estado actual permita zarpar (No puede estar Fondeado).
        /// </summary>
        [HttpPut("{id}/zarpar")]
        [Authorize]
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

        /// <summary>
        /// Amarra el buque → NavegationStatusDesc = "Amarrado".
        /// </summary>
        [HttpPut("{id}/amarrar")]
        [Authorize]
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

        /// <summary>
        /// Fondea el buque → NavegationStatusDesc = "Fondeado".
        /// </summary>
        [HttpPut("{id}/fondear")]
        [Authorize]
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

        /// <summary>
        /// Reanuda el buque → NavegationStatusDesc = "Reanudado".
        /// Paso previo obligatorio para zarpar desde un estado de fondeo.
        /// </summary>
        [HttpPut("{id}/reanudar")]
        [Authorize]
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
    }
}
