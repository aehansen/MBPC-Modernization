// src/hooks/useViajesApi.ts
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import apiClient, { viajesApi } from '../axiosClient';
import type { ViajeDto, ViajeHistoricoDto } from '../types/viajes.types';

// ─── DTO de filtros para búsqueda histórica ───────────────────────────────────

export interface FiltroHistoricoDto {
  nombre?: string;
  omi?: string;
  matricula?: string;
  origen?: string;
  destino?: string;
  desde?: string;   // ISO date string, e.g. "2024-01-01"
  hasta?: string;   // ISO date string, e.g. "2024-12-31"
}

// ─── Keys de Query ─────────────────────────────────────────────────────────────

export const viajesKeys = {
  all: ['viajes'] as const,
  paginated: (page: number, size: number, nombre: string) =>
    [...viajesKeys.all, 'paginated', page, size, nombre] as const,
  historico: (filtros: FiltroHistoricoDto) =>
    [...viajesKeys.all, 'historico', filtros] as const,
};

// ─── GET paginado ─────────────────────────────────────────────────────────────

async function fetchViajes(page: number, size: number, nombre: string): Promise<ViajeDto[]> {
  const { data } = await apiClient.get<ViajeDto[]>('/viajes', {
    params: { nombre: nombre || undefined, pagina: page, tamanio: size },
  });
  return data;
}

export function useViajes(page: number, size: number, nombre: string) {
  return useQuery<ViajeDto[], Error>({
    queryKey: viajesKeys.paginated(page, size, nombre),
    queryFn: () => fetchViajes(page, size, nombre),
  });
}

// ─── GET histórico con filtros múltiples opcionales ───────────────────────────

async function fetchViajesHistoricos(
  filtros: FiltroHistoricoDto
): Promise<ViajeHistoricoDto[]> {
  // Se eliminan las claves con valor vacío o undefined para no ensuciar la query string
  const params = Object.fromEntries(
    Object.entries(filtros).filter(([, v]) => v !== undefined && v !== '')
  );

  const { data } = await apiClient.get<ViajeHistoricoDto[]>('/viajes/historico', { params });
  return data;
}

export function useViajesHistoricos(filtros: FiltroHistoricoDto) {
  return useQuery<ViajeHistoricoDto[], Error>({
    queryKey: viajesKeys.historico(filtros),
    queryFn: () => fetchViajesHistoricos(filtros),
    enabled: false, // Solo se dispara manualmente con refetch()
  });
}

// ─── Mutaciones de estado ─────────────────────────────────────────────────────

async function zarparViaje(id: string): Promise<void> {
  await viajesApi.zarpar(id);
}

async function amarrarViaje(id: string): Promise<void> {
  await viajesApi.amarrar(id);
}

async function fondearViaje(id: string): Promise<void> {
  await viajesApi.fondear(id);
}

async function reanudarViaje(id: string): Promise<void> {
  await viajesApi.reanudar(id);
}

function useInvalidateViajes() {
  const qc = useQueryClient();
  return () => qc.invalidateQueries({ queryKey: viajesKeys.all });
}

export function useZarparViaje() {
  const invalidate = useInvalidateViajes();
  return useMutation<void, Error, string>({
    mutationFn: zarparViaje,
    onSuccess: invalidate,
  });
}

export function useAmarrarViaje() {
  const invalidate = useInvalidateViajes();
  return useMutation<void, Error, string>({
    mutationFn: amarrarViaje,
    onSuccess: invalidate,
  });
}

export function useFondearViaje() {
  const invalidate = useInvalidateViajes();
  return useMutation<void, Error, string>({
    mutationFn: fondearViaje,
    onSuccess: invalidate,
  });
}

export function useReanudarViaje() {
  const invalidate = useInvalidateViajes();
  return useMutation<void, Error, string>({
    mutationFn: reanudarViaje,
    onSuccess: invalidate,
  });
}
