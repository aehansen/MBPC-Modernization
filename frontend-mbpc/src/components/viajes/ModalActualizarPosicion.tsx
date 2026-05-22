// src/components/viajes/ModalPosicion.tsx
// Hito 10.6 — Registrar posición GPS de un viaje activo

import { useState, type FormEvent } from 'react';
import { isAxiosError } from 'axios';
import { useActualizarPosicion } from '@/hooks/useActualizarPosicion';

// ─── Helpers ─────────────────────────────────────────────────────────────────

/** Retorna la fecha/hora local formateada como valor compatible con <input type="datetime-local"> */
function nowLocalDateTimeValue(): string {
  const now = new Date();
  // Ajustamos al offset local sin depender de toISOString() (que es UTC)
  const pad = (n: number) => String(n).padStart(2, '0');
  return (
    `${now.getFullYear()}-${pad(now.getMonth() + 1)}-${pad(now.getDate())}` +
    `T${pad(now.getHours())}:${pad(now.getMinutes())}`
  );
}

// ─── Props ────────────────────────────────────────────────────────────────────

interface ModalPosicionProps {
  isOpen: boolean;
  viajeId: string;
  nombreBuque: string;
  onClose: () => void;
  /** Callback opcional que se invoca tras guardar exitosamente. */
  onSuccess?: () => void;
}

// ─── Tipos de estado interno ──────────────────────────────────────────────────

interface FormState {
  latitud: string;
  longitud: string;
  fechaReporte: string;
}

interface AsyncState {
  errorMsg: string | null;
  successMsg: string | null;
}

// ─── Componente ───────────────────────────────────────────────────────────────

export function ModalPosicion({
  isOpen,
  viajeId,
  nombreBuque,
  onClose,
  onSuccess,
}: ModalPosicionProps) {
  const [form, setForm] = useState<FormState>({
    latitud: '',
    longitud: '',
    fechaReporte: nowLocalDateTimeValue(),
  });

  const [asyncState, setAsyncState] = useState<AsyncState>({
    errorMsg: null,
    successMsg: null,
  });
  const [submitError, setSubmitError] = useState<string | null>(null);

  const { mutate: actualizarPosicion, isPending } = useActualizarPosicion(viajeId);

  if (!isOpen) return null;

  // ─── Handlers ──────────────────────────────────────────────────────────────

  const handleChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const { name, value } = e.target;
    setForm((prev) => ({ ...prev, [name]: value }));
    // Limpia mensajes al editar
    setAsyncState((prev) => ({ ...prev, errorMsg: null, successMsg: null }));
    setSubmitError(null);
  };

  const handleSubmit = (e: FormEvent<HTMLFormElement>) => {
    e.preventDefault();

    const latNum = parseFloat(form.latitud);
    const lonNum = parseFloat(form.longitud);

    if (isNaN(latNum) || latNum < -90 || latNum > 90) {
      setAsyncState((prev) => ({
        ...prev,
        errorMsg: 'La latitud debe ser un número entre -90 y 90.',
      }));
      return;
    }
    if (isNaN(lonNum) || lonNum < -180 || lonNum > 180) {
      setAsyncState((prev) => ({
        ...prev,
        errorMsg: 'La longitud debe ser un número entre -180 y 180.',
      }));
      return;
    }
    if (!form.fechaReporte) {
      setAsyncState((prev) => ({
        ...prev,
        errorMsg: 'Seleccioná una fecha y hora de reporte.',
      }));
      return;
    }

    const payload = {
      latitud: latNum,
      longitud: lonNum,
      // El input datetime-local devuelve "YYYY-MM-DDTHH:mm"; lo enviamos como ISO 8601
      fechaReporte: new Date(form.fechaReporte).toISOString(),
    };

    setSubmitError(null);
    setAsyncState({ errorMsg: null, successMsg: null });

    actualizarPosicion(payload, {
      onSuccess: () => {
        setAsyncState({
          errorMsg: null,
          successMsg: '¡Posición actualizada correctamente!',
        });
        onSuccess?.();
        setTimeout(() => {
          onClose();
          setAsyncState({ errorMsg: null, successMsg: null });
          setSubmitError(null);
        }, 1200);
      },
      onError: (error) => {
        const axiosError = isAxiosError(error) ? error : undefined;
        const data = axiosError?.response?.data as { mensaje?: string } | string | undefined;
        const msg =
          (typeof data === 'object' && data !== null ? data.mensaje : undefined)
          || data
          || error.message;
        setSubmitError(typeof msg === 'string' ? msg : 'Error al actualizar');
      },
    });
  };

  const handleClose = () => {
    if (isPending) return;
    onClose();
    setAsyncState({ errorMsg: null, successMsg: null });
    setSubmitError(null);
  };

  // ─── Render ─────────────────────────────────────────────────────────────────

  return (
    /* Backdrop */
    <div
      className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/60 backdrop-blur-sm"
      role="dialog"
      aria-modal="true"
      aria-labelledby="modal-posicion-title"
      onClick={(e) => {
        if (e.target === e.currentTarget) handleClose();
      }}
    >
      {/* Panel */}
      <div className="w-full max-w-md bg-slate-900 border border-slate-700/70 rounded-2xl shadow-2xl overflow-hidden">
        {/* Header */}
        <div className="flex items-center justify-between px-6 py-4 border-b border-slate-700/60 bg-slate-800/60">
          <div className="flex items-center gap-3">
            <span className="text-2xl" role="img" aria-label="Posición">📍</span>
            <div>
              <h2
                id="modal-posicion-title"
                className="text-base font-bold text-slate-100 leading-tight"
              >
                Actualizar Posición
              </h2>
              <p className="text-xs text-slate-400 font-mono mt-0.5 truncate max-w-[18rem]">
                {nombreBuque || viajeId}
              </p>
            </div>
          </div>
          <button
            type="button"
            onClick={handleClose}
            disabled={isPending}
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
          <div className="px-6 py-5 space-y-5">

            {/* Error banner */}
            {(asyncState.errorMsg || submitError) && (
              <div
                role="alert"
                className="flex items-start gap-3 px-4 py-3 bg-red-950/50 border border-red-500/40 rounded-xl text-sm"
              >
                <svg className="w-4 h-4 text-red-400 mt-0.5 shrink-0" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 8v4m0 4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
                </svg>
                <p className="text-red-300 leading-snug">{submitError ?? asyncState.errorMsg}</p>
              </div>
            )}

            {/* Success banner */}
            {asyncState.successMsg && (
              <div
                role="status"
                className="flex items-center gap-3 px-4 py-3 bg-emerald-950/50 border border-emerald-500/40 rounded-xl text-sm"
              >
                <svg className="w-4 h-4 text-emerald-400 shrink-0" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M5 13l4 4L19 7" />
                </svg>
                <p className="text-emerald-300 leading-snug">{asyncState.successMsg}</p>
              </div>
            )}

            {/* Latitud */}
            <div>
              <label
                htmlFor="latitud"
                className="block text-xs font-semibold text-slate-400 uppercase tracking-wider mb-1.5"
              >
                Latitud <span className="text-slate-600 normal-case font-normal">(−90 a 90)</span>
              </label>
              <input
                id="latitud"
                name="latitud"
                type="number"
                step="any"
                min={-90}
                max={90}
                required
                value={form.latitud}
                onChange={handleChange}
                placeholder="-34.6037"
                className="w-full bg-slate-800 border border-slate-600 rounded-lg px-3 py-2.5
                           text-slate-200 placeholder-slate-600 text-sm font-mono
                           focus:outline-none focus:ring-2 focus:ring-cyan-500/60 focus:border-cyan-500/60
                           transition-all"
              />
            </div>

            {/* Longitud */}
            <div>
              <label
                htmlFor="longitud"
                className="block text-xs font-semibold text-slate-400 uppercase tracking-wider mb-1.5"
              >
                Longitud <span className="text-slate-600 normal-case font-normal">(−180 a 180)</span>
              </label>
              <input
                id="longitud"
                name="longitud"
                type="number"
                step="any"
                min={-180}
                max={180}
                required
                value={form.longitud}
                onChange={handleChange}
                placeholder="-58.3816"
                className="w-full bg-slate-800 border border-slate-600 rounded-lg px-3 py-2.5
                           text-slate-200 placeholder-slate-600 text-sm font-mono
                           focus:outline-none focus:ring-2 focus:ring-cyan-500/60 focus:border-cyan-500/60
                           transition-all"
              />
            </div>

            {/* Fecha y hora de reporte */}
            <div>
              <label
                htmlFor="fechaReporte"
                className="block text-xs font-semibold text-slate-400 uppercase tracking-wider mb-1.5"
              >
                Fecha y hora del reporte
              </label>
              <input
                id="fechaReporte"
                name="fechaReporte"
                type="datetime-local"
                required
                value={form.fechaReporte}
                onChange={handleChange}
                className="w-full bg-slate-800 border border-slate-600 rounded-lg px-3 py-2.5
                           text-slate-200 text-sm
                           focus:outline-none focus:ring-2 focus:ring-cyan-500/60 focus:border-cyan-500/60
                           transition-all [color-scheme:dark]"
              />
            </div>
          </div>

          {/* Footer */}
          <div className="flex justify-end gap-3 px-6 py-4 border-t border-slate-700/60 bg-slate-800/40">
            <button
              type="button"
              onClick={handleClose}
              disabled={isPending}
              className="px-4 py-2 rounded-lg text-sm font-semibold text-slate-400 border border-slate-700
                         hover:bg-slate-700/60 hover:text-slate-200 disabled:opacity-40
                         transition-colors"
            >
              Cancelar
            </button>
            <button
              type="submit"
              disabled={isPending}
              className="px-5 py-2 rounded-lg text-sm font-bold text-white bg-cyan-600
                         hover:bg-cyan-500 disabled:opacity-50 disabled:cursor-not-allowed
                         transition-colors shadow-md shadow-cyan-900/40 flex items-center gap-2"
            >
              {isPending && (
                <svg className="w-4 h-4 animate-spin" fill="none" viewBox="0 0 24 24">
                  <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" />
                  <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8v8H4z" />
                </svg>
              )}
              {isPending ? 'Guardando…' : 'Guardar Posición'}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}

export default ModalPosicion;
