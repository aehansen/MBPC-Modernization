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
        private readonly IViajeService _viajeService;
        private readonly ILogger<ViajesController> _logger;

        public ViajesController(IViajeService viajeService, ILogger<ViajesController> logger)
        {
            _viajeService = viajeService;
            _logger = logger;
        }

        /// <summary>
        /// Retorna la lista de posiciones activas desde MongoDB (last_mbpc).
        /// Limitado a 200 registros para evitar traer toda la colección.
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<List<ViajeDto>>> GetViajes(
            [FromQuery] int pagina = 1,
            [FromQuery] int tamanio = 50)
        {
            // Validamos parámetros de paginación
            if (pagina < 1) pagina = 1;
            tamanio = Math.Clamp(tamanio, 1, 200);

            var posicionesMongo = await _viajeService.GetViajesAsync(pagina, tamanio);

            var viajesDto = posicionesMongo.Select(p => new ViajeDto
            {
                Id            = p.Id,
                Buque         = p.VesselName ?? "DESCONOCIDO",
                // ¡ACÁ ESTÁ LA MAGIA! Concatenamos Origen, Destino y Coordenadas
                Ruta          = $"{p.Origin ?? "Sin Origen"} ➔ {p.Destination ?? "Sin Destino"} | Pos: {Math.Round(p.Latitude, 4)}, {Math.Round(p.Longitude, 4)}",
                FechaInicioFormateada = p.MsgTime.ToString("dd/MM/yyyy HH:mm"),
                EstadoActual  = p.NavegationStatusDesc ?? "N/A"
            }).ToList();

            return Ok(viajesDto);
        }

        /// <summary>
        /// Busca un buque específico por su MMSI.
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
        /// Retorna los barcos actualmente en puerto.
        /// Datos filtrados desde MongoDB según estado de navegación, complementados con datos operativos de Oracle.
        /// </summary>
        [HttpGet("puerto")]
        public async Task<ActionResult<List<BarcoPuertoDto>>> GetBarcosEnPuerto()
        {
            _logger.LogInformation("Consultando barcos en puerto.");
            var barcos = await _viajeService.GetBarcosEnPuertoAsync();
            return Ok(barcos);
        }

        /// <summary>
        /// Busca viajes históricos por criterios múltiples (nombre, OMI, matrícula, etc.).
        /// Consulta Oracle para datos de archivo.
        /// </summary>
        [HttpGet("historico")]
        public async Task<ActionResult<List<ViajeHistoricoDto>>> GetHistorico(
            [FromQuery] string? nombre     = null,
            [FromQuery] string? omi        = null,
            [FromQuery] string? matricula  = null,
            [FromQuery] string? origen     = null,
            [FromQuery] string? destino    = null,
            [FromQuery] DateTime? desde    = null,
            [FromQuery] DateTime? hasta    = null)
        {
            _logger.LogInformation(
                "Búsqueda histórica — Nombre: {Nombre} | OMI: {Omi} | Matrícula: {Matricula} | Origen: {Origen} | Destino: {Destino}",
                nombre, omi, matricula, origen, destino);

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

        /// <summary>
        /// Inicia un nuevo viaje registrando los datos en Oracle.
        /// NOTA CQRS: la escritura va a Oracle; la lectura viene de Mongo.
        /// El frontend verá el nuevo viaje en la grilla de inmediato gracias al Insert en Mongo.
        /// El buque nace con estado "Amarrado" según regla de negocio.
        /// </summary>
        [HttpPost]
        public async Task<ActionResult> IniciarViaje([FromBody] NuevoViajeDto nuevoViaje)
        {
            _logger.LogInformation(
                "Recibida petición de despacho para buque: {Buque}",
                nuevoViaje?.NombreBuque);

            // Validación completa de todos los campos requeridos por el SP
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

        // ── TAREA 1: ENDPOINTS DE CAMBIO DE ESTADO DEL BUQUE ────────────────

        /// <summary>
        /// Hace zarpar el buque: cambia NavegationStatusDesc a "Navegando" en MongoDB.
        /// CQRS: simula bypass a Oracle. La operación real se registraría en PKG_MBPC_VIAJES.SP_ZARPAR.
        /// </summary>
        [HttpPut("{id}/zarpar")]
        public async Task<ActionResult> ZarparViaje(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return BadRequest(new { mensaje = "El ID del viaje/buque es requerido." });

            _logger.LogInformation("Solicitud de ZARPAR recibida para: {Id}", id);

            var exito = await _viajeService.ZarparAsync(id);
            if (!exito)
                return NotFound(new { mensaje = $"No se encontró el buque/viaje con ID '{id}' en MongoDB." });

            return Ok(new { mensaje = $"Buque '{id}' zarpó correctamente. Estado actualizado a 'Navegando'." });
        }

        /// <summary>
        /// Amarra el buque en puerto: cambia NavegationStatusDesc a "Amarrado" en MongoDB.
        /// CQRS: simula bypass a Oracle. La operación real se registraría en PKG_MBPC_VIAJES.SP_AMARRAR_VIAJE.
        /// </summary>
        [HttpPut("{id}/amarrar")]
        public async Task<ActionResult> AmarrarViaje(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return BadRequest(new { mensaje = "El ID del viaje/buque es requerido." });

            _logger.LogInformation("Solicitud de AMARRAR VIAJE recibida para: {Id}", id);

            var exito = await _viajeService.AmarrarViajeAsync(id);
            if (!exito)
                return NotFound(new { mensaje = $"No se encontró el buque/viaje con ID '{id}' en MongoDB." });

            return Ok(new { mensaje = $"Buque '{id}' amarrado correctamente. Estado actualizado a 'Amarrado'." });
        }

        /// <summary>
        /// Fondea el buque: cambia NavegationStatusDesc a "Fondeado" en MongoDB.
        /// CQRS: simula bypass a Oracle. La operación real se registraría en PKG_MBPC_VIAJES.SP_FONDEAR_VIAJE.
        /// </summary>
        [HttpPut("{id}/fondear")]
        public async Task<ActionResult> FondearViaje(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return BadRequest(new { mensaje = "El ID del viaje/buque es requerido." });

            _logger.LogInformation("Solicitud de FONDEAR VIAJE recibida para: {Id}", id);

            var exito = await _viajeService.FondearViajeAsync(id);
            if (!exito)
                return NotFound(new { mensaje = $"No se encontró el buque/viaje con ID '{id}' en MongoDB." });

            return Ok(new { mensaje = $"Buque '{id}' fondeado correctamente. Estado actualizado a 'Fondeado'." });
        }
    }
}
