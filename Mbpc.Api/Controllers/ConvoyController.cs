// Mbpc.Api/Controllers/ConvoyController.cs

using System.ComponentModel.DataAnnotations;
using Mbpc.Api.DTOs.Convoy;
using Mbpc.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Mbpc.Api.Controllers;

/// <summary>
/// Controlador de Convoyes — expone la nueva superficie REST explícita.
/// Patrón Strangler Fig: coexiste con <c>CargaController</c> legacy mientras
/// el frontend migra gradualmente a estas rutas semánticas.
///
/// Responsabilidad única: delegar al <see cref="IConvoyManagerService"/> y
/// traducir excepciones de dominio a respuestas HTTP con <see cref="ProblemDetails"/>.
/// </summary>
[ApiController]
[Route("api/convoyes")]
[Produces("application/json")]
public sealed class ConvoyController : ControllerBase
{
    private readonly IConvoyManagerService _convoyService;
    private readonly ILogger<ConvoyController> _logger;

    public ConvoyController(
        IConvoyManagerService convoyService,
        ILogger<ConvoyController> logger)
    {
        _convoyService = convoyService ?? throw new ArgumentNullException(nameof(convoyService));
        _logger        = logger        ?? throw new ArgumentNullException(nameof(logger));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // GET /api/convoyes/viaje/{viajeId}
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Obtiene la composición completa del convoy asociado a un viaje.
    /// </summary>
    /// <param name="viajeId">Identificador del viaje.</param>
    /// <param name="ct">Token de cancelación inyectado por el framework.</param>
    /// <returns>
    /// <c>200 OK</c> con <see cref="ConvoyDto"/>, o <c>404 Not Found</c> si el viaje no existe.
    /// </returns>
    [HttpGet("viaje/{viajeId}")]
    [ProducesResponseType(typeof(ConvoyDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> ObtenerConvoyPorViaje(
        [FromRoute] string viajeId,
        CancellationToken ct)
    {
        try
        {
            var convoy = await _convoyService.ObtenerConvoyPorViajeIdAsync(viajeId, ct);

            if (convoy is null)
            {
                return NotFound(CrearProblem(
                    status: StatusCodes.Status404NotFound,
                    title:  "Convoy no encontrado",
                    detail: $"No existe un convoy asociado al viaje con Id '{viajeId}'."));
            }

            return Ok(convoy);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Error inesperado al obtener convoy para ViajeId={ViajeId}", viajeId);
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                CrearProblem(
                    status: StatusCodes.Status500InternalServerError,
                    title:  "Error interno del servidor",
                    detail: "Ocurrió un error inesperado. Por favor reintente más tarde."));
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // PUT /api/convoyes/barcazas/{barcazaId}/amarrar
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Amarra una barcaza a un muelle específico.
    /// </summary>
    /// <param name="barcazaId">Identificador de la barcaza a amarrar.</param>
    /// <param name="request">Payload con el muelle destino.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>
    /// <c>204 No Content</c> si la operación fue exitosa.
    /// <c>400 Bad Request</c> si el payload es inválido o la transición de estado es ilegal.
    /// <c>404 Not Found</c> si la barcaza no existe.
    /// </returns>
    [HttpPut("barcazas/{barcazaId}/amarrar")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> AmarrarBarcaza(
        [FromRoute] string barcazaId,
        [FromBody]  AmarrarBarcazaRequest request,
        CancellationToken ct)
    {
        try
        {
            await _convoyService.AmarrarBarcazaAsync(barcazaId, request, ct);
            return NoContent();
        }
        catch (ValidationException ex)
        {
            return BadRequest(CrearProblem(
                status: StatusCodes.Status400BadRequest,
                title:  "Datos de amarre inválidos",
                detail: ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(CrearProblem(
                status: StatusCodes.Status400BadRequest,
                title:  "Operación de amarre no permitida",
                detail: ex.Message));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(CrearProblem(
                status: StatusCodes.Status404NotFound,
                title:  "Barcaza no encontrada",
                detail: ex.Message));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex,
                "Error inesperado al amarrar BarcazaId={BarcazaId} en Muelle={Muelle}",
                barcazaId, request.NuevoMuelle);

            return StatusCode(
                StatusCodes.Status500InternalServerError,
                CrearProblem(
                    status: StatusCodes.Status500InternalServerError,
                    title:  "Error interno del servidor",
                    detail: "No se pudo completar la operación de amarre. Reintente más tarde."));
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // PUT /api/convoyes/barcazas/{barcazaId}/fondear
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Fondea una barcaza en la zona indicada.
    /// </summary>
    /// <param name="barcazaId">Identificador de la barcaza a fondear.</param>
    /// <param name="request">Payload con la zona de fondeo.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>
    /// <c>204 No Content</c> si la operación fue exitosa.
    /// <c>400 Bad Request</c> si el payload es inválido o la transición de estado es ilegal.
    /// <c>404 Not Found</c> si la barcaza no existe.
    /// </returns>
    [HttpPut("barcazas/{barcazaId}/fondear")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> FondearBarcaza(
        [FromRoute] string barcazaId,
        [FromBody]  FondearBarcazaRequest request,
        CancellationToken ct)
    {
        try
        {
            await _convoyService.FondearBarcazaAsync(barcazaId, request, ct);
            return NoContent();
        }
        catch (ValidationException ex)
        {
            return BadRequest(CrearProblem(
                status: StatusCodes.Status400BadRequest,
                title:  "Datos de fondeo inválidos",
                detail: ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(CrearProblem(
                status: StatusCodes.Status400BadRequest,
                title:  "Operación de fondeo no permitida",
                detail: ex.Message));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(CrearProblem(
                status: StatusCodes.Status404NotFound,
                title:  "Barcaza no encontrada",
                detail: ex.Message));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex,
                "Error inesperado al fondear BarcazaId={BarcazaId} en Zona={Zona}",
                barcazaId, request.ZonaFondeo);

            return StatusCode(
                StatusCodes.Status500InternalServerError,
                CrearProblem(
                    status: StatusCodes.Status500InternalServerError,
                    title:  "Error interno del servidor",
                    detail: "No se pudo completar la operación de fondeo. Reintente más tarde."));
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // POST /api/convoyes/viaje/{viajeId}/adjuntar
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Adjunta barcazas a un convoy en viaje.
    /// </summary>
    /// <param name="viajeId">Identificador del viaje.</param>
    /// <param name="request">Payload con los identificadores string de las barcazas a adjuntar.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>
    /// <c>200 OK</c> si la operación fue exitosa.
    /// <c>400 Bad Request</c> si el payload es inválido o la transición de estado es ilegal.
    /// <c>404 Not Found</c> si el viaje no existe y no puede inicializarse.
    /// <c>500 Internal Server Error</c> ante cualquier falla inesperada.
    /// </returns>
    [HttpPost("viaje/{viajeId}/adjuntar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> AdjuntarBarcazas(
        [FromRoute] string viajeId,
        [FromBody]  AdjuntarBarcazasRequest request,
        CancellationToken ct)
    {
        try
        {
            await _convoyService.AdjuntarBarcazasAsync(viajeId, request, ct);
            return Ok(new { mensaje = "Barcazas adjuntadas correctamente al convoy." });
        }
        catch (ValidationException ex)
        {
            return BadRequest(CrearProblem(
                status: StatusCodes.Status400BadRequest,
                title:  "Datos de adjuntar inválidos",
                detail: ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(CrearProblem(
                status: StatusCodes.Status400BadRequest,
                title:  "Operación de adjuntar no permitida",
                detail: ex.Message));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(CrearProblem(
                status: StatusCodes.Status404NotFound,
                title:  "Viaje no encontrado",
                detail: ex.Message));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex,
                "Error inesperado al adjuntar barcazas al ViajeId={ViajeId}",
                viajeId);

            return StatusCode(
                StatusCodes.Status500InternalServerError,
                CrearProblem(
                    status: StatusCodes.Status500InternalServerError,
                    title:  "Error interno del servidor",
                    detail: "No se pudo completar la operación de adjuntar. Reintente más tarde."));
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // POST /api/convoyes/viaje/{viajeId}/separar
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Separa barcazas de un convoy en viaje.
    /// </summary>
    /// <param name="viajeId">Identificador del viaje.</param>
    /// <param name="request">Payload con los identificadores string de las barcazas a separar.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>
    /// <c>200 OK</c> si la operación fue exitosa.
    /// <c>400 Bad Request</c> si el payload es inválido o la transición de estado es ilegal.
    /// <c>404 Not Found</c> si el viaje no posee un convoy activo.
    /// <c>500 Internal Server Error</c> ante cualquier falla inesperada.
    /// </returns>
    [HttpPost("viaje/{viajeId}/separar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> SepararConvoy(
        [FromRoute] string viajeId,
        [FromBody]  SepararConvoyRequest request,
        CancellationToken ct)
    {
        try
        {
            await _convoyService.SepararConvoyAsync(viajeId, request, ct);
            return Ok(new { mensaje = "Barcazas separadas correctamente del convoy." });
        }
        catch (ValidationException ex)
        {
            return BadRequest(CrearProblem(
                status: StatusCodes.Status400BadRequest,
                title:  "Datos de separar inválidos",
                detail: ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(CrearProblem(
                status: StatusCodes.Status400BadRequest,
                title:  "Operación de separar no permitida",
                detail: ex.Message));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(CrearProblem(
                status: StatusCodes.Status404NotFound,
                title:  "Viaje no encontrado",
                detail: ex.Message));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex,
                "Error inesperado al separar barcazas del ViajeId={ViajeId}",
                viajeId);

            return StatusCode(
                StatusCodes.Status500InternalServerError,
                CrearProblem(
                    status: StatusCodes.Status500InternalServerError,
                    title:  "Error interno del servidor",
                    detail: "No se pudo completar la operación de separar. Reintente más tarde."));
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // HELPER PRIVADO
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Construye un <see cref="ProblemDetails"/> tipado con la instancia de request actual,
    /// cumpliendo RFC 9457 (anteriormente RFC 7807).
    /// </summary>
    private ProblemDetails CrearProblem(int status, string title, string detail) =>
        new()
        {
            Status   = status,
            Title    = title,
            Detail   = detail,
            Instance = HttpContext.Request.Path
        };
}
