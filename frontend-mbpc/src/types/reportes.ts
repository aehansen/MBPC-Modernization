// src/types/reportes.ts

export interface FiltroDinamico {
  label: string;
  name: string;
  type: 'text' | 'date' | 'number';
  mask?: string;
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

export interface FiltroHistoricoDto {
  nombre?: string;
  omi?: string;
  matricula?: string;
  origen?: string;
  destino?: string;
  desde?: string; // Formato ISO para interactuar con C# DateTime?
  hasta?: string; // Formato ISO para interactuar con C# DateTime?
}

export interface ReportParamDto {
  name: string;
  value?: string;
}

export interface ReporteRequestDto {
  parametros: ReportParamDto[];
}

export interface NotaBitacoraDto {
  id: string;
  texto: string;
  usuario: string;
  fechaHora: string; // ISO 8601 DateTime
  categoria: string;
}

export interface AgenciaDto {
  rol: string;
  nombre: string;
  contacto: string;
}

export interface DatosPbipDto {
  contactoOcpm: string;
  nroInmarsat: string;
  arqueoBruto?: number;
  nivelProteccion: number;
}

export interface ViajeComplementosDto {
  viajeId: string;
  notasBitacora: NotaBitacoraDto[];
  agencias: AgenciaDto[];
  datosPbip?: DatosPbipDto;
}
