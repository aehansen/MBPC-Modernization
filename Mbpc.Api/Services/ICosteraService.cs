using System.Collections.Generic;
using System.Threading.Tasks;
using Mbpc.Api.DTOs;

namespace Mbpc.Api.Services
{
    public interface ICosteraService
    {
        Task<IEnumerable<CosteraDto>> ObtenerLimitesJurisdiccionalesAsync();
        Task<CosteraDto?> ObtenerLimitePorCosteraIdAsync(int costeraId);
    }
}
