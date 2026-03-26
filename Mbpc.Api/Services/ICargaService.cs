using Mbpc.Api.DTOs;
using System.Collections.Generic;

namespace Mbpc.Api.Services
{
    public interface ICargaService
    {
        IEnumerable<CargaDto> ObtenerCargasPorViaje(string vesselName);
        bool AmarrarBarcaza(string id, string nuevoMuelle);
        bool FondearBarcaza(string id, string zonaFondeo);
        bool CargarBarcaza(string id, double toneladas);
        bool DescargarBarcaza(string id, double toneladas);
    }
}