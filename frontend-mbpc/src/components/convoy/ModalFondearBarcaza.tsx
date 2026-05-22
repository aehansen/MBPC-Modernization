// src/components/convoy/ModalFondearBarcaza.tsx
// Hito 10.6 — Fondear una barcaza específica del convoy

import { useState, type FormEvent } from 'react';
import { useFondearBarcaza } from '@/hooks/useGestionConvoy';
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

interface ModalFondearBarcazaProps {
  isOpen: boolean;
  viajeId: string;
  barcazaId: string;
  barcazaNombre: string;
  onClose: () => void;
  /** Callback opcional invocado tras la mutación exitosa. */
  onSuccess?: () => void;
}

// ─── Componente ───────────────────────────────────────────────────────────────

export function ModalFondearBarcaza({
  isOpen,
  viajeId,
  barcazaId,
  barcazaNombre,
  onClose,
  onSuccess,
}: ModalFondearBarcazaProps) {
  const [zonaFondeo, setZonaFondeo] = useState('');
  const [localError, setLocalError] = useState<string | null>(null);

  const mutFondear = useFondearBarcaza();

  if (!isOpen) return null;

  // ─── Handlers ──────────────────────────────────────────────────────────────

  const handleSubmit = (e: FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    setLocalError(null);

    if (!zonaFondeo.trim()) {
      setLocalError('Ingresá la zona de fondeo antes de continuar.');
      return;
    }

    mutFondear.mutate(
      {
        barcazaId,
        viajeId,
        payload: { zonaFondeo: zonaFondeo.trim() },
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
    if (mutFondear.isPending) return;
    setZonaFondeo('');
    setLocalError(null);
    mutFondear.reset();
    onClose();
  };

  const errorMsg = localError ?? (mutFondear.error ? resolverMensajeError(mutFondear.error) : null);

  // ─── Render ─────────────────────────────────────────────────────────────────

  return (
    <div
      className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/60 backdrop-blur-sm"
      role="dialog"
      aria-modal="true"
      aria-labelledby="modal-fondear-title"
      onClick={(e) => {
        if (e.target === e.currentTarget) handleClose();
      }}
    >
      <div className="w-full max-w-sm bg-slate-900 border border-slate-700/70 rounded-2xl shadow-2xl overflow-hidden">
        {/* Header */}
        <div className="flex items-center justify-between px-6 py-4 border-b border-slate-700/60 bg-slate-800/60">
          <div className="flex items-center gap-3">
            <span className="text-2xl" role="img" aria-label="Ancla">⚓</span>
            <div>
              <h2
                id="modal-fondear-title"
                className="text-base font-bold text-slate-100 leading-tight"
              >
                Fondear Barcaza
              </h2>
              <p className="text-xs text-amber-400 font-mono mt-0.5 truncate max-w-[14rem]" title={barcazaNombre}>
                {barcazaNombre}
              </p>
            </div>
          </div>
          <button
            type="button"
            onClick={handleClose}
            disabled={mutFondear.isPending}
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
            {/* Descripción contextual */}
            <p className="text-sm text-slate-400 leading-relaxed">
              Indicá la zona donde se fondeará la barcaza. Esta acción cambiará su estado a{' '}
              <span className="text-amber-400 font-semibold">Fondeada</span>.
            </p>

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

            {/* Zona de fondeo */}
            <div>
              <label
                htmlFor="zonaFondeo"
                className="block text-xs font-semibold text-slate-400 uppercase tracking-wider mb-1.5"
              >
                Zona de Fondeo
              </label>
              <input
                id="zonaFondeo"
                name="zonaFondeo"
                type="text"
                required
                maxLength={200}
                value={zonaFondeo}
                onChange={(e) => {
                  setZonaFondeo(e.target.value);
                  setLocalError(null);
                  mutFondear.reset();
                }}
                placeholder="Ej: Km 584 - Paraná Guazú"
                className="w-full bg-slate-800 border border-slate-600 rounded-lg px-3 py-2.5
                           text-slate-200 placeholder-slate-600 text-sm
                           focus:outline-none focus:ring-2 focus:ring-amber-500/60 focus:border-amber-500/60
                           transition-all"
              />
            </div>
          </div>

          {/* Footer */}
          <div className="flex justify-end gap-3 px-6 py-4 border-t border-slate-700/60 bg-slate-800/40">
            <button
              type="button"
              onClick={handleClose}
              disabled={mutFondear.isPending}
              className="px-4 py-2 rounded-lg text-sm font-semibold text-slate-400 border border-slate-700
                         hover:bg-slate-700/60 hover:text-slate-200 disabled:opacity-40
                         transition-colors"
            >
              Cancelar
            </button>
            <button
              type="submit"
              disabled={mutFondear.isPending}
              className="px-5 py-2 rounded-lg text-sm font-bold text-white bg-amber-600
                         hover:bg-amber-500 disabled:opacity-50 disabled:cursor-not-allowed
                         transition-colors shadow-md shadow-amber-900/40 flex items-center gap-2"
            >
              {mutFondear.isPending && (
                <svg className="w-4 h-4 animate-spin" fill="none" viewBox="0 0 24 24">
                  <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" />
                  <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8v8H4z" />
                </svg>
              )}
              {mutFondear.isPending ? 'Fondeando…' : '⚓ Fondear'}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}

export default ModalFondearBarcaza;
