using System.Threading;
using System.Threading.Tasks;

namespace Mbpc.Api.Services
{
    public interface IReconciliacionService
    {
        Task EjecutarCicloReconciliacionAsync(CancellationToken cancellationToken = default);
        Task<bool> ForzarReconciliacionViajeAsync(long travelId, CancellationToken cancellationToken = default);
    }
}
