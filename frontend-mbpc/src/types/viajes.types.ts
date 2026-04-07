// ─── Generic ────────────────────────────────────────────────────────────────

export interface PaginatedResponse<T> {
  data: T[];
  totalCount: number;
  page: number;
}

// ─── Enums / Literal Types ───────────────────────────────────────────────────

export type DeclaracionMalvinas =
  | 'NoVieneDeMalvinas_L'
  | 'VieneDeMalvinas_AutorizadoCPER_M'
  | 'VieneDeMalvinas_NoAutorizado_Infraccion_Extranjero_W'
  | 'VieneDeMalvinas_NoSolicitoAutorizacion_Amarra_Y'
  | 'VieneDeMalvinas_SolicitoAutorizacion_Amarra_V'
  | 'NoVaAMalvinas_Exceptuado_MilitarOGC_D'
  | 'NoVaAMalvinas_Exceptuado_NoNavegacionMaritima_F'
  | 'NoVaAMalvinas_B'
  | 'NoVaAMalvinas_Exceptuado_GiroInteriorPuerto_G'
  | 'NoVaAMalvinas_Exceptuado_NavegacionRadaRiaCostera_E'
  | 'NoVaAMalvinas_Exceptuado_OtrosMotivos_X'
  | 'NoVaAMalvinas_NoPresentoDeclaracion_N'
  | 'NoVaAMalvinas_PresentoDeclaracion_J'
  | 'NoVaAMalvinas_ReiniciaNavegacion_PresentoDeclaracion_K'
  | 'VaAMalvinas_Exceptuado_MilitarOGC_Q'
  | 'VaAMalvinas_AutorizadoCPER_A'
  | 'VaAMalvinas_AutorizadoCPER_ReiniciaNavegacion_R'
  | 'VaAMalvinas_NoAutorizadoCPER_Z'
  | 'VaAMalvinas_NoAutorizadoCPER_Fondeo_P';

// ─── Response DTOs ───────────────────────────────────────────────────────────

export interface EtapaDto {
  puntoControl: string | null;
  hrp: string | null;
  eta: string | null;
  estado: string;
  esActiva: boolean;
}

export interface BarcazaDto {
  nombre: string;
  bandera: string;
  carga: string;
  unidad: string;
  matricula: string | null;
  muelleActual: string | null;
  cantidad: number;
}

export interface RemolcadorDto {
  nombre: string;
  estado: string;
  fechaSalida: string | null;
}

export interface PracticoDto {
  nombre: string;
  fechaEmbarque: string | null;
  fechaDesembarque: string | null;
  zona: string | null;
}

export interface InspectorDto {
  nombre: string;
  organismo: string;
}

export interface ViajeDto {
  id: string;
  buque: string;
  ruta: string;
  fechaInicioFormateada: string;
  estadoActual: string;
  costeraId: string | null;
  barcazas: BarcazaDto[];
  remolcador: RemolcadorDto | null;
  etapas: EtapaDto[];
  practicos: PracticoDto[];
  inspectores: InspectorDto[];
}

export interface GeoMongo {
  type: string;
  coordinates: number[];
}

export interface LocationMongo {
  geo: GeoMongo;
}

export interface ViajePosicionMongo {
  id: string;
  travelId: number;
  vesselName: string;
  mmsi: string | null;
  callSign: string | null;
  origin: string | null;
  destination: string | null;
  imo: number | null;
  costeraId: number | null;
  latitude: number;
  longitude: number;
  speedOverGround: number;
  courseOverGround: number;
  navegationStatusDesc: string;
  msgTime: string;
  location: LocationMongo | null;
}

export interface BarcoPuertoDto {
  id: string;
  buque: string;
  origen: string;
  destino: string;
  eta: string;
  estado: string;
  mmsi: string;
}

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

export interface MapaViajeDto {
  id: string;
  nombreBuque: string;
  estadoNav: string;
  ultimaActualizacion: string;
  mmsi: string | null;
  indicativo: string | null;
  origen: string | null;
  destino: string | null;
  remolcador: string | null;
  imo: number | null;
  latitud: number;
  longitud: number;
  velocidad: number;
  rumbo: number;
  tieneDetalleOperativo: boolean;
  cantidadBarcazas: number;
}

// ─── Request DTOs ────────────────────────────────────────────────────────────

export interface NuevoViajeDto {
  nombreBuque: string;
  origen: string;
  destino: string;
  proximoPuntoControl: string;
  muelleSalida: string | null;
  agenciaMaritima: string | null;
  motivoViaje: string | null;
  zoe: string | null;
  posicion: string | null;
  fechaPartida: string;
  eta: string;
  rioCanalKmPar: number | null;
  declaracionMalvinas: DeclaracionMalvinas;
}

export interface CrearViajeResponse {
  mensaje: string;
  buque: string;
  origen: string;
  destino: string;
  estadoInicial: string;
}

export interface AccionViajeResponse {
  mensaje: string;
}