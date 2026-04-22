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
        Task<bool> AgregarCargaAsync(string viajeNombreBuque, NuevaCargaDto nuevaCarga);
        Task<bool> ModificarCargaAsync(string id, ModificarCargaDto dto);
        Task<bool> EliminarCargaAsync(string viajeId, string cargaId);
    }
}
