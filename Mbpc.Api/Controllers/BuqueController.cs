using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Mbpc.Api.Services;
using Mbpc.Api.DTOs.Catalogos;

namespace Mbpc.Api.Controllers;

/// <summary>
/// Controller de gestión del padrón de Buques y Barcazas (ABM).
///
/// DISEÑO (Controlador Anoréxico):
///   - No contiene lógica de negocio.
///   - Extrae el CosteraId del JWT y lo pasa al servicio. NUNCA lo acepta del cliente.
///   - Las validaciones de dominio (OMI duplicado, MMSI duplicado, etc.) las lanza
///     el servicio como InvalidOperationException, que el Middleware global convierte en HTTP 422.
///   - Los endpoints de autocomplete usan IBuqueService (read-only, Oracle BUQUES_NEW).
///   - Los endpoints ABM usan ICatalogoService (escritura, Oracle stored procedures).
///
/// RUTAS:
///   GET  /api/buques/autocomplete              → Autocomplete buques (lectura)
///   GET  /api/buques/autocomplete/barcazas     → Autocomplete barcazas (lectura)
///   GET  /api/buques/autocomplete/remolcadores → Autocomplete remolcadores (lectura)
///   GET  /api/buques/barcazas/autocomplete     → Autocomplete barcazas por etapa (lectura)
///   GET  /api/buques                           → Lista paginada de buques del padrón
///   GET  /api/buques/{id}                      → Detalle de un buque por IdBuque
///   POST /api/buques                           → Alta de buque
///   PUT  /api/buques/{id}                      → Edición de buque
///   GET  /api/buques/barcazas                  → Lista paginada de barcazas
///   GET  /api/buques/barcazas/{id}             → Detalle de una barcaza
///   POST /api/buques/barcazas                  → Alta de barcaza
///   PUT  /api/buques/barcazas/{id}             → Edición de barcaza
/// </summary>
[ApiController]
[Route("api/buques")]
[Authorize]
public class BuqueController : ControllerBase
{
    private readonly IBuqueService               _buqueService;
    private readonly ICatalogoService            _catalogoService;
    private readonly ILogger<BuqueController>    _logger;

    public BuqueController(
        IBuqueService            buqueService,
        ICatalogoService         catalogoService,
        ILogger<BuqueController> logger)
    {
        _buqueService    = buqueService;
        _catalogoService = catalogoService;
        _logger          = logger;
    }

    // ── AUTOCOMPLETE (solo lectura, delega a IBuqueService) ──────────────────

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
    public async Task<IActionResult> BarcazasAutocomplete(
        [FromQuery] string  etapaId,
        [FromQuery] string? query)
    {
        var resultados = await _buqueService.BuscarBarcazasDisponiblesAsync(etapaId, query ?? string.Empty);
        return Ok(resultados);
    }

    // ── ABM BUQUES ────────────────────────────────────────────────────────────

    /// <summary>
    /// Lista paginada del padrón de buques.
    /// Filtra por query (nombre, matrícula, OMI) si se informa.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> ListarBuques(
        [FromQuery] string? query,
        [FromQuery] int     pagina  = 1,
        [FromQuery] int     tamanio = 50)
    {
        if (pagina  < 1)   pagina  = 1;
        if (tamanio < 1)   tamanio = 1;
        if (tamanio > 100) tamanio = 100;

        _logger.LogInformation(
            "ListarBuques — query='{Query}', pagina={Pagina}, tamanio={Tamanio}.",
            query, pagina, tamanio);

        var resultado = await _catalogoService.ObtenerBuquesAsync(query, pagina, tamanio);
        return Ok(resultado);
    }

    /// <summary>
    /// Retorna el detalle completo de un buque por su IdBuque del padrón Oracle.
    /// </summary>
    [HttpGet("{id:long}")]
    public async Task<IActionResult> ObtenerBuque(long id)
    {
        _logger.LogInformation("ObtenerBuque — IdBuque: {Id}.", id);

        var buque = await _catalogoService.ObtenerBuquePorIdAsync(id);

        if (buque is null)
            return NotFound(new { mensaje = $"No se encontró un buque con IdBuque={id} en el padrón." });

        return Ok(buque);
    }

    /// <summary>
    /// Da de alta un nuevo buque en el padrón Oracle.
    /// El CosteraId se extrae del JWT. Si el OMI, MMSI o Matrícula ya existen,
    /// el servicio lanza InvalidOperationException → Middleware → HTTP 422.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> CrearBuque([FromBody] BuqueAltaDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var costeraId = ObtenerCosteraId();
        if (costeraId is null)
            return Forbid();

        _logger.LogInformation(
            "CrearBuque — Nombre: '{Nombre}', Tipo: '{Tipo}', CosteraId: {CosteraId}.",
            dto.Nombre, dto.Tipo, costeraId);

        var buqueCreado = await _catalogoService.CrearBuqueAsync(dto, costeraId.Value);

        return CreatedAtAction(
            nameof(ObtenerBuque),
            new { id = buqueCreado.IdBuque },
            buqueCreado);
    }

    /// <summary>
    /// Edita los datos de un buque existente.
    /// El id de la ruta debe coincidir con el IdBuque del body.
    /// </summary>
    [HttpPut("{id:long}")]
    public async Task<IActionResult> EditarBuque(long id, [FromBody] BuqueEditDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        if (id != dto.IdBuque)
            return BadRequest(new { mensaje = $"El Id de la ruta ({id}) no coincide con el IdBuque del body ({dto.IdBuque})." });

        _logger.LogInformation(
            "EditarBuque — IdBuque: {Id}, Nombre: '{Nombre}'.",
            dto.IdBuque, dto.Nombre);

        var buqueActualizado = await _catalogoService.EditarBuqueAsync(dto);
        return Ok(buqueActualizado);
    }

    // ── ABM BARCAZAS ──────────────────────────────────────────────────────────

    /// <summary>
    /// Lista paginada del padrón de barcazas.
    /// Filtra por query (nombre o matrícula) si se informa.
    /// </summary>
    [HttpGet("barcazas")]
    public async Task<IActionResult> ListarBarcazas(
        [FromQuery] string? query,
        [FromQuery] int     pagina  = 1,
        [FromQuery] int     tamanio = 50)
    {
        if (pagina  < 1)   pagina  = 1;
        if (tamanio < 1)   tamanio = 1;
        if (tamanio > 100) tamanio = 100;

        _logger.LogInformation(
            "ListarBarcazas — query='{Query}', pagina={Pagina}, tamanio={Tamanio}.",
            query, pagina, tamanio);

        var resultado = await _catalogoService.ObtenerBarcazasAsync(query, pagina, tamanio);
        return Ok(resultado);
    }

    /// <summary>
    /// Retorna el detalle completo de una barcaza por su IdBarcaza del padrón Oracle.
    /// </summary>
    [HttpGet("barcazas/{id:long}")]
    public async Task<IActionResult> ObtenerBarcaza(long id)
    {
        _logger.LogInformation("ObtenerBarcaza — IdBarcaza: {Id}.", id);

        var barcaza = await _catalogoService.ObtenerBarcazaPorIdAsync(id);

        if (barcaza is null)
            return NotFound(new { mensaje = $"No se encontró una barcaza con IdBarcaza={id} en el padrón." });

        return Ok(barcaza);
    }

    /// <summary>
    /// Da de alta una nueva barcaza en el padrón Oracle.
    /// El CosteraId se extrae del JWT. Si la matrícula ya existe (y no es valor especial legacy),
    /// el servicio lanza InvalidOperationException → Middleware → HTTP 422.
    /// </summary>
    [HttpPost("barcazas")]
    public async Task<IActionResult> CrearBarcaza([FromBody] BarcazaAltaDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var costeraId = ObtenerCosteraId();
        if (costeraId is null)
            return Forbid();

        _logger.LogInformation(
            "CrearBarcaza — Nombre: '{Nombre}', Matrícula: '{Matricula}', CosteraId: {CosteraId}.",
            dto.Nombre, dto.Matricula, costeraId);

        var barcazaCreada = await _catalogoService.CrearBarcazaAsync(dto, costeraId.Value);

        return CreatedAtAction(
            nameof(ObtenerBarcaza),
            new { id = barcazaCreada.IdBarcaza },
            barcazaCreada);
    }

    /// <summary>
    /// Edita los datos de una barcaza existente.
    /// El id de la ruta debe coincidir con el IdBarcaza del body.
    /// </summary>
    [HttpPut("barcazas/{id:long}")]
    public async Task<IActionResult> EditarBarcaza(long id, [FromBody] BarcazaEditDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        if (id != dto.IdBarcaza)
            return BadRequest(new { mensaje = $"El Id de la ruta ({id}) no coincide con el IdBarcaza del body ({dto.IdBarcaza})." });

        _logger.LogInformation(
            "EditarBarcaza — IdBarcaza: {Id}, Nombre: '{Nombre}'.",
            dto.IdBarcaza, dto.Nombre);

        var barcazaActualizada = await _catalogoService.EditarBarcazaAsync(dto);
        return Ok(barcazaActualizada);
    }

    // ── Helper Privado ────────────────────────────────────────────────────────

    /// <summary>
    /// Extrae el CosteraId del Claim JWT del usuario autenticado.
    /// Retorna null si el claim no existe o no es un entero válido.
    /// El controller llama a Forbid() en ese caso.
    /// </summary>
    private int? ObtenerCosteraId()
    {
        var claimValue = User.FindFirst("CosteraId")?.Value;

        if (string.IsNullOrWhiteSpace(claimValue))
        {
            _logger.LogWarning("ObtenerCosteraId — El Claim 'CosteraId' no está presente en el JWT.");
            return null;
        }

        if (!int.TryParse(claimValue, out var costeraId))
        {
            _logger.LogWarning("ObtenerCosteraId — El Claim 'CosteraId' no es un entero válido: '{Value}'.", claimValue);
            return null;
        }

        return costeraId;
    }
}