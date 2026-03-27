using Microsoft.AspNetCore.Mvc;
using Mbpc.Api.Services;
using Mbpc.Api.Models.Mongo;
using Mbpc.Api.DTOs;

namespace Mbpc.Api.Controllers
{
    [ApiController]
    [Route("api/viajes")]
    public class ViajesController : ControllerBase
    {
        private readonly IViajeService            _viajeService;
        private readonly ILogger<ViajesController> _logger;

        public ViajesController(IViajeService viajeService, ILogger<ViajesController> logger)
        {
            _viajeService = viajeService;
            _logger       = logger;
        }

        // ── GETs de lectura ───────────────────────────────────────────────────

        /// <summary>
        /// Lista paginada de posiciones activas desde MongoDB (max 200 por página).
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<List<ViajeDto>>> GetViajes(
            [FromQuery] int pagina  = 1,
            [FromQuery] int tamanio = 50)
        {
            if (pagina < 1) pagina = 1;
            tamanio = Math.Clamp(tamanio, 1, 200);

            var posicionesMongo = await _viajeService.GetViajesAsync(pagina, tamanio);

            var viajesDto = posicionesMongo.Select(p => new ViajeDto
            {
                Id                   = p.Id,
                Buque                = p.VesselName ?? "DESCONOCIDO",
                Ruta                 = $"{p.Origin ?? "Sin Origen"} ➔ {p.Destination ?? "Sin Destino"} | Pos: {Math.Round(p.Latitude, 4)}, {Math.Round(p.Longitude, 4)}",
                FechaInicioFormateada = p.MsgTime.ToString("dd/MM/yyyy HH:mm"),
                EstadoActual         = p.NavegationStatusDesc ?? "N/A"
            }).ToList();

            return Ok(viajesDto);
        }

        /// <summary>
        /// Busca un buque específico por MMSI.
        /// </summary>
        [HttpGet("{mmsi}")]
        public async Task<ActionResult<ViajePosicionMongo>> GetViajeByMmsi(string mmsi)
        {
            if (string.IsNullOrWhiteSpace(mmsi))
                return BadRequest(new { mensaje = "El MMSI no puede estar vacío." });

            var viaje = await _viajeService.GetViajeByMmsiAsync(mmsi);

            if (viaje == null)
                return NotFound(new { mensaje = $"No se encontró posición para el buque con MMSI {mmsi}." });

            return Ok(viaje);
        }

        /// <summary>
        /// Barcos actualmente en puerto (filtro por estado de navegación + datos Oracle).
        /// </summary>
        [HttpGet("puerto")]
        public async Task<ActionResult<List<BarcoPuertoDto>>> GetBarcosEnPuerto()
        {
            _logger.LogInformation("Consultando barcos en puerto.");
            var barcos = await _viajeService.GetBarcosEnPuertoAsync();
            return Ok(barcos);
        }

        /// <summary>
        /// Búsqueda de viajes históricos por criterios múltiples. Fuente: Oracle.
        /// </summary>
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
            _logger.LogInformation(
                "Búsqueda histórica — Nombre:{Nombre} OMI:{Omi} Matrícula:{Matricula}",
                nombre, omi, matricula);

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

        // ── Endpoint del Mapa AIS ───────────────────────────────────

        /// <summary>
        /// Retorna los puntos GeoJSON-ready para el mapa ArcGIS.
        /// </summary>
        [HttpGet("mapa")]
        public async Task<ActionResult<List<MapaViajeDto>>> GetMapaViajes(
            [FromQuery] string? mmsi        = null,
            [FromQuery] string? nombreBuque = null)
        {
            _logger.LogInformation(
                "Consulta mapa AIS — MMSI: '{Mmsi}' | Nombre: '{Nombre}'",
                mmsi ?? "TODOS", nombreBuque ?? "TODOS");

            var puntos = await _viajeService.GetMapaViajesAsync(mmsi, nombreBuque);
            return Ok(puntos);
        }

        // ── POSTs y PUTs de escritura (CQRS + MÁQUINA DE ESTADOS) ─────────────

        /// <summary>
        /// Inicia un nuevo viaje (escribe en Oracle + inserta en MongoDB).
        /// El buque nace con estado "Amarrado" según regla de negocio.
        /// </summary>
        [HttpPost]
        public async Task<ActionResult> IniciarViaje([FromBody] NuevoViajeDto nuevoViaje)
        {
            _logger.LogInformation("Despacho para buque: {Buque}", nuevoViaje?.NombreBuque);

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