using System.Data;
using Mbpc.Api.DTOs;

namespace Mbpc.Api.Services
{
    public interface IReporteService
    {
        Task<DataTable> EjecutarReporteAsync(string reportName, List<ReportParamDto> parameters);
        Task<byte[]> GenerarExcelAsync(DataTable data);
    }
}
