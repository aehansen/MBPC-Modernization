using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Mbpc.Api.DTOs;
using Mbpc.Api.Services;

namespace Mbpc.Api.Controllers;

[ApiController]
[Route("api/reportes")]
[Authorize]
public class ReporteController : ControllerBase
{
    private readonly IReporteService _reporteService;
    private readonly ILogger<ReporteController> _logger;

    public ReporteController(IReporteService reporteService, ILogger<ReporteController> logger)
    {
        _reporteService = reporteService;
        _logger = logger;
    }

    [HttpGet("{nombre}/data")]
    public async Task<IActionResult> GetData([FromRoute] string nombre, [FromQuery] ReporteRequestDto request)
    {
        _logger.LogInformation("Endpoint GET /api/reportes/{Nombre}/data invocado.", nombre);
        
        var dataTable = await _reporteService.EjecutarReporteAsync(nombre, request.Parametros ?? new());
        var mappedData = ConvertDataTableToList(dataTable);
        
        return Ok(mappedData);
    }

    [HttpGet("{nombre}/exportar")]
    public async Task<IActionResult> Exportar([FromRoute] string nombre, [FromQuery] ReporteRequestDto request)
    {
        _logger.LogInformation("Endpoint GET /api/reportes/{Nombre}/exportar invocado.", nombre);
        
        var dataTable = await _reporteService.EjecutarReporteAsync(nombre, request.Parametros ?? new());
        var excelBytes = await _reporteService.GenerarExcelAsync(dataTable);
        
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var fileName = $"Reporte_{nombre}_{timestamp}.xlsx";
        
        return File(excelBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }

    private List<Dictionary<string, object?>> ConvertDataTableToList(DataTable table)
    {
        var rows = new List<Dictionary<string, object?>>();
        foreach (DataRow row in table.Rows)
        {
            var dict = new Dictionary<string, object?>();
            foreach (DataColumn col in table.Columns)
            {
                var val = row[col];
                dict[col.ColumnName] = val == DBNull.Value ? null : val;
            }
            rows.Add(dict);
        }
        return rows;
    }
}
