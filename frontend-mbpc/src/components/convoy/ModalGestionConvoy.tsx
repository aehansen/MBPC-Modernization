// src/components/convoy/ModalGestionConvoy.tsx
// Hito 10.4 — Ventana contextual de gestión de convoy por viajeId

import { useCallback, useEffect, useState } from "react";
import { isAxiosError } from "axios";
import axiosInstance from "@/axiosClient";
import PanelGestionConvoy from "@/components/convoy/PanelGestionConvoy";
import type { ConvoyDto } from "@/types/convoy.types";

export interface ModalGestionConvoyProps {
  isOpen: boolean;
  onClose: () => void;
  viajeId: string;
}

function extractFetchErrorMessage(error: unknown): string {
  if (isAxiosError(error)) {
    const data = error.response?.data;
    if (typeof data === "string" && data.trim()) return data.trim();
    if (data && typeof data === "object") {
      const o = data as Record<string, unknown>;
      if (o.detail != null && String(o.detail).trim()) return String(o.detail);
      if (o.mensaje != null && String(o.mensaje).trim()) return String(o.mensaje);
      if (o.title != null && String(o.title).trim()) return String(o.title);
    }
    if (error.response?.status === 404) {
      return "No existe un convoy asociado a este viaje.";
    }
    return error.message;
  }
  if (error instanceof Error) return error.message;
  return "No se pudo cargar el convoy.";
}

export default function ModalGestionConvoy({
  isOpen,
  onClose,
  viajeId,
}: ModalGestionConvoyProps) {
  const [convoy, setConvoy] = useState<ConvoyDto | null>(null);
  const [isLoading, setIsLoading] = useState(false);
  const [fetchError, setFetchError] = useState<string | null>(null);

  const cargarConvoy = useCallback(
    async (signal?: AbortSignal) => {
      if (!viajeId.trim()) return;

      setIsLoading(true);
      setFetchError(null);

      try {
        const { data } = await axiosInstance.get<ConvoyDto>(
          `/convoyes/viaje/${encodeURIComponent(viajeId)}`,
          { signal },
        );
        setConvoy(data);
      } catch (err: unknown) {
        if (signal?.aborted) return;
        setConvoy(null);
        setFetchError(extractFetchErrorMessage(err));
      } finally {
        if (!signal?.aborted) {
          setIsLoading(false);
        }
      }
    },
    [viajeId],
  );

  const handleRefresh = useCallback(() => {
    void cargarConvoy();
  }, [cargarConvoy]);

  useEffect(() => {
    if (!isOpen || !viajeId.trim()) {
      setConvoy(null);
      setFetchError(null);
      setIsLoading(false);
      return;
    }

    const controller = new AbortController();
    void cargarConvoy(controller.signal);

    return () => controller.abort();
  }, [isOpen, viajeId, cargarConvoy]);

  useEffect(() => {
    if (!isOpen) return;
    const handleKeyDown = (e: KeyboardEvent) => {
      if (e.key === "Escape" && !isLoading) onClose();
    };
    document.addEventListener("keydown", handleKeyDown);
    return () => document.removeEventListener("keydown", handleKeyDown);
  }, [isOpen, isLoading, onClose]);

  if (!isOpen) return null;

  return (
    <div
      role="dialog"
      aria-modal="true"
      aria-labelledby="modal-gestion-convoy-title"
      className="fixed inset-0 z-50 flex items-center justify-center p-4"
    >
      <div
        className="absolute inset-0 bg-slate-950/80 backdrop-blur-sm"
        onClick={!isLoading ? onClose : undefined}
        aria-hidden="true"
      />

      <div className="relative z-10 w-full max-w-5xl max-h-[92vh] flex flex-col bg-slate-900 border border-cyan-500/30 rounded-lg shadow-2xl shadow-black/60">
        <div className="h-1 w-full bg-gradient-to-r from-cyan-500 via-sky-400 to-cyan-600 shrink-0 rounded-t-lg" />

        <div className="px-6 py-4 border-b border-slate-700/60 flex items-center justify-between shrink-0">
          <div className="flex items-center gap-3">
            <span className="text-2xl" aria-hidden="true">
              🚢
            </span>
            <div>
              <h2
                id="modal-gestion-convoy-title"
                className="text-base font-bold text-slate-100 leading-tight"
              >
                Gestión de Convoy
              </h2>
              <p className="text-xs text-slate-400 mt-0.5 font-mono">
                Viaje {viajeId}
                {convoy?.nombreBuque ? (
                  <span className="text-slate-500"> · {convoy.nombreBuque}</span>
                ) : null}
              </p>
            </div>
          </div>
          <button
            type="button"
            onClick={onClose}
            disabled={isLoading}
            aria-label="Cerrar modal"
            className="text-slate-500 hover:text-slate-200 disabled:opacity-40 disabled:cursor-not-allowed transition-colors rounded p-1 focus:outline-none focus:ring-2 focus:ring-cyan-500/50"
          >
            <svg className="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" />
            </svg>
          </button>
        </div>

        <div className="flex-1 overflow-y-auto px-6 py-5 min-h-0">
          {isLoading && (
            <div className="flex flex-col items-center justify-center gap-3 py-16">
              <svg className="w-8 h-8 animate-spin text-cyan-500" fill="none" viewBox="0 0 24 24">
                <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" />
                <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8v8H4z" />
              </svg>
              <p className="text-sm text-slate-400">Cargando composición del convoy…</p>
            </div>
          )}

          {fetchError && !isLoading && (
            <div className="flex flex-col items-center justify-center gap-4 py-16 text-center">
              <div className="w-14 h-14 rounded-full bg-red-950/50 border border-red-500/30 flex items-center justify-center">
                <svg className="w-7 h-7 text-red-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path
                    strokeLinecap="round"
                    strokeLinejoin="round"
                    strokeWidth={2}
                    d="M12 9v2m0 4h.01M10.29 3.86L1.82 18a2 2 0 001.71 3h16.94a2 2 0 001.71-3L13.71 3.86a2 2 0 00-3.42 0z"
                  />
                </svg>
              </div>
              <div>
                <p className="text-slate-200 font-semibold text-sm">No se pudo cargar el convoy</p>
                <p className="text-slate-500 text-xs mt-1 max-w-md">{fetchError}</p>
              </div>
              <button
                type="button"
                onClick={handleRefresh}
                className="px-4 py-2 bg-cyan-600 text-white text-xs font-semibold rounded-lg hover:bg-cyan-500 transition-colors"
              >
                Reintentar
              </button>
            </div>
          )}

          {convoy && !isLoading && !fetchError && (
            <PanelGestionConvoy
              viajeId={viajeId}
              convoy={convoy}
              onRefreshConvoy={handleRefresh}
            />
          )}
        </div>
      </div>
    </div>
  );
}
