// src/hooks/useZarpar.ts
// ──────────────────────────────────────────────────────────────────────────────
// Custom hook que encapsula la mutación "Zarpar".
// Usa TanStack Query v5 e inyecta la petición mediante la instancia Axios.
// Replica el patrón de manejo de errores ProblemDetails de useNuevoViaje.ts.
// ──────────────────────────────────────────────────────────────────────────────

import { useMutation, useQueryClient } from "@tanstack/react-query";
// @ts-expect-error - axiosClient es un archivo .js legacy en un entorno TS
import axiosInstance from "@/axiosClient";
import type { NuevoViajeError } from "@/types/viajes.types";

// ─── Constantes ──────────────────────────────────────────────────────────────

/**
 * IMPORTANTE: Se quita el prefijo "/api" porque la instancia global en
 * axiosClient.js ya tiene configurado baseURL: "/api".
 */
const VIAJES_ENDPOINT = "/viajes" as const;

/**
 * Query key raíz para la colección de viajes.
 * Re-exportada desde este módulo para evitar imports cruzados con useNuevoViaje.
 */
export const VIAJES_QUERY_KEY = ["viajes"] as const;

// ─── DTOs locales ─────────────────────────────────────────────────────────────

export interface ZarparRequest {
  /** ID del viaje a zarpar */
  id: string;
}

export interface ZarparResponse {
  /** Mensaje de confirmación del sistema */
  mensaje: string;
}

// ─── Fetcher ─────────────────────────────────────────────────────────────────

async function zarparFetcher({ id }: ZarparRequest): Promise<ZarparResponse> {
  try {
    const response = await axiosInstance.put(`${VIAJES_ENDPOINT}/${id}/zarpar`);

    console.log("🔍 RESPUESTA CRUDA DEL BACKEND (zarpar):", response.data);

    const data = response.data as Partial<ZarparResponse> & Record<string, unknown>;

    return {
      mensaje:
        (data.mensaje as string | undefined) ?? "Zarpe registrado correctamente.",
    };
  } catch (error: unknown) {
    console.error("DEBUG ERROR COMPLETO (zarpar):", error);

    if (isAxiosErrorWithResponse(error)) {
      const body = error.response.data as Partial<NuevoViajeError>;
      const enriched: NuevoViajeError = {
        mensaje:
          body.mensaje ??
          (body as { title?: string }).title ??
          "Error al registrar el zarpe.",
        detail: body.detail,
        status: error.response.status,
      };
      throw enriched;
    }

    // Error de red o caída del servidor
    const fallback: NuevoViajeError = {
      mensaje:
        (error as Error).message === "Network Error"
          ? "Error de Red: El servidor no respondió a tiempo o la conexión fue rechazada."
          : "Error inesperado: " + (error as Error).message,
      status: undefined,
    };
    throw fallback;
  }
}

// ─── Type guard interno ───────────────────────────────────────────────────────

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

// ─── Hook ─────────────────────────────────────────────────────────────────────

export function useZarpar() {
  const queryClient = useQueryClient();

  return useMutation<ZarparResponse, NuevoViajeError, ZarparRequest>({
    mutationFn: zarparFetcher,
    onSuccess: async () => {
      // Invalida la query de viajes para forzar el refresh de la grilla
      await queryClient.invalidateQueries({ queryKey: VIAJES_QUERY_KEY });
    },
  });
}
