/**
 * src/hooks/useActualizarPosicion.ts
 *
 * Hook de mutación para actualizar la posición geográfica de un buque.
 * Usa TanStack Query v5, axiosInstance centralizada y tipado estricto (cero `any`).
 */

import { useMutation, useQueryClient } from '@tanstack/react-query';
import axiosInstance from '@/axiosClient';
import { VIAJES_QUERY_KEY } from '@/hooks/useZarpar';

// ---------------------------------------------------------------------------
// Interfaces exportadas (contratos con el backend / .NET Web API)
// ---------------------------------------------------------------------------

/**
 * Payload que se envía al endpoint PUT /api/viajes/:id/posicion
 */
export interface ActualizarPosicionPayload {
  latitud: number;
  longitud: number;
  /** Fecha/hora del reporte en formato ISO 8601 UTC (e.g. "2024-11-03T14:30:00Z") */
  fechaReporte: string;
}

/**
 * Respuesta exitosa del backend tras actualizar la posición.
 * Incluye los datos de cinemática calculados por el servidor.
 */
export interface PosicionActualizadaResult {
  mensaje: string;
  vesselName: string;
  latitud: number;
  longitud: number;
  /** Velocidad calculada en nudos respecto al punto anterior del tracklog */
  velocidadCalculadaKn: number;
  /** Distancia recorrida en millas náuticas desde la posición anterior */
  distanciaRecorridaNM: number;
  tracklogId: string;
  fechaReporte: string;
}

/**
 * Estándar ProblemDetails de .NET (RFC 7807) para errores tipados.
 * Se usa con el type-guard `isAxiosError` para evitar casteos `any`.
 */
export interface ProblemDetails {
  type?: string;
  title: string;
  status?: number;
  detail?: string;
  instance?: string;
  /** Errores de validación adicionales (e.g. FluentValidation) */
  errors?: Record<string, string[]>;
}

// ---------------------------------------------------------------------------
// Hook
// ---------------------------------------------------------------------------

/**
 * Mutación para actualizar la posición de un buque en un viaje activo.
 *
 * @param viajeId - Identificador del viaje cuya posición se actualiza.
 *
 * @example
 * const mutation = useActualizarPosicion(viajeId);
 * mutation.mutate({ latitud: -34.59, longitud: -58.37, fechaReporte: new Date().toISOString() });
 */
export function useActualizarPosicion(viajeId: string) {
  const queryClient = useQueryClient();

  return useMutation<PosicionActualizadaResult, Error, ActualizarPosicionPayload>({
    mutationFn: async (payload: ActualizarPosicionPayload): Promise<PosicionActualizadaResult> => {
      const { data } = await axiosInstance.put<PosicionActualizadaResult>(
        `/viajes/${viajeId}/posicion`,
        payload,
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
