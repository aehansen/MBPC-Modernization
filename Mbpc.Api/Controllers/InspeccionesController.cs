using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Mbpc.Api.Services;
using Mbpc.Api.DTOs;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Mbpc.Api.Controllers
{
    [ApiController]
    [Route("api/inspecciones")]
    [Authorize]
    public class InspeccionesController : ControllerBase
    {
        private readonly IInspeccionService _inspeccionService;
        private readonly ILogger<InspeccionesController> _logger;

        public InspeccionesController(IInspeccionService inspeccionService, ILogger<InspeccionesController> logger)
        {
            _inspeccionService = inspeccionService;
            _logger = logger;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<InspeccionDto>>> GetInspecciones(
            [FromQuery] string? viajeId = null,
            [FromQuery] int pagina = 1,
            [FromQuery] int tamanio = 50)
        {
            var costeraIdClaim = User.FindFirstValue("CosteraId");
            if (string.IsNullOrWhiteSpace(costeraIdClaim))
            {
                _logger.LogWarning("GetInspecciones rechazado: el token no contiene el Claim 'CosteraId'.");
                return Forbid();
            }

            if (pagina < 1) pagina = 1;
            tamanio = Math.Clamp(tamanio, 1, 200);

            var result = await _inspeccionService.ObtenerInspeccionesAsync(viajeId, pagina, tamanio);
            return Ok(result);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<InspeccionDto>> GetInspeccionById(Guid id)
        {
            var costeraIdClaim = User.FindFirstValue("CosteraId");
            if (string.IsNullOrWhiteSpace(costeraIdClaim)) return Forbid();

            var result = await _inspeccionService.ObtenerPorIdAsync(id);
            if (result == null)
            {
                return NotFound(new { mensaje = $"No se encontró la inspección con ID {id}." });
            }

            return Ok(result);
        }

        [HttpPost]
        public async Task<ActionResult> CrearInspeccion([FromBody] CrearInspeccionDto dto)
        {
            if (!ModelState.IsValid)
            {
                return ValidationProblem(ModelState);
            }

            var costeraIdClaim = User.FindFirstValue("CosteraId");
            if (string.IsNullOrWhiteSpace(costeraIdClaim))
            {
                _logger.LogWarning("CrearInspeccion rechazado: el token no contiene el Claim 'CosteraId'.");
                return Forbid();
            }

            var exito = await _inspeccionService.CrearInspeccionAsync(dto);
            if (!exito)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    mensaje = "Error interno al procesar el alta de inspección. Intente nuevamente."
                });
            }

            return Ok(new { mensaje = "Inspección creada correctamente." });
        }

        [HttpPut("{id}")]
        public async Task<ActionResult> ModificarInspeccion(Guid id, [FromBody] ModificarInspeccionDto dto)
        {
            if (!ModelState.IsValid)
            {
                return ValidationProblem(ModelState);
            }

            var costeraIdClaim = User.FindFirstValue("CosteraId");
            if (string.IsNullOrWhiteSpace(costeraIdClaim)) return Forbid();

            var exito = await _inspeccionService.ModificarInspeccionAsync(id, dto);
            if (!exito)
            {
                return UnprocessableEntity(new { mensaje = "No se pudo modificar la inspección. Verifique que exista y coincida con el viaje especificado." });
            }

            return Ok(new { mensaje = "Inspección modificada correctamente." });
        }

        [HttpDelete("{id}")]
        public async Task<ActionResult> EliminarInspeccion(Guid id, [FromQuery] string viajeId)
        {
            var costeraIdClaim = User.FindFirstValue("CosteraId");
            if (string.IsNullOrWhiteSpace(costeraIdClaim)) return Forbid();

            var exito = await _inspeccionService.EliminarInspeccionAsync(id, viajeId);
            if (!exito)
            {
                return UnprocessableEntity(new { mensaje = "No se pudo eliminar la inspección. Verifique que exista y coincida con el viaje especificado." });
            }

            return Ok(new { mensaje = "Inspección eliminada correctamente." });
        }
    }
}
