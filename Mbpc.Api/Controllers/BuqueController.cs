using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Mbpc.Api.Services;

namespace Mbpc.Api.Controllers;

[ApiController]
[Route("api/buques")]
[Authorize]
public class BuqueController : ControllerBase
{
    private readonly IBuqueService _buqueService;
    private readonly ILogger<BuqueController> _logger;

    public BuqueController(IBuqueService buqueService, ILogger<BuqueController> logger)
    {
        _buqueService = buqueService;
        _logger = logger;
    }

    [HttpGet("autocomplete")]
    public async Task<IActionResult> Autocomplete([FromQuery] string? query)
    {
        var resultados = await _buqueService.BuscarBuquesDisponiblesAsync(query ?? string.Empty);
        return Ok(resultados);
    }

    [HttpGet("autocomplete/barcazas")]
    public async Task<IActionResult> AutocompleteBarcazas([FromQuery] string? query)
    {
        var resultados = await _buqueService.BuscarBarcazasDisponiblesAsync(query ?? string.Empty);
        return Ok(resultados);
    }

    [HttpGet("autocomplete/remolcadores")]
    public async Task<IActionResult> AutocompleteRemolcadores([FromQuery] string? query)
    {
        var resultados = await _buqueService.BuscarRemolcadoresDisponiblesAsync(query ?? string.Empty);
        return Ok(resultados);
    }

    [HttpGet("barcazas/autocomplete")]
    public async Task<IActionResult> BarcazasAutocomplete([FromQuery] string etapaId, [FromQuery] string? query)
    {
        var resultados = await _buqueService.BuscarBarcazasDisponiblesAsync(etapaId, query ?? string.Empty);
        return Ok(resultados);
    }
}