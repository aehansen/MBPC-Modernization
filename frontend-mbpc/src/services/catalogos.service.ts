import api from './apiClient';
import type { CatalogoDto } from '../types/catalogos.types';

const catalogosService = {
  getMuelles: async (): Promise<CatalogoDto[]> => {
    const { data } = await api.get<CatalogoDto[]>('/api/catalogos/muelles');
    return data;
  },

  getPuntosControl: async (): Promise<CatalogoDto[]> => {
    const { data } = await api.get<CatalogoDto[]>('/api/catalogos/puntos-control');
    return data;
  },
};

export default catalogosService;
