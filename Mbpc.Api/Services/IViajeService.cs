// IViajeService.cs
// Se agrega ReanudarAsync para completar la máquina de estados (EJE 2).
// Sin ReanudarAsync no existe transición legal desde Fondeado → Navegando.
// Namespace: Mbpc.Api.Services

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

        // ── MAPA (ArcGIS) ────────────────────────────────────────────────────
        Task<List<MapaViajeDto>> GetMapaViajesAsync(string? mmsi = null, string? nombreBuque = null);

        // ── ESCRITURA (Oracle + CQRS) ────────────────────────────────────────
        Task<bool> IniciarViajeAsync(NuevoViajeDto nuevoViaje);

        // ── MÁQUINA DE ESTADOS (EJE 2) ───────────────────────────────────────

        /// <summary>
        /// Zarpar: Amarrado/Reanudado → Navegando.
        /// Transición ilegal si el estado actual es Fondeado.
        /// </summary>
        Task<bool> ZarparAsync(string id);

        /// <summary>
        /// Amarrar: Navegando/Reanudado → Amarrado.
        /// </summary>
        Task<bool> AmarrarViajeAsync(string id);

        /// <summary>
        /// Fondear: Navegando/Reanudado → Fondeado.
        /// </summary>
        Task<bool> FondearViajeAsync(string id);

        /// <summary>
        /// Reanudar: Fondeado → Reanudado.
        /// Paso previo OBLIGATORIO para que un buque Fondeado pueda volver a Zarpar.
        /// </summary>
        Task<bool> ReanudarViajeAsync(string id);
    }
}