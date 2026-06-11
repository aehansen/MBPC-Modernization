using System.Threading;
using System.Threading.Tasks;

namespace Mbpc.Api.Services
{
    public interface IAisIngestionService
    {
        Task SincronizarPosicionesAisAsync(CancellationToken cancellationToken = default);
    }
}
