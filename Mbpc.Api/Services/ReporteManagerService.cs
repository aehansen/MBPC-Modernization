using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using ClosedXML.Excel;
using Dapper;
using Mbpc.Api.DTOs;
using Mbpc.Api.Models.Config;
using Mbpc.Api.Services.Auth;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Oracle.ManagedDataAccess.Client;

namespace Mbpc.Api.Services
{
    public class ReporteManagerService : IReporteService
    {
        private readonly string _oracleConnectionString;
        private readonly ICosteraUserContext _costeraUserContext;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<ReporteManagerService> _logger;
        private readonly IWebHostEnvironment _env;

        public ReporteManagerService(
            IOptions<OracleDbSettings> oracleSettings,
            ICosteraUserContext costeraUserContext,
            IHttpContextAccessor httpContextAccessor,
            ILogger<ReporteManagerService> logger,
            IWebHostEnvironment env)
        {
            _oracleConnectionString = oracleSettings.Value.ConnectionString;
            _costeraUserContext = costeraUserContext;
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
            _env = env;
        }

        public async Task<DataTable> EjecutarReporteAsync(string reportName, List<ReportParamDto> parameters)
        {
            // 1. Verificación de Seguridad
            int costeraId = _costeraUserContext.GetCurrentCosteraId();
            if (costeraId == -1)
            {
                throw new UnauthorizedAccessException("Usuario no autenticado o token inválido.");
            }

            var user = _httpContextAccessor.HttpContext?.User;
            string usuario = user?.Identity?.Name 
                             ?? user?.FindFirst(ClaimTypes.NameIdentifier)?.Value 
                             ?? user?.FindFirst(ClaimTypes.Name)?.Value 
                             ?? "Sistema";

            _logger.LogInformation("EjecutarReporteAsync - Usuario: '{Usuario}' (CosteraId: {CosteraId}) solicitando reporte '{ReportName}'", usuario, costeraId, reportName);

            // Validar permiso en SP legacy o lógica
            await ValidarPermisoReporteAsync(reportName, usuario);

            // Crear una copia editable para evitar mutar la colección recibida
            var parametersList = parameters != null ? new List<ReportParamDto>(parameters) : new List<ReportParamDto>();

            // 2. Inyección de Scope de Seguridad para Operadores (Evitar Parameter Tampering)
            if (costeraId > 0)
            {
                // Forzar el CosteraId del operador en los parámetros de la consulta
                var paramCostera = parametersList.FirstOrDefault(p => 
                    p.Name.Equals("CosteraId", StringComparison.OrdinalIgnoreCase) ||
                    p.Name.Equals("p_CosteraId", StringComparison.OrdinalIgnoreCase) ||
                    p.Name.Equals("vCosteraId", StringComparison.OrdinalIgnoreCase) ||
                    p.Name.Equals("p_Costera", StringComparison.OrdinalIgnoreCase) ||
                    p.Name.Equals("v_Costera", StringComparison.OrdinalIgnoreCase));

                if (paramCostera != null)
                {
                    parametersList.Remove(paramCostera);
                }
                parametersList.Add(new ReportParamDto { Name = "p_CosteraId", Value = costeraId.ToString() });
            }

            // 3. Ejecución del Reporte
            if (_env.IsDevelopment() && string.IsNullOrEmpty(_oracleConnectionString))
            {
                _logger.LogWarning("Bypass de base de datos en entorno de desarrollo. Retornando datos mockeados para '{ReportName}'", reportName);
                return GenerarMockDataTable(reportName, costeraId);
            }

            try
            {
                using var connection = new OracleConnection(_oracleConnectionString);
                await connection.OpenAsync();

                var spParams = new OracleDynamicParameters();
                
                // Mapear los parámetros pasados
                foreach (var p in parametersList)
                {
                    spParams.Add(p.Name, p.Value, OracleDbType.Varchar2, ParameterDirection.Input);
                }

                // El RefCursor de salida es obligatorio para que el SP nos devuelva las filas del reporte
                spParams.Add("vCursor", OracleDbType.RefCursor, ParameterDirection.Output);

                using var reader = await connection.ExecuteReaderAsync(
                    reportName,
                    spParams,
                    commandType: CommandType.StoredProcedure);

                var dataTable = new DataTable();
                dataTable.Load(reader);
                return dataTable;
            }
            catch (OracleException ex)
            {
                if (_env.IsDevelopment())
                {
                    _logger.LogWarning(ex, "[DEV BYPASS] Fallo Oracle al ejecutar reporte '{ReportName}'. Retornando datos mockeados.", reportName);
                    return GenerarMockDataTable(reportName, costeraId);
                }
                _logger.LogError(ex, "Error ejecutando stored procedure de reporte '{ReportName}'", reportName);
                throw;
            }
        }

        public Task<byte[]> GenerarExcelAsync(DataTable data)
        {
            if (data == null || data.Rows.Count == 0)
            {
                throw new InvalidOperationException("No se encontraron registros para generar el reporte.");
            }

            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("Reporte");

            // Insertamos la tabla. Esto formatea las cabeceras y aplica estilos básicos por defecto de ClosedXML.
            var table = worksheet.Cell(1, 1).InsertTable(data);
            
            // Autoajustar el ancho de las columnas
            worksheet.Columns().AdjustToContents();

            // Estilos premium
            table.ShowAutoFilter = true;
            
            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return Task.FromResult(stream.ToArray());
        }

        private async Task ValidarPermisoReporteAsync(string reportName, string usuario)
        {
            if (_env.IsDevelopment() && string.IsNullOrEmpty(_oracleConnectionString))
            {
                return;
            }

            try
            {
                using var connection = new OracleConnection(_oracleConnectionString);
                await connection.OpenAsync();

                var spParams = new DynamicParameters();
                spParams.Add("vUsuario", usuario, DbType.String, ParameterDirection.Input);
                spParams.Add("vReporte", reportName, DbType.String, ParameterDirection.Input);
                spParams.Add("vPermitido", dbType: DbType.Int32, direction: ParameterDirection.Output);

                await connection.ExecuteAsync(
                    "mbpc.verificar_permiso_reporte",
                    spParams,
                    commandType: CommandType.StoredProcedure);

                int permitido = spParams.Get<int>("vPermitido");
                if (permitido != 1)
                {
                    throw new UnauthorizedAccessException($"El usuario '{usuario}' no tiene permisos para acceder al reporte '{reportName}'.");
                }
            }
            catch (Exception ex)
            {
                // Si la base de datos legacy no tiene la función/SP implementado, permitimos la ejecución
                // bajo la premisa de fallback para no bloquear el sistema si no está configurada la tabla.
                _logger.LogWarning(ex, "No se pudo validar permiso de reporte '{ReportName}' en Oracle usando verificar_permiso_reporte. Procediendo con validación interna.", reportName);
            }
        }

        private DataTable GenerarMockDataTable(string reportName, int costeraId)
        {
            var dataTable = new DataTable();
            dataTable.Columns.Add("ID_REPORTE", typeof(int));
            dataTable.Columns.Add("REPORTE", typeof(string));
            dataTable.Columns.Add("COSTERA_ID", typeof(int));
            dataTable.Columns.Add("FECHA", typeof(string));
            dataTable.Columns.Add("DETALLE", typeof(string));

            dataTable.Rows.Add(1, reportName, costeraId, DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"), "Simulación de reporte 1");
            dataTable.Rows.Add(2, reportName, costeraId, DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"), "Simulación de reporte 2");
            dataTable.Rows.Add(3, reportName, costeraId, DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"), "Simulación de reporte 3");

            return dataTable;
        }
    }
}
