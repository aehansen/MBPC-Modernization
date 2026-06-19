using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Mbpc.Api.DTOs;
using Mbpc.Api.Services;

namespace Mbpc.Api.Controllers
{
    [ApiController]
    [Route("api/querybuilder")]
    [Authorize]
    public class QueryBuilderController : ControllerBase
    {
        private readonly IQueryBuilderService _queryBuilderService;
        private readonly ILogger<QueryBuilderController> _logger;

        public QueryBuilderController(IQueryBuilderService queryBuilderService, ILogger<QueryBuilderController> logger)
        {
            _queryBuilderService = queryBuilderService;
            _logger = logger;
        }

        [HttpGet("metadata")]
        public async Task<IActionResult> GetMetadata()
        {
            _logger.LogInformation("Endpoint GET /api/querybuilder/metadata invocado.");
            try
            {
                var metadata = await _queryBuilderService.ObtenerMetadataAsync();
                return Ok(metadata);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al recuperar metadatos del Query Builder.");
                return StatusCode(500, new { mensaje = "Ocurrió un error al obtener la configuración de metadatos." });
            }
        }

        [HttpPost("ejecutar")]
        public async Task<IActionResult> EjecutarConsulta([FromBody] QueryRequestDto request)
        {
            _logger.LogInformation("Endpoint POST /api/querybuilder/ejecutar invocado.");
            try
            {
                var result = await _queryBuilderService.EjecutarConsultaAsync(request);
                return Ok(result);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Solicitud de consulta inválida.");
                return BadRequest(new { mensaje = ex.Message });
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al ejecutar consulta personalizada.");
                return StatusCode(500, new { mensaje = "Ocurrió un error interno al ejecutar la consulta dinámica." });
            }
        }

        [HttpPost("exportar")]
        public async Task<IActionResult> ExportarExcel([FromBody] QueryRequestDto request)
        {
            _logger.LogInformation("Endpoint POST /api/querybuilder/exportar invocado.");
            try
            {
                var result = await _queryBuilderService.EjecutarConsultaAsync(request);
                var excelBytes = await _queryBuilderService.GenerarExcelAsync(result);

                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                var fileName = $"ConsultaDinámica_{request.EntidadPrincipal}_{timestamp}.xlsx";

                return File(excelBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al exportar consulta a Excel.");
                return StatusCode(500, new { mensaje = "Ocurrió un error al generar la exportación a Excel." });
            }
        }
    }
}
