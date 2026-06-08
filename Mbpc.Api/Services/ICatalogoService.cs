using Mbpc.Api.DTOs;

namespace Mbpc.Api.Services
{
    public interface ICatalogoService
    {
        Task<IEnumerable<PuntoControlDto>> ObtenerPuntosControlAsync();
        Task<IEnumerable<MuelleDto>> ObtenerMuellesAsync();
    }
}
