using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Mbpc.Api.DTOs;
using Mbpc.Api.Services;
using Mbpc.Api.Services.Auth;

namespace Mbpc.Api.Controllers
{
    [ApiController]
    [Route("api/costeras")]
    [Authorize]
    public class CosteraController : ControllerBase
    {
        private readonly ICosteraService _costeraService;
        private readonly ICosteraUserContext _userContext;

        public CosteraController(ICosteraService costeraService, ICosteraUserContext userContext)
        {
            _costeraService = costeraService;
            _userContext = userContext;
        }

        [HttpGet("limites")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(GeoJsonFeatureCollectionDto))]
        public async Task<IActionResult> ObtenerLimites([FromQuery] bool todos = false)
        {
            int costeraId = _userContext.GetCurrentCosteraId();
            
            if (costeraId < 0 && !todos)
            {
                return Unauthorized(new { mensaje = "El token provisto es inválido o carece de CosteraId." });
            }

            var respuestaGeoJson = new GeoJsonFeatureCollectionDto();

            // Si es SuperAdmin (CosteraId == 0) o pide todos para el mapa global
            if (todos || costeraId == 0)
            {
                var limitesGlobales = await _costeraService.ObtenerLimitesJurisdiccionalesAsync();
                respuestaGeoJson.Features = limitesGlobales.ToList();
                return Ok(respuestaGeoJson);
            }

            // Operador normal: solo ve su jurisdicción
            var limitePersonal = await _costeraService.ObtenerLimitePorCosteraIdAsync(costeraId);

            if (limitePersonal == null)
            {
                return NotFound(new { mensaje = $"No se encontraron perímetros geográficos para la Costera ID: {costeraId}" });
            }

            respuestaGeoJson.Features.Add(limitePersonal);
            return Ok(respuestaGeoJson);
        }
    }
}
