// src/hooks/useGestionConvoy.ts
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import axiosInstance from '@/axiosClient';
import { cargasKeys } from './useCargasApi';
import type {
  ConvoyDto,
  AmarrarBarcazaRequest,
  FondearBarcazaRequest,
} from '@/types/convoy.types';

// ---------------------------------------------------------------------------
// Tipos compartidos de Error Handling
// ---------------------------------------------------------------------------
export interface DotNetProblemDetails {
  detail?: string;
  title?: string;
  mensaje?: string;
  errors?: Record<string, string[]>;
}

// ---------------------------------------------------------------------------
// Query Keys
// ---------------------------------------------------------------------------
export const convoyKeys = {
  all: ['convoy'] as const,
  byViaje: (viajeId: string) => ['convoy', 'viaje', viajeId] as const,
};

// ---------------------------------------------------------------------------
// Hook de Consulta — GET /api/convoyes/viaje/{viajeId}
// ---------------------------------------------------------------------------
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
    staleTime: 30_000,
    retry: (failureCount, error) => {
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
  viajeId: string; // Incorporamos viajeId para la invalidación cruzada
  payload: AmarrarBarcazaRequest;
}

export function useAmarrarBarcaza() {
  const queryClient = useQueryClient();

  return useMutation<void, Error, AmarrarVariables>({
    mutationFn: async ({ barcazaId, payload }) => {
      await axiosInstance.put(
        `/convoyes/barcazas/${encodeURIComponent(barcazaId)}/amarrar`,
        payload,
      );
    },
    onSuccess: (_, variables) => {
      queryClient.invalidateQueries({ queryKey: convoyKeys.all });
      queryClient.invalidateQueries({ queryKey: cargasKeys.byViaje(variables.viajeId) }); // Invalidación cruzada
    },
  });
}

// ---------------------------------------------------------------------------
// Hook de Mutación — PUT /api/convoyes/barcazas/{barcazaId}/fondear
// ---------------------------------------------------------------------------
interface FondearVariables {
  barcazaId: string;
  viajeId: string; // Incorporamos viajeId para la invalidación cruzada
  payload: FondearBarcazaRequest;
}

export function useFondearBarcaza() {
  const queryClient = useQueryClient();

  return useMutation<void, Error, FondearVariables>({
    mutationFn: async ({ barcazaId, payload }) => {
      await axiosInstance.put(
        `/convoyes/barcazas/${encodeURIComponent(barcazaId)}/fondear`,
        payload,
      );
    },
    onSuccess: (_, variables) => {
      queryClient.invalidateQueries({ queryKey: convoyKeys.all });
      queryClient.invalidateQueries({ queryKey: cargasKeys.byViaje(variables.viajeId) }); // Invalidación cruzada
    },
  });
}

// ---------------------------------------------------------------------------
// Hook de Mutación — POST /api/convoyes/viaje/{viajeId}/adjuntar
// ---------------------------------------------------------------------------
export interface AdjuntarBarcazasRequest {
  barcazasIds: string[];
  ubicacion: string;
}

interface AdjuntarVariables {
  viajeId: string;
  payload: AdjuntarBarcazasRequest;
}

export function useAdjuntarBarcazas() {
  const queryClient = useQueryClient();

  return useMutation<void, Error, AdjuntarVariables>({
    mutationFn: async ({ viajeId, payload }) => {
      await axiosInstance.post(
        `/convoyes/viaje/${encodeURIComponent(viajeId)}/adjuntar`,
        payload,
      );
    },
    onSuccess: (_, variables) => {
      queryClient.invalidateQueries({ queryKey: convoyKeys.all });
      queryClient.invalidateQueries({ queryKey: cargasKeys.byViaje(variables.viajeId) }); // Invalidación cruzada
    },
  });
}

// ---------------------------------------------------------------------------
// Hook de Mutación — POST /api/convoyes/viaje/{viajeId}/separar
// ---------------------------------------------------------------------------
export interface SepararConvoyRequest {
  barcazasIds: string[];
  ubicacion: string;
}

interface SepararVariables {
  viajeId: string;
  payload: SepararConvoyRequest;
}

export function useSepararConvoy() {
  const queryClient = useQueryClient();

  return useMutation<void, Error, SepararVariables>({
    mutationFn: async ({ viajeId, payload }) => {
      await axiosInstance.post(
        `/convoyes/viaje/${encodeURIComponent(viajeId)}/separar`,
        payload,
      );
    },
    onSuccess: (_, variables) => {
      queryClient.invalidateQueries({ queryKey: convoyKeys.all });
      queryClient.invalidateQueries({ queryKey: cargasKeys.byViaje(variables.viajeId) }); // Invalidación cruzada
    },
  });
}