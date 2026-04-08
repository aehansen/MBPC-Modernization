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