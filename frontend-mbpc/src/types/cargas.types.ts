// ─── Enums ────────────────────────────────────────────────────────────────────

export type TipoCarga = 'Barcaza' | 'Bodega';

// ─── DTOs ─────────────────────────────────────────────────────────────────────

export interface CargaDto {
  id: string;
  viajeId: string;
  descripcionLista: string;
  nivelRiesgo: string;
  muelleActual: string | null;
  tonelaje: number;
}

// ─── Requests ─────────────────────────────────────────────────────────────────

export interface NuevaCargaRequest {
  nombre: string;
  tipo: TipoCarga;
  tonelaje: number;
}

export interface AmarrarCargaParams {
  id: string;
  nuevoMuelle: string;
}

export interface FondearCargaParams {
  id: string;
  zonaFondeo: string;
}

export interface CargarCargaParams {
  id: string;
  toneladas: number;
}

export interface DescargarCargaParams {
  id: string;
  toneladas: number;
}