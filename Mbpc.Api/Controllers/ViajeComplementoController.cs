using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Mbpc.Api.DTOs;
using Mbpc.Api.Services;

namespace Mbpc.Api.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/viajes/{viajeId}/complementos")]
    public class ViajeComplementoController : ControllerBase
    {
        private readonly IViajeComplementoService _service;
        private readonly ILogger<ViajeComplementoController> _logger;

        public ViajeComplementoController(
            IViajeComplementoService service,
            ILogger<ViajeComplementoController> logger)
        {
            _service = service;
            _logger = logger;
        }

        /// <summary>
        /// GET api/viajes/{viajeId}/complementos
        /// Obtiene el panel consolidado: bitácora, agencias y datos PBIP.
        /// </summary>
        [HttpGet]
        [ProducesResponseType(typeof(ViajeComplementosDto), 200)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> ObtenerComplementos(
            [FromRoute] string viajeId,
            CancellationToken ct)
        {
            var resultado = await _service.ObtenerComplementosPorViajeIdAsync(viajeId, ct);

            if (resultado is null)
            {
                _logger.LogWarning("Complementos no encontrados para el viaje {ViajeId}.", viajeId);
                return NotFound(new { mensaje = $"No se encontraron complementos para el viaje {viajeId}." });
            }

            return Ok(resultado);
        }

        /// <summary>
        /// POST api/viajes/{viajeId}/complementos/notas
        /// Inyecta una nueva nota auditada a la bitácora del viaje.
        /// </summary>
        [HttpPost("notas")]
        [ProducesResponseType(typeof(NotaBitacoraDto), 201)]
        [ProducesResponseType(400)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> AgregarNota(
            [FromRoute] string viajeId,
            [FromBody] AgregarNotaBitacoraDto dto,
            CancellationToken ct)
        {
            _logger.LogInformation("Recibida petición para agregar nota al viaje {ViajeId}", viajeId);

            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                var nuevaNota = await _service.AgregarNotaBitacoraAsync(viajeId, dto, ct);
                return StatusCode(201, nuevaNota);
            }
            catch (KeyNotFoundException ex)
            {
                _logger.LogWarning(ex, "Viaje {ViajeId} no encontrado al agregar nota.", viajeId);
                return NotFound(new { mensaje = ex.Message });
            }
        }

        /// <summary>
        /// PUT api/viajes/{viajeId}/complementos/agencias
        /// Sobreescribe de forma atómica el listado de agencias intervinientes.
        /// </summary>
        [HttpPut("agencias")]
        [ProducesResponseType(200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> ActualizarAgencias(
            [FromRoute] string viajeId,
            [FromBody] List<AsignarAgenciaDto> dtos,
            CancellationToken ct)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                await _service.ActualizarAgenciasAsync(viajeId, dtos, ct);
                return Ok(new { mensaje = "Agencias actualizadas correctamente." });
            }
            catch (KeyNotFoundException ex)
            {
                _logger.LogWarning(ex, "Viaje {ViajeId} no encontrado al actualizar agencias.", viajeId);
                return NotFound(new { mensaje = ex.Message });
            }
        }

        /// <summary>
        /// PUT api/viajes/{viajeId}/complementos/pbip
        /// Actualiza o hidrata los datos de protección marítima PBIP.
        /// </summary>
        [HttpPut("pbip")]
        [ProducesResponseType(200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> ActualizarPbip(
            [FromRoute] string viajeId,
            [FromBody] ActualizarDatosPbipDto dto,
            CancellationToken ct)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                await _service.ActualizarDatosPbipAsync(viajeId, dto, ct);
                return Ok(new { mensaje = "Datos PBIP actualizados correctamente." });
            }
            catch (KeyNotFoundException ex)
            {
                _logger.LogWarning(ex, "Viaje {ViajeId} no encontrado al actualizar PBIP.", viajeId);
                return NotFound(new { mensaje = ex.Message });
            }
        }
    }
}
