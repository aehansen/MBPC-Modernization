import { useCallback, useState } from "react";
// @ts-expect-error - axiosClient es módulo JS legacy
import axiosInstance from "@/axiosClient";

/** Tipo de embarcación soportado por el autocompletado (rutas distintas bajo `/api/buques/...`). */
export type EmbarcacionAutocompleteTipo = "buque" | "barcaza" | "remolcador";

/** Forma alineada con `BuqueAutocompleteDto` del backend (serialización camelCase típica de ASP.NET). */
export interface BuqueAutocompleteItem {
  idBuque: number;
  nombre: string;
  matricula: string;
  omi: string;
  tipo: string;
}

function pathForTipo(tipo: EmbarcacionAutocompleteTipo): string {
  switch (tipo) {
    case "buque":
      return "/buques/autocomplete";
    case "barcaza":
      return "/buques/autocomplete/barcazas";
    case "remolcador":
      return "/buques/autocomplete/remolcadores";
    default: {
      const _exhaustive: never = tipo;
      return _exhaustive;
    }
  }
}

/**
 * Hook genérico para autocompletado de buques, barcazas o remolcadores contra la API MBPC.
 * Debounce recomendado en el componente consumidor antes de llamar a `fetchSuggestions`.
 */
export function useEmbarcacionesAutocomplete(tipo: EmbarcacionAutocompleteTipo) {
  const [items, setItems] = useState<BuqueAutocompleteItem[]>([]);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const fetchSuggestions = useCallback(
    async (query: string) => {
      const trimmed = query.trim();
      if (trimmed.length < 2) {
        setItems([]);
        setError(null);
        return;
      }

      setIsLoading(true);
      setError(null);

      try {
        const path = pathForTipo(tipo);
        const { data } = await axiosInstance.get<BuqueAutocompleteItem[]>(
          `${path}?query=${encodeURIComponent(trimmed)}`
        );
        setItems(Array.isArray(data) ? data : []);
      } catch (e: unknown) {
        const ax = e as { response?: { data?: { Error?: string; mensaje?: string } }; message?: string };
        const apiMsg = ax?.response?.data?.Error ?? ax?.response?.data?.mensaje;
        setError(apiMsg ?? ax?.message ?? "Error al consultar el autocompletado.");
        setItems([]);
      } finally {
        setIsLoading(false);
      }
    },
    [tipo]
  );

  const clear = useCallback(() => {
    setItems([]);
    setError(null);
  }, []);

  return { items, isLoading, error, fetchSuggestions, clear };
}
