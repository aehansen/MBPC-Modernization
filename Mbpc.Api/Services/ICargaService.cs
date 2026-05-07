using Mbpc.Api.DTOs;

namespace Mbpc.Api.Services
{
    public interface ICargaService
    {
        Task<IEnumerable<CargaDto>> ObtenerCargasPorViaje(string viajeId);
        bool AmarrarBarcaza(string id, string nuevoMuelle);
        bool FondearBarcaza(string id, string zonaFondeo);
        bool CargarBarcaza(string id, double toneladas);
        bool DescargarBarcaza(string id, double toneladas);

        /// <param name="viajeId">ObjectId de MongoDB del viaje (scoping seguro).</param>
        Task<bool> AgregarCargaAsync(string viajeId, NuevaCargaDto nuevaCarga);

        /// <param name="id">Identificador de la carga (Nombre/BarcazaId) a modificar.</param>
        Task<bool> ModificarCargaAsync(string id, ModificarCargaDto dto);

        /// <param name="viajeId">ObjectId de MongoDB del viaje (scoping seguro).</param>
        /// <param name="cargaId">Identificador de la carga a eliminar.</param>
        Task<bool> EliminarCargaAsync(string viajeId, string cargaId);

        Task<bool> SincronizarAmarreConvoyAsync(string viajeId);
    }
}
