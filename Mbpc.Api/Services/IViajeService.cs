using Mbpc.Api.Models.Mongo;
using Mbpc.Api.DTOs;

namespace Mbpc.Api.Services
{
    public interface IViajeService
    {
        /// <summary>
        /// Trae posiciones paginadas desde la colección last_mbpc de MongoDB.
        /// </summary>
        Task<List<ViajePosicionMongo>> GetViajesAsync(int pagina = 1, int tamanio = 50);

        /// <summary>
        /// Busca un buque específico por su MMSI.
        /// </summary>
        Task<ViajePosicionMongo?> GetViajeByMmsiAsync(string mmsi);

        /// <summary>
        /// Registra el inicio de un viaje en Oracle a través del SP PKG_MBPC_VIAJES.SP_CREAR_VIAJE.
        /// </summary>
        Task<bool> IniciarViajeAsync(NuevoViajeDto nuevoViaje);
        Task<List<BarcoPuertoDto>> GetBarcosEnPuertoAsync();
        Task<List<ViajeHistoricoDto>> GetHistoricoAsync(FiltroHistoricoDto filtro);
    }
}
