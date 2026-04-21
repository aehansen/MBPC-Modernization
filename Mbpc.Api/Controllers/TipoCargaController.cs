using Mbpc.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Mbpc.Api.Controllers
{
    [ApiController]
    [Route("api/tipocarga")]
    [Produces("application/json")]
    public class TipoCargaController : ControllerBase
    {
        private readonly ITipoCargaService _service;

        public TipoCargaController(ITipoCargaService service)
        {
            _service = service;
        }

        [HttpGet("autocomplete")]
        public async Task<IActionResult> Autocomplete([FromQuery] string query)
        {
            var resultados = await _service.BuscarAutocompleteAsync(query);
            return Ok(resultados);
        }

        [HttpPost("sincronizar")]
        public async Task<IActionResult> Sincronizar()
        {
            var total = await _service.SincronizarDesdeOracleAsync();
            return Ok(new { TotalMigrado = total, Mensaje = $"{total} tipos de carga sincronizados." });
        }
    }
}