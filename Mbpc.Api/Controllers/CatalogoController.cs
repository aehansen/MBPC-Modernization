using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Mbpc.Api.Services;
using Mbpc.Api.DTOs;
using Mbpc.Api.DTOs.Catalogos;

namespace Mbpc.Api.Controllers
{
    /// <summary>
    /// Controller de gestión de catálogos del sistema MBPC (Muelles y Puntos de Control).
    ///
    /// DISEÑO (Controlador Anoréxico):
    ///   - No contiene lógica de negocio.
    ///   - Extrae el CosteraId del JWT y lo pasa al servicio. NUNCA lo acepta del cliente.
    ///   - Las validaciones de dominio (código de muelle duplicado, etc.) las lanza
    ///     el servicio como InvalidOperationException → Middleware global → HTTP 422.
    ///
    /// RUTAS:
    ///   GET  /api/catalogos/puntos-control → Lista todos los puntos de control
    ///   GET  /api/catalogos/muelles        → Lista todos los muelles activos
    ///   GET  /api/catalogos/muelles/{id}   → Detalle de un muelle
    ///   POST /api/catalogos/muelles        → Alta de muelle
    ///   PUT  /api/catalogos/muelles/{id}   → Edición de muelle
    /// </summary>
    [ApiController]
    [Route("api/catalogos")]
    [Authorize]
    public class CatalogoController : ControllerBase
    {
        private readonly ICatalogoService            _catalogoService;
        private readonly ILogger<CatalogoController> _logger;

        public CatalogoController(
            ICatalogoService            catalogoService,
            ILogger<CatalogoController> logger)
        {
            _catalogoService = catalogoService;
            _logger          = logger;
        }

        // ── PUNTOS DE CONTROL (solo lectura) ──────────────────────────────────

        /// <summary>Lista todos los puntos de control del sistema.</summary>
        [HttpGet("puntos-control")]
        public async Task<IActionResult> ObtenerPuntosControl()
        {
            var resultado = await _catalogoService.ObtenerPuntosControlAsync();
            return Ok(resultado);
        }

        // ── MUELLES — LECTURA ─────────────────────────────────────────────────

        /// <summary>Lista todos los muelles activos del sistema.</summary>
        [HttpGet("muelles")]
        public async Task<IActionResult> ObtenerMuelles()
        {
            var resultado = await _catalogoService.ObtenerMuellesAsync();
            return Ok(resultado);
        }

        /// <summary>
        /// Retorna el detalle completo de un muelle por su Id.
        /// Retorna 404 si el muelle no existe.
        /// </summary>
        [HttpGet("muelles/{id:int}")]
        public async Task<IActionResult> ObtenerMuelle(int id)
        {
            _logger.LogInformation("ObtenerMuelle — Id: {Id}.", id);

            var muelle = await _catalogoService.ObtenerMuellePorIdAsync(id);

            if (muelle is null)
                return NotFound(new { mensaje = $"No se encontró un muelle con Id={id}." });

            return Ok(muelle);
        }

        // ── MUELLES — ESCRITURA ───────────────────────────────────────────────

        /// <summary>
        /// Da de alta un nuevo muelle en Oracle.
        /// El CosteraId se extrae del JWT. Si el Código ya existe,
        /// el servicio lanza InvalidOperationException → Middleware → HTTP 422.
        /// </summary>
        [HttpPost("muelles")]
        public async Task<IActionResult> CrearMuelle([FromBody] MuelleAltaDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var costeraId = ObtenerCosteraId();
            if (costeraId is null)
                return Forbid();

            _logger.LogInformation(
                "CrearMuelle — Nombre: '{Nombre}', Código: '{Codigo}', CosteraId: {CosteraId}.",
                dto.Nombre, dto.Codigo, costeraId);

            var muelleCreado = await _catalogoService.CrearMuelleAsync(dto, costeraId.Value);

            return CreatedAtAction(
                nameof(ObtenerMuelle),
                new { id = muelleCreado.Id },
                muelleCreado);
        }

        /// <summary>
        /// Edita los datos de un muelle existente.
        /// El id de la ruta debe coincidir con el Id del body.
        /// </summary>
        [HttpPut("muelles/{id:int}")]
        public async Task<IActionResult> EditarMuelle(int id, [FromBody] MuelleEditDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            if (id != dto.Id)
                return BadRequest(new { mensaje = $"El Id de la ruta ({id}) no coincide con el Id del body ({dto.Id})." });

            _logger.LogInformation(
                "EditarMuelle — Id: {Id}, Nombre: '{Nombre}'.",
                dto.Id, dto.Nombre);

            var muelleActualizado = await _catalogoService.EditarMuelleAsync(dto);
            return Ok(muelleActualizado);
        }

        // ── Helper Privado ────────────────────────────────────────────────────

        /// <summary>
        /// Extrae el CosteraId del Claim JWT del usuario autenticado.
        /// Retorna null si el claim no existe o no es un entero válido.
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
                _logger.LogWarning(
                    "ObtenerCosteraId — El Claim 'CosteraId' no es un entero válido: '{Value}'.",
                    claimValue);
                return null;
            }

            return costeraId;
        }
    }
}
