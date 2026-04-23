// src/hooks/useAmarrarBarcaza.ts
// ──────────────────────────────────────────────────────────────────────────────
// Custom hook que encapsula la mutación "Amarrar Barcaza".
// Usa TanStack Query v5 e inyecta la petición mediante el apiClient centralizado
// para heredar los interceptores de seguridad (JWT) configurados en axiosClient.js.
// ──────────────────────────────────────────────────────────────────────────────

import { useMutation, useQueryClient } from "@tanstack/react-query";
import apiClient from '../axiosClient';
import type {
  AmarrarBarcazaRequest,
  AmarrarBarcazaResponse,
  AmarrarBarcazaError,
} from "../types/amarrarBarcaza.types";

// ─── Constantes ──────────────────────────────────────────────────────────────

/**
 * Query keys relacionadas con "viajes" que deben invalidarse tras amarrar
 * para reflejar el cambio de estado por consistencia eventual.
 */
const VIAJES_QUERY_KEY = ["viajes"] as const;

// ─── Función fetcher ──────────────────────────────────────────────────────────

/**
 * Realiza el PUT al endpoint de la API usando el apiClient centralizado.
 * Lanza un `AmarrarBarcazaError` enriquecido si la respuesta no es 2xx.
 */
async function amarrarBarcazaFetcher(
  request: AmarrarBarcazaRequest
): Promise<AmarrarBarcazaResponse> {
  try {
    const response = await apiClient.put<AmarrarBarcazaResponse>(
      `/carga/${encodeURIComponent(request.id)}/amarrar`,
      null,
      { params: { nuevoMuelle: request.nuevoMuelle } }
    );

    return response.data;
  } catch (error: any) {
    // Mapeamos el error de Axios a nuestro DTO de error
    const errorBody = error.response?.data;
    const errorData: AmarrarBarcazaError = {
      mensaje: errorBody?.mensaje ?? errorBody?.title ?? "Error al amarrar la barcaza.",
      detail: errorBody?.detail,
      status: error.response?.status,
    };
    throw errorData;
  }
}

// ─── Hook ─────────────────────────────────────────────────────────────────────

/**
 * Hook para disparar la mutación "Amarrar Barcaza".
 *
 * @example
 * const { mutate, isPending, isError, error } = useAmarrarBarcaza();
 * mutate({ id: "BRC-001", nuevoMuelle: "M-04" });
 */
export function useAmarrarBarcaza() {
  const queryClient = useQueryClient();

  return useMutation<
    AmarrarBarcazaResponse,
    AmarrarBarcazaError,
    AmarrarBarcazaRequest
  >({
    mutationFn: amarrarBarcazaFetcher,

    onSuccess: async () => {
      // Invalida todas las queries bajo la key "viajes" para refrescar
      // la grilla/mapa con el estado actualizado de la barcaza.
      await queryClient.invalidateQueries({ queryKey: VIAJES_QUERY_KEY });
    },

    // onError se maneja en el componente para mostrar el toast apropiado.
  });
}
