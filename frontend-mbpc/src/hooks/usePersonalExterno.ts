// src/hooks/usePersonalExterno.ts
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
// @ts-expect-error - axiosClient is legacy JS
import axiosInstance from "@/axiosClient";
import type {
  PersonalViajeDto,
  EmbarcarPersonalDto,
  DesembarcarPersonalDto
} from "@/types/viajes.types";

const ENDPOINT_VIAJES = "/viajes";

/** Extrae mensaje legible de respuestas de error típicas de la API (.NET / ProblemDetails). */
function extractApiErrorMessage(error: unknown, fallback: string): string {
  const ax = error as { response?: { data?: unknown }; message?: string };
  const data = ax?.response?.data;
  if (typeof data === "string" && data.trim()) return data.trim();
  if (data && typeof data === "object") {
    const o = data as Record<string, unknown>;
    if (o.Error != null && String(o.Error).trim()) return String(o.Error);
    if (o.error != null && String(o.error).trim()) return String(o.error);
    if (o.mensaje != null && String(o.mensaje).trim()) return String(o.mensaje);
    if (o.message != null && String(o.message).trim()) return String(o.message);
    if (o.title != null && String(o.title).trim()) return String(o.title);
    if (o.detail != null && String(o.detail).trim()) return String(o.detail);
  }
  if (ax?.message && typeof ax.message === "string" && ax.message.trim()) {
    return ax.message;
  }
  return fallback;
}

export const getPersonalQueryKey = (viajeId: string) => ["viajes", viajeId, "personal"];

export function useObtenerPersonal(viajeId: string) {
  return useQuery<PersonalViajeDto>({
    queryKey: getPersonalQueryKey(viajeId),
    queryFn: async () => {
      if (!viajeId) throw new Error("ID de viaje requerido");
      const { data } = await axiosInstance.get(`${ENDPOINT_VIAJES}/${viajeId}/personal`);
      return data;
    },
    enabled: !!viajeId
  });
}

export function useEmbarcarPersonal() {
  const queryClient = useQueryClient();

  return useMutation<
    { Mensaje: string },
    Error,
    { viajeId: string; payload: EmbarcarPersonalDto }
  >({
    mutationFn: async ({ viajeId, payload }) => {
      try {
        const { data } = await axiosInstance.post(
          `${ENDPOINT_VIAJES}/${viajeId}/personal/embarcar`,
          payload
        );
        return data;
      } catch (error: unknown) {
        throw new Error(extractApiErrorMessage(error, "Error al embarcar personal"));
      }
    },
    onSuccess: (_, variables) => {
      queryClient.invalidateQueries({ queryKey: getPersonalQueryKey(variables.viajeId) });
    }
  });
}

export function useDesembarcarPersonal() {
  const queryClient = useQueryClient();

  return useMutation<
    { Mensaje: string },
    Error,
    { viajeId: string; dni: string; payload: DesembarcarPersonalDto }
  >({
    mutationFn: async ({ viajeId, dni, payload }) => {
      try {
        const { data } = await axiosInstance.put(
          `${ENDPOINT_VIAJES}/${viajeId}/personal/desembarcar/${dni}`,
          payload
        );
        return data;
      } catch (error: unknown) {
        throw new Error(extractApiErrorMessage(error, "Error al desembarcar personal"));
      }
    },
    onSuccess: (_, variables) => {
      queryClient.invalidateQueries({ queryKey: getPersonalQueryKey(variables.viajeId) });
    }
  });
}
