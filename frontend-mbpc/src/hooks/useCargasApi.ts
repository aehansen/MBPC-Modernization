// src/hooks/useCargasApi.ts
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import apiClient, { cargaApi } from '../axiosClient';
import type {
  CargaDto,
  NuevaCargaRequest,
  AmarrarCargaParams,
  FondearCargaParams,
  CargarCargaParams,
  DescargarCargaParams,
} from '../types/cargas.types';

// ─── Keys ─────────────────────────────────────────────────────────────────────

export const cargasKeys = {
  all: ['cargas'] as const,
  byViaje: (viajeId: string) => [...cargasKeys.all, 'viaje', viajeId] as const,
};

// ─── GET cargas de un viaje ───────────────────────────────────────────────────

async function fetchCargas(viajeId: string): Promise<CargaDto[]> {
  const { data } = await cargaApi.getByViaje(viajeId);
  return data;
}

export function useCargas(viajeId: string) {
  return useQuery<CargaDto[], Error>({
    queryKey: cargasKeys.byViaje(viajeId),
    queryFn: () => fetchCargas(viajeId),
    enabled: Boolean(viajeId),
  });
}

// ─── POST nueva carga ─────────────────────────────────────────────────────────

interface NuevaCargaArgs {
  nombreBuque: string;
  body: NuevaCargaRequest;
}

async function crearCarga({ nombreBuque, body }: NuevaCargaArgs): Promise<CargaDto> {
  const { data } = await apiClient.post<CargaDto>(
    `/carga/viaje/${nombreBuque}`,
    body
  );
  return data;
}

export function useCrearCarga(viajeId: string) {
  const qc = useQueryClient();
  return useMutation<CargaDto, Error, NuevaCargaArgs>({
    mutationFn: crearCarga,
    onSuccess: () => qc.invalidateQueries({ queryKey: cargasKeys.byViaje(viajeId) }),
  });
}

// ─── PUT amarrar carga ────────────────────────────────────────────────────────

async function amarrarCarga({ id, nuevoMuelle }: AmarrarCargaParams): Promise<CargaDto> {
  const { data } = await apiClient.put<CargaDto>(
    `/carga/${id}/amarrar`,
    null,
    { params: { nuevoMuelle } }
  );
  return data;
}

export function useAmarrarCarga(viajeId: string) {
  const qc = useQueryClient();
  return useMutation<CargaDto, Error, AmarrarCargaParams>({
    mutationFn: amarrarCarga,
    onSuccess: () => qc.invalidateQueries({ queryKey: cargasKeys.byViaje(viajeId) }),
  });
}

// ─── PUT fondear carga ────────────────────────────────────────────────────────

async function fondearCarga({ id, zonaFondeo }: FondearCargaParams): Promise<CargaDto> {
  const { data } = await apiClient.put<CargaDto>(
    `/carga/${id}/fondear`,
    null,
    { params: { zonaFondeo } }
  );
  return data;
}

export function useFondearCarga(viajeId: string) {
  const qc = useQueryClient();
  return useMutation<CargaDto, Error, FondearCargaParams>({
    mutationFn: fondearCarga,
    onSuccess: () => qc.invalidateQueries({ queryKey: cargasKeys.byViaje(viajeId) }),
  });
}

// ─── PUT cargar toneladas ─────────────────────────────────────────────────────

async function cargarToneladas({ id, toneladas }: CargarCargaParams): Promise<CargaDto> {
  const { data } = await apiClient.put<CargaDto>(
    `/carga/${id}/cargar`,
    null,
    { params: { toneladas } }
  );
  return data;
}

export function useCargarToneladas(viajeId: string) {
  const qc = useQueryClient();
  return useMutation<CargaDto, Error, CargarCargaParams>({
    mutationFn: cargarToneladas,
    onSuccess: () => qc.invalidateQueries({ queryKey: cargasKeys.byViaje(viajeId) }),
  });
}

// ─── PUT descargar toneladas ──────────────────────────────────────────────────

async function descargarToneladas({ id, toneladas }: DescargarCargaParams): Promise<CargaDto> {
  const { data } = await apiClient.put<CargaDto>(
    `/carga/${id}/descargar`,
    null,
    { params: { toneladas } }
  );
  return data;
}

export function useDescargarToneladas(viajeId: string) {
  const qc = useQueryClient();
  return useMutation<CargaDto, Error, DescargarCargaParams>({
    mutationFn: descargarToneladas,
    onSuccess: () => qc.invalidateQueries({ queryKey: cargasKeys.byViaje(viajeId) }),
  });
}
