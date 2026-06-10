using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Mbpc.Api.DTOs;

namespace Mbpc.Api.Services
{
    public interface IInspeccionService
    {
        Task<IEnumerable<InspeccionDto>> ObtenerInspeccionesAsync(string? viajeId = null, int pagina = 1, int tamanio = 50);
        Task<InspeccionDto?> ObtenerPorIdAsync(Guid id);
        Task<bool> CrearInspeccionAsync(CrearInspeccionDto dto);
        Task<bool> ModificarInspeccionAsync(Guid id, ModificarInspeccionDto dto);
        Task<bool> EliminarInspeccionAsync(Guid id, string viajeId);
    }
}
