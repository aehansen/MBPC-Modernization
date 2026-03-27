// IViajeService.cs
// Interfaz del servicio de viajes — actualizada con los 3 nuevos métodos de cambio de estado (Tarea 1).
// Agregar este archivo en Mbpc.Api/Services/IViajeService.cs (o donde resida actualmente).

using Mbpc.Api.Models.Mongo;
using Mbpc.Api.DTOs;

namespace Mbpc.Api.Services
{
    public interface IViajeService
    {
        // ── LECTURA (MongoDB) ────────────────────────────────────────────────
        Task<List<ViajePosicionMongo>> GetViajesAsync(int pagina = 1, int tamanio = 50);
        Task<ViajePosicionMongo?> GetViajeByMmsiAsync(string mmsi);
        Task<List<BarcoPuertoDto>> GetBarcosEnPuertoAsync();
        Task<List<ViajeHistoricoDto>> GetHistoricoAsync(FiltroHistoricoDto filtro);

        // ── ESCRITURA (Oracle + CQRS) ────────────────────────────────────────
        Task<bool> IniciarViajeAsync(NuevoViajeDto nuevoViaje);

        // ── TAREA 1: CAMBIO DE ESTADO DEL BUQUE ─────────────────────────────
        /// <summary>Zarpar: establece NavegationStatusDesc = "Navegando" en MongoDB.</summary>
        Task<bool> ZarparAsync(string id);

        /// <summary>Amarrar viaje: establece NavegationStatusDesc = "Amarrado" en MongoDB.</summary>
        Task<bool> AmarrarViajeAsync(string id);

        /// <summary>Fondear viaje: establece NavegationStatusDesc = "Fondeado" en MongoDB.</summary>
        Task<bool> FondearViajeAsync(string id);
    }
}
