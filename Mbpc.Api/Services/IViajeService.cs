// IViajeService.cs
// EJE 3 — Filtrado Multitenant Geográfico (CosteraId).
// El CosteraId ya NO se pasa como parámetro desde el Controller.
// Cada implementación lo resuelve internamente vía IHttpContextAccessor,
// leyendo el Claim "CosteraId" del JWT del usuario autenticado.
// Namespace: Mbpc.Api.Services

using Mbpc.Api.Models.Mongo;
using Mbpc.Api.DTOs;

namespace Mbpc.Api.Services
{
    public interface IViajeService
    {
        // ── LECTURA (MongoDB) ────────────────────────────────────────────────

        /// <summary>
        /// Retorna viajes paginados. El filtro por CosteraId se aplica internamente
        /// desde el contexto HTTP del usuario autenticado.
        /// El parámetro opcional <paramref name="nombre"/> aplica un filtro Regex
        /// case-insensitive sobre VesselName directamente en MongoDB, antes de paginar.
        /// </summary>
        Task<List<ViajePosicionMongo>> GetViajesAsync(string? nombre = null, int pagina = 1, int tamanio = 50);

        /// <summary>
        /// Retorna viajes proyectados como DTOs, paginados. El filtro por CosteraId
        /// se aplica internamente desde el contexto HTTP del usuario autenticado.
        /// El parámetro opcional <paramref name="nombre"/> aplica un filtro Regex
        /// case-insensitive sobre VesselName directamente en MongoDB, antes de paginar.
        /// </summary>
        Task<List<ViajeDto>> ObtenerViajesDtoAsync(string? nombre, int pagina, int tamanio);

        /// <summary>
        /// Retorna la última posición de un buque por MMSI. Valida internamente
        /// que el registro pertenezca a la costera del usuario autenticado.
        /// </summary>
        Task<ViajePosicionMongo?> GetViajeByMmsiAsync(string mmsi);

        /// <summary>
        /// Retorna el documento de detalle operativo de un viaje por su ObjectId de MongoDB,
        /// junto con el TravelId relacional obtenido desde la colección de posiciones.
        ///
        /// La tupla garantiza que el TravelId siempre esté disponible para el fallback a Oracle,
        /// incluso cuando el documento de detalle no existe o tiene el campo IdViaje vacío.
        ///   Detalle == null, TravelId == 0  →  no se encontró la posición base.
        ///   Detalle == null, TravelId  > 0  →  posición encontrada pero sin detalle en Mongo (sync pendiente).
        ///   Detalle != null, TravelId  > 0  →  caso nominal; usar Detalle.Barcazas si Count > 0.
        /// </summary>
        Task<(ViajeDetalleMongo? Detalle, long TravelId)> GetViajeDetalleByIdAsync(string id, CancellationToken ct = default);

        /// <summary>
        /// Retorna barcos en puerto (Amarrado/Fondeado) dentro de la jurisdicción
        /// de la costera del usuario autenticado.
        /// </summary>
        Task<List<BarcoPuertoDto>> GetBarcosEnPuertoAsync();

        /// <summary>
        /// Retorna el histórico de viajes filtrado por la costera del usuario
        /// autenticado. La costera se pasa al stored procedure de Oracle como
        /// parámetro adicional de forma transparente.
        /// </summary>
        Task<List<ViajeHistoricoDto>> GetHistoricoAsync(FiltroHistoricoDto filtro);

        // ── MAPA (ArcGIS) ────────────────────────────────────────────────────

        /// <summary>
        /// Retorna los puntos del mapa restringidos a la costera del usuario
        /// autenticado. Los filtros opcionales de mmsi/nombre se aplican en memoria
        /// sobre el resultado ya acotado por CosteraId.
        /// </summary>
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

        // ── POSICIONAMIENTO AIS (EJE 4) ──────────────────────────────────────

        /// <summary>
        /// Actualiza la posición geográfica de un buque (lat/lng + timestamp).
        ///
        /// Reglas de negocio aplicadas internamente:
        ///   • Haversine: calcula distancia entre posición anterior y nueva.
        ///   • Cinemática: si velocidad calculada > 60 kn → lanza InvalidOperationException.
        ///   • Persistencia dual: actualiza el doc activo en MongoDB E inserta copia en tracklog.
        ///
        /// Retorna null si no existe el documento con ese Id para la costera autenticada.
        /// Lanza InvalidOperationException (mensaje comienza con "Cinemática inválida")
        /// si la velocidad calculada supera el límite físico permitido.
        /// </summary>
        Task<PosicionActualizadaResultDto?> ActualizarPosicionAsync(string id, ActualizarPosicionDto dto);
    }
}
