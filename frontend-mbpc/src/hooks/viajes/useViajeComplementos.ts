import {
  useMutation,
  useQuery,
  useQueryClient,
  type UseMutationResult,
  type UseQueryResult,
} from '@tanstack/react-query';

import apiClient from '../../axiosClient';
import type {
  ActualizarDatosPbipDto,
  AgregarNotaBitacoraDto,
  AsignarAgenciaDto,
  NotaBitacora,
  ViajeComplementos,
} from '../../types/complementos.types';

export const viajeComplementosKeys = {
  byViaje: (viajeId: string) => ['viaje-complementos', viajeId] as const,
};

async function fetchViajeComplementos(viajeId: string): Promise<ViajeComplementos> {
  const { data } = await apiClient.get<ViajeComplementos>(`/viajes/${viajeId}/complementos`);
  return data;
}

async function postNotaBitacora(params: {
  viajeId: string;
  payload: AgregarNotaBitacoraDto;
}): Promise<NotaBitacora> {
  const { data } = await apiClient.post<NotaBitacora>(
    `/viajes/${params.viajeId}/complementos/notas`,
    params.payload,
  );
  return data;
}

async function putAgencias(params: {
  viajeId: string;
  payload: AsignarAgenciaDto[];
}): Promise<void> {
  await apiClient.put(`/viajes/${params.viajeId}/complementos/agencias`, params.payload);
}

async function putPbip(params: {
  viajeId: string;
  payload: ActualizarDatosPbipDto;
}): Promise<void> {
  await apiClient.put(`/viajes/${params.viajeId}/complementos/pbip`, params.payload);
}

export function useViajeComplementos(viajeId: string): UseQueryResult<ViajeComplementos, Error> {
  return useQuery({
    queryKey: viajeComplementosKeys.byViaje(viajeId),
    queryFn: () => fetchViajeComplementos(viajeId),
    enabled: Boolean(viajeId),
  });
}

export function useAgregarNotaBitacora(
  viajeId: string,
): UseMutationResult<NotaBitacora, Error, AgregarNotaBitacoraDto> {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (payload: AgregarNotaBitacoraDto) => postNotaBitacora({ viajeId, payload }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['viaje-complementos', viajeId] });
    },
  });
}

export function useAsignarAgencias(
  viajeId: string,
): UseMutationResult<void, Error, AsignarAgenciaDto[]> {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (payload: AsignarAgenciaDto[]) => putAgencias({ viajeId, payload }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['viaje-complementos', viajeId] });
    },
  });
}

export function useActualizarDatosPbip(
  viajeId: string,
): UseMutationResult<void, Error, ActualizarDatosPbipDto> {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (payload: ActualizarDatosPbipDto) => putPbip({ viajeId, payload }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['viaje-complementos', viajeId] });
    },
  });
}
