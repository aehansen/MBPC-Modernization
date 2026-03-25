using Mbpc.Api.Models.Mongo;
using Mbpc.Api.DTOs;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Mbpc.Api.Services
{
    public interface IViajeService
    {
        // Trae la lista de posiciones reales desde last_mbpc
        Task<List<ViajePosicionMongo>> GetViajesAsync();
        
        // Busca un buque específico por su MMSI
        Task<ViajePosicionMongo?> GetViajeByMmsiAsync(string mmsi);

        Task<bool> IniciarViajeAsync(NuevoViajeDto nuevoViaje);
    }
}