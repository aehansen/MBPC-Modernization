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

        /// <summary>
        /// Transfiere un tonelaje específico de carga desde un viaje y barcaza de origen hacia otro de destino.
        /// </summary>
        Task<bool> TransferirCargaAsync(string viajeOrigenId, string cargaOrigenId, TransferirCargaDto dto);

        /// <summary>
        /// Realiza una rectificación histórica del tonelaje de una carga en un viaje, incluso si el viaje está finalizado.
        /// </summary>
        Task<bool> RectificarCargaAsync(string viajeId, string cargaId, RectificarCargaDto dto);

        Task<bool> SincronizarAmarreConvoyAsync(string viajeId);
        Task<bool> SincronizarZarpeConvoyAsync(string viajeId);
        Task<bool> SincronizarFondeoConvoyAsync(string viajeId);
    }
}
