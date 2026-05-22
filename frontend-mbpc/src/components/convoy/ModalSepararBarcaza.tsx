// src/components/convoy/ModalSepararBarcaza.tsx
// Hito 10.6 — Liberar/separar una barcaza específica del convoy

import { useState, type FormEvent } from 'react';
import { useSepararConvoy } from '@/hooks/useGestionConvoy';
import type { DotNetProblemDetails } from '@/hooks/useGestionConvoy';
import { isAxiosError } from 'axios';

// ─── Helpers ─────────────────────────────────────────────────────────────────

function resolverMensajeError(error: Error | null): string {
  if (!error) return '';
  if (isAxiosError<DotNetProblemDetails | string>(error) && error.response?.data) {
    const data = error.response.data;
    if (typeof data === 'string') return data;
    if (typeof data === 'object' && data !== null) {
      if (data.errors && Object.keys(data.errors).length > 0) {
        return data.errors[Object.keys(data.errors)[0]][0];
      }
      if (data.detail) return data.detail;
      if (data.mensaje) return data.mensaje;
      if (data.title && !data.title.toLowerCase().includes('validation')) return data.title;
    }
  }
  return error.message;
}

// ─── Props ────────────────────────────────────────────────────────────────────

interface ModalSepararBarcazaProps {
  isOpen: boolean;
  viajeId: string;
  barcazaId: string;
  barcazaNombre: string;
  onClose: () => void;
  /** Callback opcional invocado tras la mutación exitosa. */
  onSuccess?: () => void;
}

// ─── Componente ───────────────────────────────────────────────────────────────

export function ModalSepararBarcaza({
  isOpen,
  viajeId,
  barcazaId,
  barcazaNombre,
  onClose,
  onSuccess,
}: ModalSepararBarcazaProps) {
  const [ubicacion, setUbicacion] = useState('');
  const [localError, setLocalError] = useState<string | null>(null);

  const mutSeparar = useSepararConvoy();

  if (!isOpen) return null;

  // ─── Handlers ──────────────────────────────────────────────────────────────

  const handleSubmit = (e: FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    setLocalError(null);

    if (!ubicacion.trim()) {
      setLocalError('Ingresá la ubicación de separación antes de continuar.');
      return;
    }

    mutSeparar.mutate(
      {
        viajeId,
        payload: {
          barcazasIds: [barcazaId],
          ubicacion: ubicacion.trim(),
        },
      },
      {
        onSuccess: () => {
          onSuccess?.();
          handleClose();
        },
      },
    );
  };

  const handleClose = () => {
    if (mutSeparar.isPending) return;
    setUbicacion('');
    setLocalError(null);
    mutSeparar.reset();
    onClose();
  };

  const errorMsg = localError ?? (mutSeparar.error ? resolverMensajeError(mutSeparar.error) : null);

  // ─── Render ─────────────────────────────────────────────────────────────────

  return (
    <div
      className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/60 backdrop-blur-sm"
      role="dialog"
      aria-modal="true"
      aria-labelledby="modal-separar-title"
      onClick={(e) => {
        if (e.target === e.currentTarget) handleClose();
      }}
    >
      <div className="w-full max-w-sm bg-slate-900 border border-slate-700/70 rounded-2xl shadow-2xl overflow-hidden">
        {/* Header */}
        <div className="flex items-center justify-between px-6 py-4 border-b border-slate-700/60 bg-slate-800/60">
          <div className="flex items-center gap-3">
            {/* Ícono de separación: dos flechas apuntando hacia afuera */}
            <span className="text-2xl" role="img" aria-label="Separar">🔓</span>
            <div>
              <h2
                id="modal-separar-title"
                className="text-base font-bold text-slate-100 leading-tight"
              >
                Liberar Barcaza
              </h2>
              <p className="text-xs text-cyan-400 font-mono mt-0.5 truncate max-w-[14rem]" title={barcazaNombre}>
                {barcazaNombre}
              </p>
            </div>
          </div>
          <button
            type="button"
            onClick={handleClose}
            disabled={mutSeparar.isPending}
            className="text-slate-500 hover:text-slate-200 transition-colors disabled:opacity-40 p-1"
            aria-label="Cerrar modal"
          >
            <svg className="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" />
            </svg>
          </button>
        </div>

        {/* Body */}
        <form onSubmit={handleSubmit} noValidate>
          <div className="px-6 py-5 space-y-4">
            {/* Descripción contextual con nombre de la barcaza */}
            <div className="flex items-start gap-3 p-3 rounded-xl bg-slate-800/60 border border-slate-700/50">
              <svg className="w-4 h-4 text-slate-500 mt-0.5 shrink-0" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M13 16h-1v-4h-1m1-4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
              </svg>
              <p className="text-xs text-slate-400 leading-relaxed">
                Se separará la barcaza{' '}
                <span className="text-slate-200 font-semibold">{barcazaNombre}</span>{' '}
                del convoy activo. Esta acción es irreversible dentro del viaje actual.
              </p>
            </div>

            {/* Error banner */}
            {errorMsg && (
              <div
                role="alert"
                className="flex items-start gap-3 px-4 py-3 bg-red-950/50 border border-red-500/40 rounded-xl text-sm"
              >
                <svg className="w-4 h-4 text-red-400 mt-0.5 shrink-0" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 8v4m0 4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
                </svg>
                <p className="text-red-300 leading-snug">{errorMsg}</p>
              </div>
            )}

            {/* Ubicación */}
            <div>
              <label
                htmlFor="ubicacion"
                className="block text-xs font-semibold text-slate-400 uppercase tracking-wider mb-1.5"
              >
                Ubicación de Separación
              </label>
              <input
                id="ubicacion"
                name="ubicacion"
                type="text"
                required
                maxLength={200}
                value={ubicacion}
                onChange={(e) => {
                  setUbicacion(e.target.value);
                  setLocalError(null);
                  mutSeparar.reset();
                }}
                placeholder="Ej: Puerto Ibicuy - Muelle 3"
                className="w-full bg-slate-800 border border-slate-600 rounded-lg px-3 py-2.5
                           text-slate-200 placeholder-slate-600 text-sm
                           focus:outline-none focus:ring-2 focus:ring-cyan-500/60 focus:border-cyan-500/60
                           transition-all"
              />
              <p className="mt-1.5 text-xs text-slate-600">
                Punto geográfico o nombre del lugar donde se realiza la maniobra.
              </p>
            </div>
          </div>

          {/* Footer */}
          <div className="flex justify-end gap-3 px-6 py-4 border-t border-slate-700/60 bg-slate-800/40">
            <button
              type="button"
              onClick={handleClose}
              disabled={mutSeparar.isPending}
              className="px-4 py-2 rounded-lg text-sm font-semibold text-slate-400 border border-slate-700
                         hover:bg-slate-700/60 hover:text-slate-200 disabled:opacity-40
                         transition-colors"
            >
              Cancelar
            </button>
            <button
              type="submit"
              disabled={mutSeparar.isPending}
              className="px-5 py-2 rounded-lg text-sm font-bold text-white bg-slate-600
                         hover:bg-slate-500 disabled:opacity-50 disabled:cursor-not-allowed
                         transition-colors shadow-md shadow-slate-900/60 flex items-center gap-2"
            >
              {mutSeparar.isPending && (
                <svg className="w-4 h-4 animate-spin" fill="none" viewBox="0 0 24 24">
                  <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" />
                  <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8v8H4z" />
                </svg>
              )}
              {mutSeparar.isPending ? 'Liberando…' : '🔓 Liberar Barcaza'}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}

export default ModalSepararBarcaza;
