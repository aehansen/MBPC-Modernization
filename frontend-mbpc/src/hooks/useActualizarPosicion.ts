/**
 * src/hooks/useActualizarPosicion.ts
 * Hook de mutación para actualizar la posición de un buque usando TanStack Query.
 */
import { useMutation, useQueryClient } from '@tanstack/react-query';
import apiClient from '../services/apiClient'; // Ajustá el path si es necesario
import { viajesKeys } from './useViajes'; // Asumiendo que tenés tus keys acá

export interface ActualizarPosicionPayload {
  latitud: number;
  longitud: number;
  fechaReporte: string; // ISO 8601 UTC
}

export interface PosicionActualizadaResult {
  mensaje: string;
  vesselName: string;
  latitud: number;
  longitud: number;
  velocidadCalculadaKn: number;
  distanciaRecorridaNM: number;
  tracklogId: string;
  fechaReporte: string;
}

export function useActualizarPosicion(viajeId: string) {
  const queryClient = useQueryClient();

  return useMutation<PosicionActualizadaResult, Error, ActualizarPosicionPayload>({
    mutationFn: async (payload) => {
      const response = await apiClient.put(`/api/viajes/${viajeId}/posicion`, payload);
      return response.data;
    },
    onSuccess: () => {
      // Invalida la caché para que el Dashboard y el Mapa se recarguen solos
      queryClient.invalidateQueries({ queryKey: viajesKeys.all });
    },
  });
}