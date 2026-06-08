using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Mbpc.Api.Services;
using Mbpc.Api.DTOs;

namespace Mbpc.Api.Controllers
{
    [ApiController]
    [Route("api/catalogos")]
    [Authorize]
    public class CatalogoController : ControllerBase
    {
        private readonly ICatalogoService _catalogoService;

        public CatalogoController(ICatalogoService catalogoService)
        {
            _catalogoService = catalogoService;
        }

        [HttpGet("puntos-control")]
        public async Task<IActionResult> ObtenerPuntosControl()
        {
            var resultado = await _catalogoService.ObtenerPuntosControlAsync();
            return Ok(resultado);
        }

        [HttpGet("muelles")]
        public async Task<IActionResult> ObtenerMuelles()
        {
            var resultado = await _catalogoService.ObtenerMuellesAsync();
            return Ok(resultado);
        }
    }
}
