// src/hooks/useGestionConvoy.ts
//
// Hooks de TanStack Query v5 para el módulo de Gestión de Convoyes.
// Consume los endpoints de ConvoyController:
//   GET  /api/convoyes/viaje/{viajeId}
//   PUT  /api/convoyes/barcazas/{barcazaId}/amarrar
//   PUT  /api/convoyes/barcazas/{barcazaId}/fondear
//
// Capa de red: axiosInstance desde @/axiosClient (ya inyecta /api).
// Invalidación automática tras cada mutación exitosa.

import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import axiosInstance from '@/axiosClient';
import type {
  ConvoyDto,
  AmarrarBarcazaRequest,
  FondearBarcazaRequest,
} from '@/types/convoy.types';

// ---------------------------------------------------------------------------
// Tipos compartidos de Error Handling (copiados de ModalActualizarPosicion)
// ---------------------------------------------------------------------------

/**
 * Forma del cuerpo de error que devuelve ASP.NET Core (RFC 9457 / ProblemDetails).
 * Incluye el fallback `mensaje` y el diccionario `errors` de ValidationProblemDetails.
 */
export interface DotNetProblemDetails {
  detail?: string;
  title?: string;
  mensaje?: string;
  errors?: Record<string, string[]>;
}

// ---------------------------------------------------------------------------
// Query Keys
// ---------------------------------------------------------------------------

/**
 * Fábrica de query keys tipada.
 * Centraliza las claves para invalidaciones consistentes.
 */
export const convoyKeys = {
  all: ['convoy'] as const,
  byViaje: (viajeId: string) => ['convoy', 'viaje', viajeId] as const,
};

// ---------------------------------------------------------------------------
// Hook de Consulta — GET /api/convoyes/viaje/{viajeId}
// ---------------------------------------------------------------------------

/**
 * Obtiene la composición completa del convoy asociado a un viaje.
 *
 * @param viajeId - Identificador del viaje. Si es string vacío, la query queda deshabilitada.
 */
export function useObtenerConvoy(viajeId: string) {
  return useQuery<ConvoyDto, Error>({
    queryKey: convoyKeys.byViaje(viajeId),
    queryFn: async ({ signal }) => {
      const { data } = await axiosInstance.get<ConvoyDto>(
        `/convoyes/viaje/${encodeURIComponent(viajeId)}`,
        { signal },
      );
      return data;
    },
    enabled: viajeId.trim().length > 0,
    staleTime: 30_000, // 30 s — los datos de convoy cambian con poca frecuencia
    retry: (failureCount, error) => {
      // No reintentar en errores 4xx (el error es del cliente, no transitorio)
      if (
        'response' in error &&
        (error as { response?: { status?: number } }).response?.status !== undefined
      ) {
        const status = (error as { response: { status: number } }).response.status;
        if (status >= 400 && status < 500) return false;
      }
      return failureCount < 2;
    },
  });
}

// ---------------------------------------------------------------------------
// Hook de Mutación — PUT /api/convoyes/barcazas/{barcazaId}/amarrar
// ---------------------------------------------------------------------------

interface AmarrarVariables {
  barcazaId: string;
  payload: AmarrarBarcazaRequest;
}

/**
 * Amarra una barcaza al muelle indicado.
 * Invalida el cache de convoy en onSuccess para refrescar el panel automáticamente.
 */
export function useAmarrarBarcaza() {
  const queryClient = useQueryClient();

  return useMutation<void, Error, AmarrarVariables>({
    mutationFn: async ({ barcazaId, payload }) => {
      await axiosInstance.put(
        `/convoyes/barcazas/${encodeURIComponent(barcazaId)}/amarrar`,
        payload,
      );
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: convoyKeys.all });
    },
  });
}

// ---------------------------------------------------------------------------
// Hook de Mutación — PUT /api/convoyes/barcazas/{barcazaId}/fondear
// ---------------------------------------------------------------------------

interface FondearVariables {
  barcazaId: string;
  payload: FondearBarcazaRequest;
}

/**
 * Fondea una barcaza en la zona indicada.
 * Invalida el cache de convoy en onSuccess para refrescar el panel automáticamente.
 */
export function useFondearBarcaza() {
  const queryClient = useQueryClient();

  return useMutation<void, Error, FondearVariables>({
    mutationFn: async ({ barcazaId, payload }) => {
      await axiosInstance.put(
        `/convoyes/barcazas/${encodeURIComponent(barcazaId)}/fondear`,
        payload,
      );
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: convoyKeys.all });
    },
  });
}
