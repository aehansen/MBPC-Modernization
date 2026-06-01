import {
  useMutation,
  useQuery,
  useQueryClient,
  type UseMutationResult,
  type UseQueryResult,
} from '@tanstack/react-query';

import viajesService, { type GetViajesParams } from '../services/viajes.service';
import type {
  AccionViajeResponse,
  BarcoPuertoDto,
  CrearViajeResponse,
  MapaViajeDto,
  NuevoViajeDto,
  ViajeDto,
  ViajeHistoricoDto,
  ViajePosicionMongo,
} from '../types/viajes.types';

export const viajesKeys = {
  all: ['viajes'] as const,
  list: (params: GetViajesParams) => ['viajes', 'list', params] as const,
  posicion: (mmsi: string) => ['viajes', 'posicion', mmsi] as const,
  puerto: () => ['viajes', 'puerto'] as const,
  historico: () => ['viajes', 'historico'] as const,
  mapa: () => ['viajes', 'mapa'] as const,
};

export function useViajes(
  pagina: number,
  tamanio: number,
): UseQueryResult<ViajeDto[], Error> {
  const params: GetViajesParams = { pagina, tamanio };
  return useQuery({
    queryKey: viajesKeys.list(params),
    queryFn: () => viajesService.getViajes(params),
    placeholderData: (prev) => prev,
  });
}

export function useViajePosicion(mmsi: string, enabled: boolean): UseQueryResult<ViajePosicionMongo, Error> {
  return useQuery({
    queryKey: viajesKeys.posicion(mmsi),
    queryFn: () => viajesService.getViajePosicion(mmsi),
    enabled,
  });
}

export function useZarparViaje(): UseMutationResult<AccionViajeResponse, Error, string> {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => viajesService.zarpar(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: viajesKeys.all });
    },
  });
}

export function useAmarrarViaje(): UseMutationResult<AccionViajeResponse, Error, string> {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => viajesService.amarrar(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: viajesKeys.all });
    },
  });
}

export function useFondearViaje(): UseMutationResult<AccionViajeResponse, Error, string> {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => viajesService.fondear(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: viajesKeys.all });
    },
  });
}

export function useReanudarViaje(): UseMutationResult<AccionViajeResponse, Error, string> {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => viajesService.reanudar(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: viajesKeys.all });
    },
  });
}


export function useCrearViaje() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (payload: NuevoViajeDto) => viajesService.crearViaje(payload),
    onSuccess: () => {
      // Invalida TODAS las queries de viajes para forzar refetch en el listado
      queryClient.invalidateQueries({ queryKey: viajesKeys.all });
    },
  });
}

export function useTransferirViaje(): UseMutationResult<
  void,
  Error,
  { viajeId: string; nuevaCosteraId: number }
> {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ viajeId, nuevaCosteraId }) =>
      viajesService.transferir(viajeId, nuevaCosteraId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: viajesKeys.all });
    },
  });
}