using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Mbpc.Api.DTOs;

namespace Mbpc.Api.Services
{
    public interface IViajeComplementoService
    {
        /// <summary>
        /// Obtiene todas las notas, agencias y datos PBIP asociados a un viaje en MongoDB.
        /// </summary>
        Task<ViajeComplementosDto?> ObtenerComplementosPorViajeIdAsync(string viajeId, CancellationToken ct = default);

        /// <summary>
        /// Inyecta de forma atómica una nueva nota auditada en el array de bitácora en Mongo.
        /// </summary>
        Task<NotaBitacoraDto> AgregarNotaBitacoraAsync(string viajeId, AgregarNotaBitacoraDto dto, CancellationToken ct = default);

        /// <summary>
        /// Sobreescribe de manera atómica el listado de agencias intervinientes para el viaje.
        /// </summary>
        Task ActualizarAgenciasAsync(string viajeId, List<AsignarAgenciaDto> dtos, CancellationToken ct = default);

        /// <summary>
        /// Actualiza o hidrata el nodo embebido de información PBIP de protección marítima.
        /// </summary>
        Task<bool> ActualizarDatosPbipAsync(string viajeId, ActualizarDatosPbipDto dto, CancellationToken ct = default);
    }
}