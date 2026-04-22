using Microsoft.AspNetCore.Mvc;
using Mbpc.Api.Services;
using Mbpc.Api.DTOs;

namespace Mbpc.Api.Controllers
{
    [ApiController]
    [Route("api/carga")]
    public class CargaController : ControllerBase
    {
        private readonly ICargaService            _cargaService;
        private readonly ILogger<CargaController> _logger;

        public CargaController(ICargaService cargaService, ILogger<CargaController> logger)
        {
            _cargaService = cargaService;
            _logger       = logger;
        }

        // ── GET ───────────────────────────────────────────────────────────────

        [HttpGet("viaje/{viajeId}")]
        public async Task<ActionResult<IEnumerable<CargaDto>>> GetCargasPorViaje(string viajeId)
        {
            if (string.IsNullOrWhiteSpace(viajeId))
                return BadRequest(new { mensaje = "El identificador de viaje no puede estar vacío." });

            _logger.LogInformation("Buscando cargas para viajeId: {ViajeId}", viajeId);
            var cargas = await _cargaService.ObtenerCargasPorViaje(viajeId);
            return Ok(cargas);
        }

        // ── PUTs de estado ────────────────────────────────────────────────────

        [HttpPut("{id}/amarrar")]
        public ActionResult AmarrarBarcaza(string id, [FromQuery] string nuevoMuelle)
        {
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(nuevoMuelle))
                return BadRequest(new { mensaje = "El ID y el nuevo muelle son requeridos." });

            var exito = _cargaService.AmarrarBarcaza(id, nuevoMuelle);
            if (!exito) return NotFound(new { mensaje = $"No se encontró la unidad con ID {id}." });
            return Ok(new { mensaje = $"Unidad {id} amarrada en {nuevoMuelle}." });
        }

        [HttpPut("{id}/fondear")]
        public ActionResult FondearBarcaza(string id, [FromQuery] string zonaFondeo)
        {
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(zonaFondeo))
                return BadRequest(new { mensaje = "El ID y la zona de fondeo son requeridos." });

            var exito = _cargaService.FondearBarcaza(id, zonaFondeo);
            if (!exito) return NotFound(new { mensaje = $"No se encontró la unidad con ID {id}." });
            return Ok(new { mensaje = $"Unidad {id} fondeada en {zonaFondeo}." });
        }

        [HttpPut("{id}/cargar")]
        public ActionResult CargarBarcaza(string id, [FromQuery] double toneladas)
        {
            if (string.IsNullOrWhiteSpace(id))
                return BadRequest(new { mensaje = "El ID de la embarcación es requerido." });

            if (toneladas < 0)
                return BadRequest(new { mensaje = "La cantidad de toneladas no puede ser negativa." });

            var exito = _cargaService.CargarBarcaza(id, toneladas);
            if (!exito) return NotFound(new { mensaje = $"No se encontró la unidad con ID {id}." });
            return Ok(new { mensaje = "Carga registrada correctamente." });
        }

        [HttpPut("{id}/descargar")]
        public ActionResult DescargarBarcaza(string id, [FromQuery] double toneladas)
        {
            if (string.IsNullOrWhiteSpace(id))
                return BadRequest(new { mensaje = "El ID de la embarcación es requerido." });

            // Permitimos 0 para que pase a EN LASTRE
            if (toneladas < 0)
                return BadRequest(new { mensaje = "La cantidad de toneladas no puede ser negativa." });

            var exito = _cargaService.DescargarBarcaza(id, toneladas);
            if (!exito) return NotFound(new { mensaje = $"No se encontró la unidad con ID {id}." });
            return Ok(new { mensaje = "Descarga registrada correctamente." });
        }

        // ── PUT: modificar carga ──────────────────────────────────────────────

        /// <summary>
        /// Modifica los datos de una barcaza existente (BarcazaId, Tipo, Tonelaje).
        /// CQRS: actualiza Oracle mediante SP y hace Load-Mutate-Save en MongoDB.
        /// El campo ViajeId en el DTO ancla la operación al documento correcto (fix de scoping).
        /// </summary>
        [HttpPut("{id}")]
        public async Task<ActionResult> ModificarCarga(string id, [FromBody] ModificarCargaDto dto)
        {
            if (string.IsNullOrWhiteSpace(id))
                return BadRequest(new { mensaje = "El ID de la carga es requerido." });

            if (dto == null)
                return BadRequest(new { mensaje = "El cuerpo de la solicitud no puede estar vacío." });

            if (string.IsNullOrWhiteSpace(dto.ViajeId))
                return BadRequest(new { mensaje = "El ViajeId es requerido para el scoping. No se puede modificar una carga sin especificar el viaje al que pertenece." });

            if (dto.BarcazaId < 0)
                return BadRequest(new { mensaje = "El BarcazaId no puede ser negativo (0 es válido para Bodegas)." });

            if (string.IsNullOrWhiteSpace(dto.Tipo))
                return BadRequest(new { mensaje = "El campo Tipo es requerido." });

            var tiposValidos = new[] { "Barcaza", "Bodega" };
            if (!tiposValidos.Contains(dto.Tipo, StringComparer.OrdinalIgnoreCase))
                return BadRequest(new { mensaje = "El tipo debe ser 'Barcaza' o 'Bodega'." });

            if (dto.Tonelaje < 0)
                return BadRequest(new { mensaje = "El tonelaje no puede ser negativo." });

            _logger.LogInformation(
                "Modificar carga ID='{Id}' en ViajeId='{ViajeId}' → BarcazaId={BarcazaId}, Tipo={Tipo}, Tonelaje={Tonelaje}tn.",
                id, dto.ViajeId, dto.BarcazaId, dto.Tipo, dto.Tonelaje);

            var exito = await _cargaService.ModificarCargaAsync(id, dto);

            if (!exito)
                return StatusCode(500, new { mensaje = $"Error interno al modificar la carga con ID '{id}'." });

            return Ok(new
            {
                mensaje = $"Carga '{id}' actualizada correctamente (nuevo BarcazaId={dto.BarcazaId})."
            });
        }

        // ── DELETE: eliminar carga ────────────────────────────────────────────

        /// <summary>
        /// Elimina una barcaza del manifiesto del viaje.
        /// CQRS: elimina en Oracle mediante SP y remueve el nodo en MongoDB.
        /// Fix de scoping (Hito 5.8): el viajeId es obligatorio en la ruta para que MongoDB
        /// filtre estrictamente por documento de viaje antes de remover la barcaza,
        /// evitando que bodegas con ID "0" sean eliminadas del viaje incorrecto.
        /// </summary>
        [HttpDelete("viaje/{viajeId}/carga/{id}")]
        public async Task<ActionResult> EliminarCarga(string viajeId, string id)
        {
            if (string.IsNullOrWhiteSpace(viajeId))
                return BadRequest(new { mensaje = "El ViajeId es requerido para el scoping. No se puede eliminar una carga sin especificar el viaje al que pertenece." });

            if (string.IsNullOrWhiteSpace(id))
                return BadRequest(new { mensaje = "El ID de la carga es requerido." });

            _logger.LogInformation("Eliminar carga ID='{Id}' del viaje '{ViajeId}'.", id, viajeId);

            var exito = await _cargaService.EliminarCargaAsync(viajeId, id);

            if (!exito)
                return StatusCode(500, new { mensaje = $"Error interno al eliminar la carga con ID '{id}' del viaje '{viajeId}'." });

            return Ok(new { mensaje = $"Carga '{id}' eliminada correctamente del manifiesto del viaje '{viajeId}'." });
        }

        // ── POST: agregar carga al viaje ──────────────────────────────────────

        /// <summary>
        /// Agrega una nueva carga (Barcaza o Bodega) al manifiesto del buque.
        /// CQRS: escribe en Oracle y hace Load-Mutate-Save en MongoDB.
        /// </summary>
        [HttpPost("viaje/{viajeNombreBuque}")]
        public async Task<ActionResult> AgregarCarga(
            string viajeNombreBuque,
            [FromBody] NuevaCargaDto nuevaCarga)
        {
            if (string.IsNullOrWhiteSpace(viajeNombreBuque))
                return BadRequest(new { mensaje = "El nombre del buque no puede estar vacío." });

            if (nuevaCarga == null
                || nuevaCarga.BarcazaId < 0
                || string.IsNullOrWhiteSpace(nuevaCarga.Tipo))
            {
                return BadRequest(new { mensaje = "Los campos BarcazaId (positivo o cero) y Tipo son requeridos." });
            }

            var tiposValidos = new[] { "Barcaza", "Bodega" };
            if (!tiposValidos.Contains(nuevaCarga.Tipo, StringComparer.OrdinalIgnoreCase))
                return BadRequest(new { mensaje = "El tipo debe ser 'Barcaza' o 'Bodega'." });

            if (nuevaCarga.Tonelaje < 0)
                return BadRequest(new { mensaje = "El tonelaje no puede ser negativo." });

            _logger.LogInformation(
                "Agregar carga BarcazaId={BarcazaId} ({Tipo}, {Tonelaje}tn) al buque '{Buque}'.",
                nuevaCarga.BarcazaId, nuevaCarga.Tipo, nuevaCarga.Tonelaje, viajeNombreBuque);

            var exito = await _cargaService.AgregarCargaAsync(viajeNombreBuque, nuevaCarga);

            if (!exito)
                return StatusCode(500, new { mensaje = $"Error interno al agregar la carga al buque '{viajeNombreBuque}'." });

            return Ok(new
            {
                mensaje = $"Carga BarcazaId={nuevaCarga.BarcazaId} ({nuevaCarga.Tipo}) agregada correctamente al buque '{viajeNombreBuque}'."
            });
        }
    }
}
