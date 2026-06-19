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
        /// Finalizar: transición final del viaje (bloqueo paranoico: barcazas/inspectores/prácticos).
        /// </summary>
        Task<bool> FinalizarViajeAsync(string id);

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

        // ── PERSONAL EXTERNO (Hito 9.0) ──────────────────────────────────────

        Task<bool> EmbarcarPracticoAsync(string viajeId, EmbarcarPracticoDto dto);
        Task<bool> DesembarcarPracticoAsync(string viajeId, string dni, DesembarcarPracticoDto dto);
        Task<bool> EmbarcarInspectorAsync(string viajeId, EmbarcarInspectorDto dto);
        Task<bool> DesembarcarInspectorAsync(string viajeId, string dni, DesembarcarInspectorDto dto);

        /// <summary>
        /// Retorna el personal externo (inspectores y prácticos) embarcado en un viaje.
        /// Lee directamente de ViajeDetalleMongo sin caché (foto real).
        /// </summary>
        Task<PersonalViajeDto?> ObtenerPersonalAsync(string viajeId);

        /// <summary>
        /// Transfiere la jurisdicción de un viaje a otra dependencia (Costera).
        /// </summary>
        Task<bool> TransferirJurisdiccionAsync(string id, TransferirJurisdiccionDto dto);

        /// <summary>
        /// Registra una solicitud de transferencia pendiente de aprobación.
        /// </summary>
        Task RegistrarSolicitudTransferenciaAsync(string id, int nuevaCosteraId);

        /// <summary>
        /// Limpia/cancela una solicitud de transferencia pendiente de aprobación.
        /// </summary>
        Task LimpiarSolicitudTransferenciaAsync(string id);

        /// <summary>
        /// Obtiene la lista de transferencias pendientes para la costera indicada (origen o destino).
        /// </summary>
        Task<List<ViajeDto>> ObtenerTransferenciasPendientesAsync(int costeraId);

        /// <summary>
        /// Aprueba y ejecuta la transferencia de jurisdicción pendiente.
        /// </summary>
        Task<bool> AprobarTransferenciaAsync(string id, int operadorCosteraId);

        /// <summary>
        /// Rechaza/cancela la transferencia de jurisdicción pendiente.
        /// </summary>
        Task<bool> RechazarTransferenciaAsync(string id);

        /// <summary>
        /// Valida si el viaje está finalizado y lanza InvalidOperationException si es así.
        /// </summary>
        Task ThrowIfViajeFinalizadoAsync(string viajeId, bool permitirRectificacion = false);

        // ── HERRAMIENTAS SOPORTE (Hito: Personal Externo, Bitácoras y Herramientas Soporte) ──
        Task<List<EtapaDetalleDto>?> ObtenerEtapasAsync(string viajeId);
        Task<bool> IntercalarEtapaAsync(string viajeId, IntercalarEtapaDto dto);
        Task<bool> ReiniciarViajeAsync(string viajeId);
    }
}
