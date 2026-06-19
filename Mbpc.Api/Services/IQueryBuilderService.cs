using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Mbpc.Api.DTOs;

namespace Mbpc.Api.Services
{
    public interface IQueryBuilderService
    {
        Task<List<MetadataEntityDto>> ObtenerMetadataAsync(CancellationToken ct = default);
        Task<QueryResultDto> EjecutarConsultaAsync(QueryRequestDto request, CancellationToken ct = default);
        Task<byte[]> GenerarExcelAsync(QueryResultDto result);
    }
}
