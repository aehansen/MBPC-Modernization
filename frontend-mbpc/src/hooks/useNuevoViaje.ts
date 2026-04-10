// src/hooks/useNuevoViaje.ts
// ──────────────────────────────────────────────────────────────────────────────
// Custom hook que encapsula la mutación "Nuevo Viaje".
// Usa TanStack Query v5 e inyecta la petición mediante la instancia Axios.
// ──────────────────────────────────────────────────────────────────────────────

import { useMutation, useQueryClient } from "@tanstack/react-query";
// @ts-expect-error - axiosClient es un archivo .js legacy en un entorno TS
import axiosInstance from "@/axiosClient";
import type {
  NuevoViajeRequest,
  NuevoViajeResponse,
  NuevoViajeError,
} from "@/types/viajes.types";

// ─── Constantes ──────────────────────────────────────────────────────────────

/**
 * IMPORTANTE: Se quita el prefijo "/api" porque la instancia global en 
 * axiosClient.js ya tiene configurado baseURL: "/api".
 */
const VIAJES_ENDPOINT = "/viajes" as const;

/**
 * Query key raíz para la colección de viajes.
 */
export const VIAJES_QUERY_KEY = ["viajes"] as const;

// ─── Fetcher ─────────────────────────────────────────────────────────────────

async function nuevoViajeFetcher(
  request: NuevoViajeRequest
): Promise<NuevoViajeResponse> {
  // Garantizamos que costeraId no viaje en el payload por seguridad
  const { costeraId: _omitted, ...payload } = request;

  try {
    const response = await axiosInstance.post(
      VIAJES_ENDPOINT,
      payload
    );

    // 🕵️ EL LOG MAESTRO: Esto nos va a mostrar el objeto real en la Consola (pestaña Console)
    console.log("🔍 RESPUESTA CRUDA DEL BACKEND:", response.data);

    // Intentamos capturar el ID de cualquier forma posible
    const data = response.data;
    const finalId = data.viajeId ?? data.id ?? data.Id ?? data.VIAJEID ?? (typeof data === 'number' ? data : 0);

    return {
      viajeId: finalId,
      mensaje: data.mensaje || "Viaje registrado correctamente",
    };

  } catch (error: unknown) {
    console.error("DEBUG ERROR COMPLETO:", error);
    
    if (isAxiosErrorWithResponse(error)) {
      const body = error.response.data as Partial<NuevoViajeError>;
      const enriched: NuevoViajeError = {
        mensaje:
          body.mensaje ??
          (body as { title?: string }).title ??
          "Error al registrar el viaje.",
        detail: body.detail,
        status: error.response.status,
      };
      throw enriched;
    }

    // Error de red o caída del servidor
    const fallback: NuevoViajeError = {
      mensaje: (error as Error).message === "Network Error" 
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

export function useNuevoViaje() {
  const queryClient = useQueryClient();

  return useMutation<NuevoViajeResponse, NuevoViajeError, NuevoViajeRequest>({
    mutationFn: nuevoViajeFetcher,
    onSuccess: async () => {
      // Invalida la query de viajes para forzar el refresh de la grilla
      await queryClient.invalidateQueries({ queryKey: VIAJES_QUERY_KEY });
    },
  });
}