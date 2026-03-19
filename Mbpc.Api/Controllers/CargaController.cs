using Microsoft.AspNetCore.Mvc;
using Mbpc.Api.Services;
using Mbpc.Api.DTOs;
using System.Collections.Generic;

namespace Mbpc.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CargaController : ControllerBase
    {
        private readonly ICargaService _cargaService;

        public CargaController(ICargaService cargaService)
        {
            _cargaService = cargaService;
        }

        // Cambiamos int viajeId por string viajeId
        [HttpGet("viaje/{viajeId}")]
        public ActionResult<IEnumerable<CargaDto>> GetCargasPorViaje(string viajeId)
        {
            var cargas = _cargaService.ObtenerCargasPorViaje(viajeId);
            return Ok(cargas);
        }

        // Cambiamos int id por string id
        [HttpPut("{id}/amarrar")]
        public ActionResult AmarrarBarcaza(string id, [FromQuery] string nuevoMuelle)
        {
            if (string.IsNullOrWhiteSpace(nuevoMuelle))
            {
                return BadRequest("Debe especificar el muelle de destino.");
            }

            var exito = _cargaService.AmarrarBarcaza(id, nuevoMuelle);
            if (!exito)
            {
                return NotFound($"No se encontró la carga o barcaza con ID {id}.");
            }

            return Ok(new { Mensaje = $"Barcaza {id} amarrada exitosamente en {nuevoMuelle}." });
        }

        // Cambiamos int id por string id
        [HttpPut("{id}/fondear")]
        public ActionResult FondearBarcaza(string id, [FromQuery] string zonaFondeo)
        {
            if (string.IsNullOrWhiteSpace(zonaFondeo))
            {
                return BadRequest("Debe especificar la zona de fondeo.");
            }

            var exito = _cargaService.FondearBarcaza(id, zonaFondeo);
            if (!exito)
            {
                return NotFound($"No se encontró la carga o barcaza con ID {id}.");
            }

            return Ok(new { Mensaje = $"Barcaza {id} fondeada exitosamente en {zonaFondeo}." });
        }
    }
}