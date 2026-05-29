export interface NotaBitacora {
  id: string;
  texto: string;
  usuario: string;
  fechaHora: string;
  categoria: string;
}

export interface AgregarNotaBitacoraDto {
  texto: string;
  categoria: string;
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
  viajeId: string;
  notasBitacora: NotaBitacora[];
  agencias: Agencia[];
  datosPbip: DatosPbip | null;
}
