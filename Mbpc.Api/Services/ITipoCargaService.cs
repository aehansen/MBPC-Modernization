using Mbpc.Api.DTOs;

namespace Mbpc.Api.Services
{
    public interface ITipoCargaService
    {
        Task<IEnumerable<TipoCargaDto>> BuscarAutocompleteAsync(string query);
        Task<int> SincronizarDesdeOracleAsync();
        Task<TipoCargaDto?> ObtenerPorIdAsync(int oracleId);
    }
}
