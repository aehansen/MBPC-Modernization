using System.Threading;
using Mbpc.Api.DTOs;

namespace Mbpc.Api.Services
{
    public interface ICargaService
    {
        Task<IEnumerable<CargaDto>> ObtenerCargasPorViaje(string viajeId);
        Task<bool> AmarrarBarcaza(string id, string nuevoMuelle, CancellationToken cancellationToken = default);
        Task<bool> FondearBarcaza(string id, string zonaFondeo, CancellationToken cancellationToken = default);
        Task<bool> CargarBarcaza(string id, double toneladas, CancellationToken cancellationToken = default);
        Task<bool> DescargarBarcaza(string id, double toneladas, CancellationToken cancellationToken = default);

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
