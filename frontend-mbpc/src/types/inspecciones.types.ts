export interface InspeccionDto {
  id: string;
  viajeId: string;
  buqueId: number;
  fechaInspeccion: string;
  tipoInspeccion: string;
  resultado: string;
  observaciones: string;
  inspectorDatos: string;
  lugarInspeccion: string;
  costeraId: number;
}

export interface CrearInspeccionDto {
  viajeId: string;
  buqueId?: number;
  fechaInspeccion: string;
  tipoInspeccion: string;
  resultado: string;
  observaciones: string;
  inspectorDatos: string;
  lugarInspeccion: string;
}

export interface ModificarInspeccionDto {
  viajeId: string;
  buqueId?: number;
  fechaInspeccion: string;
  tipoInspeccion: string;
  resultado: string;
  observaciones: string;
  inspectorDatos: string;
  lugarInspeccion: string;
}
