// IViajeService.cs
// EJE 3 — Filtrado Multitenant Geográfico (CosteraId).
// Todos los métodos de lectura principales reciben 'costeraId' obligatorio
// para garantizar que cada usuario solo acceda a los datos de su jurisdicción.
// Namespace: Mbpc.Api.Services

using Mbpc.Api.Models.Mongo;
using Mbpc.Api.DTOs;

namespace Mbpc.Api.Services
{
    public interface IViajeService
    {
        // ── LECTURA (MongoDB) ────────────────────────────────────────────────

        /// <summary>
        /// Retorna viajes paginados filtrados por la costera del usuario autenticado.
        /// </summary>
        Task<List<ViajePosicionMongo>> GetViajesAsync(string costeraId, int pagina = 1, int tamanio = 50);

        /// <summary>
        /// Retorna la última posición de un buque por MMSI, validando que pertenezca
        /// a la costera del usuario autenticado.
        /// </summary>
        Task<ViajePosicionMongo?> GetViajeByMmsiAsync(string mmsi, string costeraId);

        /// <summary>
        /// Retorna barcos en puerto (Amarrado/Fondeado) dentro de la jurisdicción
        /// de la costera indicada.
        /// </summary>
        Task<List<BarcoPuertoDto>> GetBarcosEnPuertoAsync(string costeraId);

        /// <summary>
        /// Retorna el histórico de viajes filtrado por costera. La costera se pasa
        /// al stored procedure de Oracle como parámetro adicional.
        /// </summary>
        Task<List<ViajeHistoricoDto>> GetHistoricoAsync(FiltroHistoricoDto filtro, string costeraId);

        // ── MAPA (ArcGIS) ────────────────────────────────────────────────────

        /// <summary>
        /// Retorna los puntos del mapa restringidos a la costera del usuario.
        /// Los filtros opcionales de mmsi/nombre se aplican en memoria sobre
        /// el resultado ya acotado por costeraId.
        /// </summary>
        Task<List<MapaViajeDto>> GetMapaViajesAsync(string costeraId, string? mmsi = null, string? nombreBuque = null);

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
