using Mbpc.Api.DTOs;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Mbpc.Api.Services
{
    public interface ICargaService
    {
        IEnumerable<CargaDto> ObtenerCargasPorViaje(string parametroBusqueda);
        bool AmarrarBarcaza(string id, string nuevoMuelle);
        bool FondearBarcaza(string id, string zonaFondeo);
        bool CargarBarcaza(string id, double toneladas);
        bool DescargarBarcaza(string id, double toneladas);
        
        // --- NUEVO MÉTODO ---
        Task<bool> AgregarCargaAsync(string nombreBuque, NuevaCargaDto nuevaCarga);
    }
}