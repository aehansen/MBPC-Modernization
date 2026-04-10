// ─── Viaje principal (paginado desde MongoDB) ─────────────────────────────────

export interface ViajeDto {
  id: string;
  buque: string;
  ruta: string;
  fechaInicioFormateada: string;
  estadoActual: string;
  costeraId: string | null;
}

// ─── Viaje histórico (búsqueda global desde Oracle) ───────────────────────────

export interface ViajeHistoricoDto {
  id: string;
  buque: string;
  omi: string;
  matricula: string;
  origen: string;
  destino: string;
  fechaPartida: string;
  eta: string;
  estado: string;
  costeraId: string;
}

// ─── Criterio de búsqueda ─────────────────────────────────────────────────────

export type CriterioBusqueda = 'nombre' | 'omi' | 'matricula';

export interface BusquedaParams {
  criterio: CriterioBusqueda;
  valor: string;
}

// ─── Posición ─────────────────────────────────────────────────────────────────

export interface ActualizarPosicionRequest {
  latitud: number;
  longitud: number;
  fechaReporte: string; // ISO string
}

export interface ActualizarPosicionResponse {
  velocidadCalculadaKn: number;
  distanciaRecorridaNM: number;
}

// ─── Adaptador: ViajeHistoricoDto → ViajeDto ─────────────────────────────────

export function viajeHistoricoToDto(h: ViajeHistoricoDto): ViajeDto {
  return {
    id: h.id,
    buque: h.buque,
    ruta: `${h.origen} - ${h.destino}`,
    fechaInicioFormateada: h.fechaPartida, 
    estadoActual: h.estado,
    costeraId: h.costeraId,
  };
}

export enum DeclaracionMalvinasEnum {
  // ── Viene de Malvinas ───────────────────────────────────────────────
  NoVieneDeMalvinas_L = "NoVieneDeMalvinas_L",
  VieneDeMalvinas_AutorizadoCPER_M = "VieneDeMalvinas_AutorizadoCPER_M",
  VieneDeMalvinas_NoAutorizado_Infraccion_Extranjero_W = "VieneDeMalvinas_NoAutorizado_Infraccion_Extranjero_W",
  VieneDeMalvinas_NoSolicitoAutorizacion_Amarra_Y = "VieneDeMalvinas_NoSolicitoAutorizacion_Amarra_Y",
  VieneDeMalvinas_SolicitoAutorizacion_Amarra_V = "VieneDeMalvinas_SolicitoAutorizacion_Amarra_V",

  // ── No va a Malvinas ────────────────────────────────────────────────
  NoVaAMalvinas_Exceptuado_MilitarOGC_D = "NoVaAMalvinas_Exceptuado_MilitarOGC_D",
  NoVaAMalvinas_Exceptuado_NoNavegacionMaritima_F = "NoVaAMalvinas_Exceptuado_NoNavegacionMaritima_F",
  NoVaAMalvinas_B = "NoVaAMalvinas_B",
  NoVaAMalvinas_Exceptuado_GiroInteriorPuerto_G = "NoVaAMalvinas_Exceptuado_GiroInteriorPuerto_G",
  NoVaAMalvinas_Exceptuado_NavegacionRadaRiaCostera_E = "NoVaAMalvinas_Exceptuado_NavegacionRadaRiaCostera_E",
  NoVaAMalvinas_Exceptuado_OtrosMotivos_X = "NoVaAMalvinas_Exceptuado_OtrosMotivos_X",
  NoVaAMalvinas_NoPresentoDeclaracion_N = "NoVaAMalvinas_NoPresentoDeclaracion_N",
  NoVaAMalvinas_PresentoDeclaracion_J = "NoVaAMalvinas_PresentoDeclaracion_J",
  NoVaAMalvinas_ReiniciaNavegacion_PresentoDeclaracion_K = "NoVaAMalvinas_ReiniciaNavegacion_PresentoDeclaracion_K",

  // ── Va a Malvinas ───────────────────────────────────────────────────
  VaAMalvinas_Exceptuado_MilitarOGC_Q = "VaAMalvinas_Exceptuado_MilitarOGC_Q",
  VaAMalvinas_AutorizadoCPER_A = "VaAMalvinas_AutorizadoCPER_A",
  VaAMalvinas_AutorizadoCPER_ReiniciaNavegacion_R = "VaAMalvinas_AutorizadoCPER_ReiniciaNavegacion_R",
  VaAMalvinas_NoAutorizadoCPER_Z = "VaAMalvinas_NoAutorizadoCPER_Z",
  VaAMalvinas_NoAutorizadoCPER_Fondeo_P = "VaAMalvinas_NoAutorizadoCPER_Fondeo_P",
}

/**
 * Etiquetas legibles para el usuario, usadas en el <select> del formulario.
 * Se exporta como `const` para poder iterar en el componente.
 */
export const DECLARACION_MALVINAS_LABELS: Record<DeclaracionMalvinasEnum, string> = {
  [DeclaracionMalvinasEnum.NoVieneDeMalvinas_L]: "(L) No viene de Malvinas",
  [DeclaracionMalvinasEnum.VieneDeMalvinas_AutorizadoCPER_M]: "(M) Viene de Malvinas — Autorizado por la CPER",
  [DeclaracionMalvinasEnum.VieneDeMalvinas_NoAutorizado_Infraccion_Extranjero_W]: "(W) Viene de Malvinas — Infracción / Va al extranjero",
  [DeclaracionMalvinasEnum.VieneDeMalvinas_NoSolicitoAutorizacion_Amarra_Y]: "(Y) Viene de Malvinas — No solicitó autorización (Amarra país)",
  [DeclaracionMalvinasEnum.VieneDeMalvinas_SolicitoAutorizacion_Amarra_V]: "(V) Viene de Malvinas — Solicitó autorización (Amarra país)",
  [DeclaracionMalvinasEnum.NoVaAMalvinas_Exceptuado_MilitarOGC_D]: "(D) No va a Malvinas — Exceptuado, Militar o GC",
  [DeclaracionMalvinasEnum.NoVaAMalvinas_Exceptuado_NoNavegacionMaritima_F]: "(F) No va a Malvinas — Exceptuado, sin navegación marítima",
  [DeclaracionMalvinasEnum.NoVaAMalvinas_B]: "(B) No va a Malvinas",
  [DeclaracionMalvinasEnum.NoVaAMalvinas_Exceptuado_GiroInteriorPuerto_G]: "(G) No va a Malvinas — Giro interior puerto",
  [DeclaracionMalvinasEnum.NoVaAMalvinas_Exceptuado_NavegacionRadaRiaCostera_E]: "(E) No va a Malvinas — Navegación Rada-Ría o Costera",
  [DeclaracionMalvinasEnum.NoVaAMalvinas_Exceptuado_OtrosMotivos_X]: "(X) No va a Malvinas — Exceptuado, otros motivos",
  [DeclaracionMalvinasEnum.NoVaAMalvinas_NoPresentoDeclaracion_N]: "(N) No va a Malvinas — No presentó Declaración Jurada",
  [DeclaracionMalvinasEnum.NoVaAMalvinas_PresentoDeclaracion_J]: "(J) No va a Malvinas — Presentó Declaración Jurada",
  [DeclaracionMalvinasEnum.NoVaAMalvinas_ReiniciaNavegacion_PresentoDeclaracion_K]: "(K) No va a Malvinas — Reinicia navegación, presentó DJ",
  [DeclaracionMalvinasEnum.VaAMalvinas_Exceptuado_MilitarOGC_Q]: "(Q) Va a Malvinas — Exceptuado, Militar o GC",
  [DeclaracionMalvinasEnum.VaAMalvinas_AutorizadoCPER_A]: "(A) Va a Malvinas — Autorizado por la CPER",
  [DeclaracionMalvinasEnum.VaAMalvinas_AutorizadoCPER_ReiniciaNavegacion_R]: "(R) Va a Malvinas — Autorizado CPER, Reinicia navegación",
  [DeclaracionMalvinasEnum.VaAMalvinas_NoAutorizadoCPER_Z]: "(Z) Va a Malvinas — No autorizado por la CPER",
  [DeclaracionMalvinasEnum.VaAMalvinas_NoAutorizadoCPER_Fondeo_P]: "(P) Va a Malvinas — No autorizado CPER, se ordenó fondeo",
};

// ─── Request DTO ──────────────────────────────────────────────────────────────

/**
 * Payload enviado al endpoint `POST /api/viajes`.
 *
 * NOTA IMPORTANTE — CosteraId:
 *   Este campo NO debe ser completado por el usuario ni enviado desde el
 *   formulario. El Controller de .NET lo sobreescribe con el Claim del JWT.
 *   Se incluye aquí solo para tipado de la respuesta interna del hook;
 *   el componente lo omite al construir el payload.
 *
 * Las fechas se serializan como ISO 8601 string. El servidor espera UTC.
 */
export interface NuevoViajeRequest {
  // Inyectado server-side; el frontend lo omite (el Controller lo sobreescribe)
  readonly costeraId?: string;

  // ── Campos requeridos ──────────────────────────────────────────────
  nombreBuque: string;
  origen: string;
  destino: string;
  proximoPuntoControl: string;
  /** ISO 8601 UTC — se construye desde el input datetime-local */
  fechaPartida: string;
  /** ISO 8601 UTC — debe ser posterior a fechaPartida */
  eta: string;
  declaracionMalvinas: DeclaracionMalvinasEnum;

  // ── Campos opcionales ──────────────────────────────────────────────
  muelleSalida?: string;
  agenciaMaritima?: string;
  motivoViaje?: string;
  zoe?: string;
  posicion?: string;
  /** Decimales positivos; km par del río o canal */
  rioCanalKmPar?: number;
}

// ─── Response DTO ─────────────────────────────────────────────────────────────

/**
 * Respuesta del endpoint `POST /api/viajes` ante un viaje creado exitosamente.
 * El backend devuelve un 201 Created con este body.
 */
export interface NuevoViajeResponse {
  /** ID del viaje recién creado en Oracle */
  viajeId: number;
  /** Mensaje de confirmación del sistema */
  mensaje: string;
}

// ─── Error DTO ────────────────────────────────────────────────────────────────

/**
 * Estructura del error deserializado desde la API cuando la respuesta
 * no es 2xx. Sigue el formato ProblemDetails de .NET.
 */
export interface NuevoViajeError {
  mensaje: string;
  detail?: string;
  status?: number;
}

// ─── Form Values ──────────────────────────────────────────────────────────────

/**
 * Tipo que describe los valores internos del formulario (react-hook-form).
 * Las fechas son strings para compatibilidad con input[type="datetime-local"].
 * La transformación a ISO 8601 se realiza en el submit handler.
 */
export type NuevoViajeFormValues = Omit<NuevoViajeRequest, "fechaPartida" | "eta" | "costeraId"> & {
  fechaPartida: string;
  eta: string;
};
