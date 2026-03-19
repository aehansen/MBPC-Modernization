using Microsoft.AspNetCore.Mvc;
using Mbpc.Api.Services;
using Mbpc.Api.DTOs;
using System.Collections.Generic;

namespace Mbpc.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")] // Esto ruteará a /api/viaje
    public class ViajeController : ControllerBase
    {
        private readonly IViajeService _viajeService;

        // Inyección de dependencias limpia y directa
        public ViajeController(IViajeService viajeService)
        {
            _viajeService = viajeService;
        }

        // Mapeamos explícitamente al verbo GET
        [HttpGet("activos")]
        public ActionResult<IEnumerable<ViajeDto>> GetViajesActivos()
        {
            var viajes = _viajeService.ObtenerViajesActivos();
            
            if (viajes == null)
            {
                return NotFound("No se encontraron viajes activos.");
            }

            return Ok(viajes); // Devuelve HTTP 200 con el JSON estructurado
        }
        // POST: api/viaje
        [HttpPost]
        public ActionResult<ViajeDto> IniciarViaje([FromBody] NuevoViajeDto nuevoViaje)
        {
            if (string.IsNullOrWhiteSpace(nuevoViaje.NombreBuque) || 
                string.IsNullOrWhiteSpace(nuevoViaje.Origen) || 
                string.IsNullOrWhiteSpace(nuevoViaje.Destino))
            {
                return BadRequest("El buque, origen y destino son obligatorios para iniciar el viaje.");
            }

            var viajeCreado = _viajeService.CrearViaje(nuevoViaje);
            
            // Retornamos un 201 Created con el objeto recién creado
            return Created("", viajeCreado); 
        }
    }
}