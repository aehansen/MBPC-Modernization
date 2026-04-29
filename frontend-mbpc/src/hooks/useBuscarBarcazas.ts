import { useQuery } from '@tanstack/react-query';
import axiosInstance from '@/axiosClient'; // Ajustá el path si lo necesitás, ej: '../services/apiClient' o el que uses

export interface AutocompleteBarcaza {
  idBuque: number;
  nombre: string;
  matricula: string;
  omi: string;
  tipo: string;
}

export const useBuscarBarcazas = (etapaId: string, searchTerm: string) => {
  return useQuery<AutocompleteBarcaza[], Error>({
    queryKey: ['barcazas', 'autocomplete', etapaId, searchTerm],
    queryFn: async () => {
      // Corrección: Le sacamos el /api inicial porque axiosInstance ya lo inyecta
      const { data } = await axiosInstance.get<AutocompleteBarcaza[]>(
        '/buques/barcazas/autocomplete',
        {
          params: {
            etapaId,
            query: searchTerm,
          },
        }
      );
      return data;
    },
    // Solo ejecutamos la query si el término de búsqueda tiene al menos 2 caracteres
    enabled: searchTerm.trim().length >= 2,
    staleTime: 1000 * 60 * 5, // Cacheamos por 5 minutos
  });
};