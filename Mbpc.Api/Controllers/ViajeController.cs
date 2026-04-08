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

        /// <summary>
        /// Barcos actualmente en puerto (filtro por estado de navegación + datos Oracle).
        /// El filtro de costera se aplica internamente en el servicio.
        /// </summary>
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

        /// <summary>
        /// Búsqueda de viajes históricos por criterios múltiples. Fuente: Oracle.
        /// El filtro de costera se aplica internamente en el servicio.
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

        // ── Endpoint del Mapa AIS ─────────────────────────────────────────────

        /// <summary>
        /// Retorna los puntos GeoJSON-ready para el mapa ArcGIS.
        /// El filtro multitenant por CosteraId se aplica internamente en el servicio.
        /// </summary>
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

        // ── POSTs y PUTs de escritura (CQRS + MÁQUINA DE ESTADOS) ─────────────

        /// <summary>
        /// Inicia un nuevo viaje (escribe en Oracle + inserta en MongoDB y details_mbpc).
        /// El buque nace con estado "Amarrado" según regla de negocio.
        /// El CosteraId se inyecta en el DTO desde el Claim del token.
        ///
        /// Responsabilidades del Controller (y solo estas):
        ///   1. Validar ModelState (DataAnnotations del DTO).
        ///   2. Verificar que el Claim CosteraId exista en el JWT.
        ///   3. Inyectar el CosteraId en el DTO antes de delegar.
        ///   4. Delegar a IViajeService y traducir el resultado a HTTP.
        /// </summary>
        [HttpPost]
        public async Task<ActionResult> IniciarViaje([FromBody] NuevoViajeDto nuevoViaje)
        {
            // ── 1. Validación de ModelState (DataAnnotations) ─────────────────
            // [ApiController] devuelve 400 automáticamente si ModelState es inválido,
            // pero lo dejamos explícito para mayor claridad y trazabilidad en los logs.
            if (!ModelState.IsValid)
            {
                _logger.LogWarning(
                    "IniciarViaje rechazado por ModelState inválido. Errores: {@Errors}",
                    ModelState.Values
                              .SelectMany(v => v.Errors)
                              .Select(e => e.ErrorMessage));

                return ValidationProblem(ModelState);
            }

            // ── 2. Validación del Claim de identidad multitenant ──────────────
            var costeraIdClaim = User.FindFirstValue("CosteraId");

            if (string.IsNullOrWhiteSpace(costeraIdClaim))
            {
                _logger.LogWarning(
                    "IniciarViaje rechazado: el token no contiene el Claim 'CosteraId'. Usuario: {User}",
                    User.Identity?.Name ?? "desconocido");
                return Forbid();
            }

            // ── 3. Inyección del CosteraId en el DTO ─────────────────────────
            // El Controller es el único punto donde CosteraId se extrae del contexto HTTP
            // y se asigna al DTO. El Service NUNCA debe leer el Claim para este flujo
            // de escritura; ya llega correctamente etiquetado en el DTO.
            nuevoViaje.CosteraId = costeraIdClaim;

            _logger.LogInformation(
                "IniciarViaje — Buque: '{Buque}' | Origen: '{Origen}' | Destino: '{Destino}' | CosteraId: {CosteraId}",
                nuevoViaje.NombreBuque, nuevoViaje.Origen, nuevoViaje.Destino, costeraIdClaim);

            // ── 4. Delegación total al Service ────────────────────────────────
            var exito = await _viajeService.IniciarViajeAsync(nuevoViaje);

            if (!exito)
            {
                _logger.LogError(
                    "IniciarViajeAsync retornó false para Buque: '{Buque}' CosteraId: {CosteraId}.",
                    nuevoViaje.NombreBuque, costeraIdClaim);

                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    mensaje = "Error interno al procesar el inicio de viaje. Intente nuevamente."
                });
            }

            return Ok(new
            {
                mensaje   = $"Viaje para '{nuevoViaje.NombreBuque}' iniciado correctamente con estado 'Amarrado'.",
                buque     = nuevoViaje.NombreBuque,
                origen    = nuevoViaje.Origen,
                destino   = nuevoViaje.Destino,
                estadoInicial = "Amarrado"
            });
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

        /// <summary>
        /// Actualiza la posición geográfica de un buque (lat/lng + timestamp).
        ///
        /// Reglas de negocio aplicadas en IViajeService.ActualizarPosicionAsync:
        ///   • Haversine: calcula distancia entre posición anterior y nueva.
        ///   • Cinemática: si velocidad calculada > 60 kn → HTTP 400 "Cinemática inválida".
        ///   • Persistencia dual: actualiza el doc activo en MongoDB E inserta copia en tracklog.
        ///
        /// Responsabilidades del Controller (y solo estas):
        ///   1. Validar ModelState (DataAnnotations del DTO).
        ///   2. Verificar que el Claim CosteraId exista.
        ///   3. Delegar a IViajeService y traducir resultado a HTTP.
        /// </summary>
        [HttpPut("{id}/posicion")]
        public async Task<ActionResult> ActualizarPosicion(
            string id,
            [FromBody] ActualizarPosicionDto dto)
        {
            // ── 1. Validación de ModelState ───────────────────────────────────
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

            // ── 2. Validación del Claim multitenant ───────────────────────────
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

            // Tolerancia de 5 minutos para timestamps de transponders con deriva de reloj.
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

            // ── 3. Delegación al Service ──────────────────────────────────────
            PosicionActualizadaResultDto? resultado;

            try
            {
                resultado = await _viajeService.ActualizarPosicionAsync(id, dto);
            }
            catch (InvalidOperationException ex) when (ex.Message.StartsWith("Cinemática inválida"))
            {
                // El servicio lanza esta excepción tipada cuando la velocidad calculada > 60 kn.
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
