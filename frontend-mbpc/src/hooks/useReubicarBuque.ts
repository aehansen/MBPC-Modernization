import { useMutation, useQueryClient } from '@tanstack/react-query';
import axiosInstance from '@/axiosClient';
import { VIAJES_QUERY_KEY } from '@/hooks/useZarpar';
import { PosicionActualizadaResult } from './useActualizarPosicion';

export interface ReubicarBuquePayload {
  viajeId: string;
  latitud: number;
  longitud: number;
}

export function useReubicarBuque() {
  const queryClient = useQueryClient();

  return useMutation<PosicionActualizadaResult, Error, ReubicarBuquePayload>({
    mutationFn: async ({ viajeId, latitud, longitud }): Promise<PosicionActualizadaResult> => {
      const { data } = await axiosInstance.put<PosicionActualizadaResult>(
        `/viajes/${viajeId}/reubicar`,
        { latitud, longitud },
      );
      return data;
    },

    onSuccess: () => {
      /**
       * Invalida toda la caché relacionada con viajes para que el Dashboard
       * y el Mapa se actualicen automáticamente con la nueva posición.
       */
      queryClient.invalidateQueries({ queryKey: VIAJES_QUERY_KEY });
    },
  });
}
