// src/hooks/useAccionesViaje.ts
// ──────────────────────────────────────────────────────────────────────────────
// Hooks para las acciones de transición de estado: Amarrar, Fondear, Reanudar.
// Replica exactamente el patrón arquitectónico de useZarpar.ts:
//   - Fetcher aislado con manejo ProblemDetails
//   - Type-guard interno para errores de Axios
//   - Invalidación de caché vía VIAJES_QUERY_KEY
// ──────────────────────────────────────────────────────────────────────────────

import { useMutation, useQueryClient } from "@tanstack/react-query";
// @ts-expect-error - axiosClient es un archivo .js legacy en un entorno TS
import axiosInstance from "@/axiosClient";
import type { NuevoViajeError } from "@/types/viajes.types";
import { VIAJES_QUERY_KEY } from "@/hooks/useZarpar";

// ─── Constante compartida ─────────────────────────────────────────────────────

/**
 * IMPORTANTE: Sin prefijo "/api" — axiosClient ya tiene baseURL: "/api".
 */
const VIAJES_ENDPOINT = "/viajes" as const;

// ─── Type guard interno (idéntico al de useZarpar) ────────────────────────────

function isAxiosErrorWithResponse(
  error: unknown
): error is { response: { data: unknown; status: number } } {
  return (
    typeof error === "object" &&
    error !== null &&
    "response" in error &&
    typeof (error as { response: unknown }).response === "object" &&
    (error as { response: unknown }).response !== null
  );
}

// ─── Helper genérico para construir NuevoViajeError ───────────────────────────

function buildNuevoViajeError(
  error: unknown,
  mensajeFallback: string
): NuevoViajeError {
  if (isAxiosErrorWithResponse(error)) {
    const body = error.response.data as Partial<NuevoViajeError> & { title?: string };
    return {
      mensaje: body.mensaje ?? body.title ?? mensajeFallback,
      detail: body.detail,
      status: error.response.status,
    };
  }

  const mensaje =
    (error as Error).message === "Network Error"
      ? "Error de Red: El servidor no respondió a tiempo o la conexión fue rechazada."
      : `Error inesperado: ${(error as Error).message}`;

  return { mensaje, status: undefined };
}

// ══════════════════════════════════════════════════════════════════════════════
// AMARRAR
// ══════════════════════════════════════════════════════════════════════════════

export interface AmarrarRequest {
  /** ID del viaje a amarrar */
  id: string;
}

export interface AmarrarResponse {
  /** Mensaje de confirmación del sistema */
  mensaje: string;
}

async function amarrarFetcher({ id }: AmarrarRequest): Promise<AmarrarResponse> {
  try {
    const response = await axiosInstance.put(`${VIAJES_ENDPOINT}/${id}/amarrar`);

    console.log("🔍 RESPUESTA CRUDA DEL BACKEND (amarrar):", response.data);

    const data = response.data as Partial<AmarrarResponse> & Record<string, unknown>;

    return {
      mensaje: (data.mensaje as string | undefined) ?? "Amarre registrado correctamente.",
    };
  } catch (error: unknown) {
    console.error("DEBUG ERROR COMPLETO (amarrar):", error);
    throw buildNuevoViajeError(error, "Error al registrar el amarre.");
  }
}

export function useAmarrar() {
  const queryClient = useQueryClient();

  return useMutation<AmarrarResponse, NuevoViajeError, AmarrarRequest>({
    mutationFn: amarrarFetcher,
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: VIAJES_QUERY_KEY });
    },
  });
}

// ══════════════════════════════════════════════════════════════════════════════
// FONDEAR
// ══════════════════════════════════════════════════════════════════════════════

export interface FondearRequest {
  /** ID del viaje a fondear */
  id: string;
}

export interface FondearResponse {
  /** Mensaje de confirmación del sistema */
  mensaje: string;
}

async function fondearFetcher({ id }: FondearRequest): Promise<FondearResponse> {
  try {
    const response = await axiosInstance.put(`${VIAJES_ENDPOINT}/${id}/fondear`);

    console.log("🔍 RESPUESTA CRUDA DEL BACKEND (fondear):", response.data);

    const data = response.data as Partial<FondearResponse> & Record<string, unknown>;

    return {
      mensaje: (data.mensaje as string | undefined) ?? "Fondeo registrado correctamente.",
    };
  } catch (error: unknown) {
    console.error("DEBUG ERROR COMPLETO (fondear):", error);
    throw buildNuevoViajeError(error, "Error al registrar el fondeo.");
  }
}

export function useFondear() {
  const queryClient = useQueryClient();

  return useMutation<FondearResponse, NuevoViajeError, FondearRequest>({
    mutationFn: fondearFetcher,
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: VIAJES_QUERY_KEY });
    },
  });
}

// ══════════════════════════════════════════════════════════════════════════════
// REANUDAR
// ══════════════════════════════════════════════════════════════════════════════

export interface ReanudarRequest {
  /** ID del viaje a reanudar */
  id: string;
}

export interface ReanudarResponse {
  /** Mensaje de confirmación del sistema */
  mensaje: string;
}

async function reanudarFetcher({ id }: ReanudarRequest): Promise<ReanudarResponse> {
  try {
    const response = await axiosInstance.put(`${VIAJES_ENDPOINT}/${id}/reanudar`);

    console.log("🔍 RESPUESTA CRUDA DEL BACKEND (reanudar):", response.data);

    const data = response.data as Partial<ReanudarResponse> & Record<string, unknown>;

    return {
      mensaje: (data.mensaje as string | undefined) ?? "Reanudación registrada correctamente.",
    };
  } catch (error: unknown) {
    console.error("DEBUG ERROR COMPLETO (reanudar):", error);
    throw buildNuevoViajeError(error, "Error al registrar la reanudación.");
  }
}

export function useReanudar() {
  const queryClient = useQueryClient();

  return useMutation<ReanudarResponse, NuevoViajeError, ReanudarRequest>({
    mutationFn: reanudarFetcher,
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: VIAJES_QUERY_KEY });
    },
  });
}
