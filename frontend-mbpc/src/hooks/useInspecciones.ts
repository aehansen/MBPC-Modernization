import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import apiClient from '../axiosClient';
import type { InspeccionDto, CrearInspeccionDto, ModificarInspeccionDto } from '../types/inspecciones.types';

export const inspeccionesKeys = {
  all: ['inspecciones'] as const,
  byViaje: (viajeId?: string) => [...inspeccionesKeys.all, 'viaje', viajeId] as const,
  byId: (id: string) => [...inspeccionesKeys.all, 'id', id] as const,
};

// ─── GET inspecciones ──────────────────────────────────────────────────────────
async function fetchInspecciones(viajeId?: string, pagina = 1, tamanio = 50): Promise<InspeccionDto[]> {
  const { data } = await apiClient.get<InspeccionDto[]>('/inspecciones', {
    params: { viajeId, pagina, tamanio },
  });
  return data;
}

export function useInspecciones(viajeId?: string, pagina = 1, tamanio = 50) {
  return useQuery<InspeccionDto[], Error>({
    queryKey: inspeccionesKeys.byViaje(viajeId),
    queryFn: () => fetchInspecciones(viajeId, pagina, tamanio),
  });
}

// ─── GET inspeccion por ID ──────────────────────────────────────────────────────
async function fetchInspeccionById(id: string): Promise<InspeccionDto> {
  const { data } = await apiClient.get<InspeccionDto>(`/inspecciones/${id}`);
  return data;
}

export function useInspeccionById(id: string, enabled = false) {
  return useQuery<InspeccionDto, Error>({
    queryKey: inspeccionesKeys.byId(id),
    queryFn: () => fetchInspeccionById(id),
    enabled: enabled && Boolean(id),
  });
}

// ─── POST crear inspeccion ──────────────────────────────────────────────────────
async function crearInspeccion(body: CrearInspeccionDto): Promise<{ mensaje: string }> {
  const { data } = await apiClient.post<{ mensaje: string }>('/inspecciones', body);
  return data;
}

export function useCrearInspeccion(viajeId?: string) {
  const qc = useQueryClient();
  return useMutation<{ mensaje: string }, Error, CrearInspeccionDto>({
    mutationFn: crearInspeccion,
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: inspeccionesKeys.all });
      if (viajeId) {
        qc.invalidateQueries({ queryKey: inspeccionesKeys.byViaje(viajeId) });
      }
    },
  });
}

// ─── PUT modificar inspeccion ────────────────────────────────────────────────────
interface ModificarInspeccionArgs {
  id: string;
  body: ModificarInspeccionDto;
}

async function modificarInspeccion({ id, body }: ModificarInspeccionArgs): Promise<{ mensaje: string }> {
  const { data } = await apiClient.put<{ mensaje: string }>(`/inspecciones/${id}`, body);
  return data;
}

export function useModificarInspeccion(viajeId?: string) {
  const qc = useQueryClient();
  return useMutation<{ mensaje: string }, Error, ModificarInspeccionArgs>({
    mutationFn: modificarInspeccion,
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: inspeccionesKeys.all });
      if (viajeId) {
        qc.invalidateQueries({ queryKey: inspeccionesKeys.byViaje(viajeId) });
      }
    },
  });
}

// ─── DELETE eliminar inspeccion ──────────────────────────────────────────────────
interface EliminarInspeccionArgs {
  id: string;
  viajeId: string;
}

async function eliminarInspeccion({ id, viajeId }: EliminarInspeccionArgs): Promise<{ mensaje: string }> {
  const { data } = await apiClient.delete<{ mensaje: string }>(`/inspecciones/${id}`, {
    params: { viajeId },
  });
  return data;
}

export function useEliminarInspeccion(viajeId?: string) {
  const qc = useQueryClient();
  return useMutation<{ mensaje: string }, Error, EliminarInspeccionArgs>({
    mutationFn: eliminarInspeccion,
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: inspeccionesKeys.all });
      if (viajeId) {
        qc.invalidateQueries({ queryKey: inspeccionesKeys.byViaje(viajeId) });
      }
    },
  });
}
