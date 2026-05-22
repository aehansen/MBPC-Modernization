import api from './apiClient';
import type {
  AccionViajeResponse,
  BarcoPuertoDto,
  CrearViajeResponse,
  MapaViajeDto,
  NuevoViajeDto,
  ViajeDto,
  ViajeHistoricoDto,
  ViajePosicionMongo,
} from '../types/viajes.types';

export interface GetViajesParams {
  pagina: number;
  tamanio: number;
}

export interface ActualizarPosicionDto {
  latitud: number;
  longitud: number;
  fechaReporte: string; // ISO 8601 string, e.g. "2024-07-01T14:30:00"
}

const viajesService = {
  getViajes: async (params: GetViajesParams): Promise<ViajeDto[]> => {
    const { data } = await api.get<ViajeDto[]>('/api/viajes', {
      params: {
        pagina: params.pagina,
        tamanio: params.tamanio,
      },
    });
    return data;
  },

  getViajePosicion: async (mmsi: string): Promise<ViajePosicionMongo> => {
    const { data } = await api.get<ViajePosicionMongo>(`/api/viajes/${mmsi}`);
    return data;
  },

  getMapaViajes: async (): Promise<MapaViajeDto[]> => {
    const { data } = await api.get<MapaViajeDto[]>('/api/viajes/mapa');
    return data;
  },

  crearViaje: async (payload: NuevoViajeDto): Promise<CrearViajeResponse> => {
    const { data } = await api.post<CrearViajeResponse>('/api/viajes', payload);
    return data;
  },

  zarpar: async (id: string): Promise<AccionViajeResponse> => {
    const { data } = await api.put<AccionViajeResponse>(`/api/viajes/${id}/zarpar`);
    return data;
  },

  amarrar: async (id: string): Promise<AccionViajeResponse> => {
    const { data } = await api.put<AccionViajeResponse>(`/api/viajes/${id}/amarrar`);
    return data;
  },

  fondear: async (id: string): Promise<AccionViajeResponse> => {
    const { data } = await api.put<AccionViajeResponse>(`/api/viajes/${id}/fondear`);
    return data;
  },

  reanudar: async (id: string): Promise<AccionViajeResponse> => {
    const { data } = await api.put<AccionViajeResponse>(`/api/viajes/${id}/reanudar`);
    return data;
  },

  actualizarPosicion: async (
    id: string,
    dto: ActualizarPosicionDto,
  ): Promise<void> => {
    await api.put(`/api/viajes/${id}/posicion`, dto);
  },
};

export default viajesService;
