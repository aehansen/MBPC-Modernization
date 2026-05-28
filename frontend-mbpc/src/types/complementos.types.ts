export interface NotaBitacora {
  id: string;
  texto: string;
  usuario: string;
  fecha: string;
}

export interface AgregarNotaBitacoraDto {
  texto: string;
}

export interface Agencia {
  rol: string;
  nombre: string;
  contacto: string;
}

export interface AsignarAgenciaDto {
  rol: string;
  nombre: string;
  contacto: string;
}

export interface DatosPbip {
  contactoOcpm: string;
  nroInmarsat: string;
  arqueoBruto: number;
  nivelProteccion: number;
}

export interface ActualizarDatosPbipDto {
  contactoOcpm: string;
  nroInmarsat: string;
  arqueoBruto: number;
  nivelProteccion: number;
}

export interface ViajeComplementos {
  id: string;
  idViaje: number;
  vesselName?: string | null;
  agencias: Agencia[];
  datosPbip?: DatosPbip | null;
  notasBitacora: NotaBitacora[];
}
