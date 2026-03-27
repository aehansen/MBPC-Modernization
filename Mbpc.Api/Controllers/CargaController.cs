using Microsoft.AspNetCore.Mvc;
using Mbpc.Api.Services;
using Mbpc.Api.DTOs;

namespace Mbpc.Api.Controllers
{
    [ApiController]
    [Route("api/carga")]
    public class CargaController : ControllerBase
    {
        private readonly ICargaService          _cargaService;
        private readonly ILogger<CargaController> _logger;

        public CargaController(ICargaService cargaService, ILogger<CargaController> logger)
        {
            _cargaService = cargaService;
            _logger       = logger;
        }

        // ── GET ───────────────────────────────────────────────────────────────

        [HttpGet("viaje/{viajeId}")]
        public ActionResult<IEnumerable<CargaDto>> GetCargasPorViaje(string viajeId)
        {
            if (string.IsNullOrWhiteSpace(viajeId))
                return BadRequest(new { mensaje = "El identificador de viaje no puede estar vacío." });

            _logger.LogInformation("Buscando cargas para viajeId: {ViajeId}", viajeId);
            var cargas = _cargaService.ObtenerCargasPorViaje(viajeId);
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

        // ── POST: agregar carga al viaje ──────────────────────────────────────

        /// <summary>
        /// Agrega una nueva carga (Barcaza o Bodega) al manifiesto del buque.
        /// CQRS: escribe en Oracle y hace Update.Push en MongoDB.
        /// </summary>
        [HttpPost("viaje/{viajeNombreBuque}")]
        public async Task<ActionResult> AgregarCarga(
            string viajeNombreBuque,
            [FromBody] NuevaCargaDto nuevaCarga)
        {
            if (string.IsNullOrWhiteSpace(viajeNombreBuque))
                return BadRequest(new { mensaje = "El nombre del buque no puede estar vacío." });

            if (nuevaCarga == null
                || string.IsNullOrWhiteSpace(nuevaCarga.Nombre)
                || string.IsNullOrWhiteSpace(nuevaCarga.Tipo))
            {
                return BadRequest(new { mensaje = "Los campos Nombre y Tipo son requeridos." });
            }

            var tiposValidos = new[] { "Barcaza", "Bodega" };
            if (!tiposValidos.Contains(nuevaCarga.Tipo, StringComparer.OrdinalIgnoreCase))
                return BadRequest(new { mensaje = "El tipo debe ser 'Barcaza' o 'Bodega'." });

            if (nuevaCarga.Tonelaje < 0)
                return BadRequest(new { mensaje = "El tonelaje no puede ser negativo." });

            _logger.LogInformation(
                "Agregar carga '{Nombre}' ({Tipo}, {Tonelaje}tn) al buque '{Buque}'.",
                nuevaCarga.Nombre, nuevaCarga.Tipo, nuevaCarga.Tonelaje, viajeNombreBuque);

            var exito = await _cargaService.AgregarCargaAsync(viajeNombreBuque, nuevaCarga);

            if (!exito)
                return StatusCode(500, new { mensaje = $"Error interno al agregar la carga al buque '{viajeNombreBuque}'." });

            return Ok(new
            {
                mensaje = $"Carga '{nuevaCarga.Nombre}' ({nuevaCarga.Tipo}) agregada correctamente al buque '{viajeNombreBuque}'."
            });
        }
    }
}
