/**
 * src/components/viajes/ModalActualizarPosicion.tsx
 *
 * Modal institucional para actualizar la posición geográfica de un buque.
 * - Tipado estricto: cero `any`. Errores manejados con isAxiosError + ProblemDetails robusto.
 * - TanStack Query v5 a través de useActualizarPosicion.
 * - Accesibilidad: role="dialog", aria-modal, aria-labelledby, foco atrapado básico.
 * - UI: Tailwind CSS, tema azul institucional PNA.
 */

import { useState, useEffect, useId } from 'react';
import { isAxiosError } from 'axios';
import { parseCoordinates, formatearDMS } from '@/utils/coordinates';
import { useActualizarPosicion } from '@/hooks/useActualizarPosicion';
import type { CoordenadasDecimales } from '@/utils/coordinates';

// ---------------------------------------------------------------------------
// Props
// ---------------------------------------------------------------------------

interface ModalActualizarPosicionProps {
  viajeId: string;
  nombreBuque: string;
  onClose: () => void;
  /** Callback opcional invocado tras un éxito confirmado antes de cerrar. */
  onExito?: () => void;
}

// ---------------------------------------------------------------------------
// Tipos Locales (Error Handling)
// ---------------------------------------------------------------------------

interface DotNetProblemDetails {
  detail?: string;
  title?: string;
  mensaje?: string; // Fallback por si hay código legacy
  errors?: Record<string, string[]>; // Formato de ValidationProblemDetails de .NET
}

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

/**
 * Extrae un mensaje legible del error de la mutación sin recurrir a `any`.
 * Maneja texto plano, ProblemDetails estándar, y ValidationProblemDetails (ModelState).
 */
function resolverMensajeError(error: Error | null): string {
  if (!error) return '';

  if (isAxiosError<DotNetProblemDetails | string>(error) && error.response?.data) {
    const data = error.response.data;

    // 1. Si el backend devuelve un string plano (text/plain)
    if (typeof data === 'string') {
      return data;
    }

    // 2. Si el backend devuelve un objeto (application/problem+json)
    if (typeof data === 'object' && data !== null) {
      // 2a. Si tiene diccionario de errores (ValidationProblemDetails)
      if (data.errors && Object.keys(data.errors).length > 0) {
        const primeraLlave = Object.keys(data.errors)[0];
        const primerError = data.errors[primeraLlave][0];
        if (primerError) return primerError;
      }

      // 2b. ProblemDetails estándar o custom
      if (data.detail) return data.detail;
      if (data.mensaje) return data.mensaje;
      
      // Evitamos mostrar el título genérico poco amigable de .NET
      if (data.title && !data.title.toLowerCase().includes('validation')) {
        return data.title;
      }
    }
  }

  // Fallback final genérico de Axios
  return error.message;
}

// ---------------------------------------------------------------------------
// Íconos inline (SVG) para no depender de librerías de íconos externas
// ---------------------------------------------------------------------------

const IconClose = () => (
  <svg className="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24" aria-hidden="true">
    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" />
  </svg>
);

const IconCheck = () => (
  <svg className="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24" aria-hidden="true">
    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 12l2 2 4-4m6 2a9 9 0 11-18 0 9 9 0 0118 0z" />
  </svg>
);

const IconError = () => (
  <svg className="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24" aria-hidden="true">
    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 8v4m0 4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
  </svg>
);

const IconSpinner = () => (
  <svg
    className="animate-spin w-4 h-4 text-white"
    fill="none"
    viewBox="0 0 24 24"
    aria-hidden="true"
  >
    <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" />
    <path
      className="opacity-75"
      fill="currentColor"
      d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4z"
    />
  </svg>
);

// ---------------------------------------------------------------------------
// Componente principal
// ---------------------------------------------------------------------------

export default function ModalActualizarPosicion({
  viajeId,
  nombreBuque,
  onClose,
  onExito,
}: ModalActualizarPosicionProps) {
  const inputId = useId();
  const titleId = useId();

  const [inputValor, setInputValor] = useState('');
  const [coordsParsed, setCoordsParsed] = useState<CoordenadasDecimales | null>(null);

  const mutation = useActualizarPosicion(viajeId);

  // Parsear coordenadas en tiempo real y resetear la mutación al editar.
  useEffect(() => {
    setCoordsParsed(parseCoordinates(inputValor));
    mutation.reset();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [inputValor]);

  const handleSubmit = () => {
    if (!coordsParsed) return;

    mutation.mutate(
      {
        latitud: coordsParsed.lat,
        longitud: coordsParsed.lng,
        fechaReporte: new Date().toISOString(),
      },
      {
        onSuccess: () => {
          setTimeout(() => {
            onExito?.();
            onClose();
          }, 2500);
        },
      },
    );
  };

  // El botón "Guardar" solo se habilita si hay coordenadas válidas,
  // la mutación no está en curso y aún no fue exitosa.
  const puedeEnviar =
    coordsParsed !== null && !mutation.isPending && !mutation.isSuccess;

  // Mensaje de error resuelto de forma tipada (sin `any`).
  const errorMessage = resolverMensajeError(mutation.error);

  // ---------------------------------------------------------------------------
  // Render
  // ---------------------------------------------------------------------------

  return (
    /* Overlay con blur */
    <div
      className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-gray-900/60 backdrop-blur-sm"
      role="presentation"
      onClick={(e) => {
        // Cierra al hacer clic fuera de la tarjeta si no hay operación en curso.
        if (e.target === e.currentTarget && !mutation.isPending) onClose();
      }}
    >
      {/* Tarjeta del modal */}
      <div
        role="dialog"
        aria-modal="true"
        aria-labelledby={titleId}
        className="bg-white rounded-2xl shadow-2xl w-full max-w-md overflow-hidden flex flex-col"
      >
        {/* ── Header institucional ───────────────────────────────────────── */}
        <div className="bg-[#002454] px-6 py-4 flex items-center justify-between gap-4">
          <div className="flex items-center gap-3 min-w-0">
            <img
              src="https://www.argentina.gob.ar/sites/default/files/styles/isotipo/public/imagenEncabezado/prefectura-escudo.png?itok=EywBfOaV"
              alt="Escudo PNA"
              className="h-9 w-auto flex-shrink-0"
            />
            <div className="min-w-0">
              <h2
                id={titleId}
                className="text-white font-bold text-lg leading-tight truncate"
              >
                Actualizar Posición
              </h2>
              <p className="text-blue-200 text-xs font-semibold tracking-widest uppercase truncate">
                {nombreBuque}
              </p>
            </div>
          </div>

          <button
            type="button"
            onClick={onClose}
            disabled={mutation.isPending}
            aria-label="Cerrar modal"
            className="flex-shrink-0 text-blue-200 hover:text-white transition-colors disabled:opacity-40 focus:outline-none focus-visible:ring-2 focus-visible:ring-white rounded"
          >
            <IconClose />
          </button>
        </div>

        {/* ── Cuerpo ────────────────────────────────────────────────────── */}
        <div className="p-6 flex flex-col gap-5">
          {/* Campo de coordenadas */}
          <div>
            <label
              htmlFor={inputId}
              className="block text-xs font-bold text-gray-600 uppercase tracking-widest mb-2"
            >
              Coordenadas <span className="normal-case font-normal text-gray-400">(DMS o Decimal)</span>
            </label>
            <input
              id={inputId}
              type="text"
              autoFocus
              autoComplete="off"
              spellCheck={false}
              disabled={mutation.isPending || mutation.isSuccess}
              placeholder='Ej: 34° 35′ 29″ S, 58° 21′ 22″ W'
              className="
                w-full px-4 py-3
                bg-gray-50 border border-gray-300 rounded-xl
                text-gray-900 placeholder-gray-400 text-sm font-mono
                transition-all outline-none
                focus:bg-white focus:ring-2 focus:ring-[#104a8e] focus:border-transparent
                disabled:opacity-60 disabled:cursor-not-allowed
              "
              value={inputValor}
              onChange={(e) => setInputValor(e.target.value)}
            />
          </div>

          {/* ── Feedback de parsing ─────────────────────────────────────── */}
          {inputValor.trim().length > 0 && !mutation.isSuccess && (
            <div
              className={`
                px-4 py-3 rounded-xl border flex items-start gap-3
                transition-colors duration-200
                ${coordsParsed
                  ? 'bg-green-50 border-green-200'
                  : 'bg-red-50 border-red-200'}
              `}
            >
              {coordsParsed ? (
                <>
                  <span className="text-green-600 mt-0.5 flex-shrink-0">
                    <IconCheck />
                  </span>
                  <div className="min-w-0">
                    <p className="text-green-800 text-sm font-semibold">
                      Coordenadas válidas
                    </p>
                    <p className="text-green-700 text-xs font-mono mt-1 leading-relaxed">
                      Lat: {coordsParsed.lat.toFixed(6)}
                      <br />
                      Lng: {coordsParsed.lng.toFixed(6)}
                    </p>
                    <p className="text-green-600/80 text-[10px] mt-1">
                      {formatearDMS(coordsParsed.lat, coordsParsed.lng)}
                    </p>
                  </div>
                </>
              ) : (
                <>
                  <span className="text-red-500 mt-0.5 flex-shrink-0">
                    <IconError />
                  </span>
                  <p className="text-red-700 text-sm font-medium leading-snug">
                    Formato irreconocible. Revisa los símbolos o usa formato decimal.
                  </p>
                </>
              )}
            </div>
          )}

          {/* ── Error del backend (tipado con isAxiosError + ProblemDetails) ── */}
          {mutation.isError && errorMessage && (
            <div
              role="alert"
              className="px-4 py-3 bg-red-50 border border-red-200 rounded-xl flex items-start gap-3"
            >
              <span className="text-red-500 mt-0.5 flex-shrink-0">
                <IconError />
              </span>
              <div>
                <p className="text-red-800 text-sm font-semibold">
                  Error al actualizar
                </p>
                <p className="text-red-700 text-xs mt-0.5 leading-snug">
                  {errorMessage}
                </p>
              </div>
            </div>
          )}

          {/* ── Éxito con datos cinemáticos ─────────────────────────────── */}
          {mutation.isSuccess && mutation.data && (
            <div
              role="status"
              aria-live="polite"
              className="px-4 py-4 bg-blue-50 border border-blue-200 rounded-xl"
            >
              <div className="flex items-center gap-2 mb-3">
                <span className="text-[#104a8e]">
                  <IconCheck />
                </span>
                <p className="text-[#002454] font-bold text-sm">
                  Posición actualizada correctamente
                </p>
              </div>

              <div className="grid grid-cols-2 gap-3">
                {/* Velocidad */}
                <div className="bg-white px-3 py-2.5 rounded-lg border border-blue-100 text-center">
                  <p className="text-[10px] text-gray-500 uppercase font-bold tracking-wider mb-1">
                    Velocidad
                  </p>
                  <p className="text-base font-mono font-semibold text-[#104a8e]">
                    {mutation.data.velocidadCalculadaKn}
                    <span className="text-xs font-normal text-gray-500 ml-1">kn</span>
                  </p>
                </div>

                {/* Distancia */}
                <div className="bg-white px-3 py-2.5 rounded-lg border border-blue-100 text-center">
                  <p className="text-[10px] text-gray-500 uppercase font-bold tracking-wider mb-1">
                    Distancia
                  </p>
                  <p className="text-base font-mono font-semibold text-[#104a8e]">
                    {mutation.data.distanciaRecorridaNM}
                    <span className="text-xs font-normal text-gray-500 ml-1">nm</span>
                  </p>
                </div>
              </div>

              <p className="text-[10px] text-blue-400 text-center mt-2">
                Cerrando automáticamente…
              </p>
            </div>
          )}
        </div>

        {/* ── Footer con acciones ─────────────────────────────────────────── */}
        <div className="px-6 py-4 bg-gray-50 border-t border-gray-100 flex justify-end gap-3">
          <button
            type="button"
            onClick={onClose}
            disabled={mutation.isPending}
            className="
              px-4 py-2 text-sm font-medium rounded-lg
              border border-gray-300 text-gray-700
              hover:bg-gray-100 transition-colors
              disabled:opacity-50 disabled:cursor-not-allowed
              focus:outline-none focus-visible:ring-2 focus-visible:ring-gray-400
            "
          >
            Cancelar
          </button>

          <button
            type="button"
            onClick={handleSubmit}
            disabled={!puedeEnviar}
            aria-busy={mutation.isPending}
            className="
              inline-flex items-center gap-2
              px-5 py-2 text-sm font-semibold rounded-lg text-white
              transition-all duration-200
              focus:outline-none focus-visible:ring-2 focus-visible:ring-[#104a8e] focus-visible:ring-offset-2
              disabled:opacity-40 disabled:cursor-not-allowed
              bg-[#104a8e] hover:bg-[#002454] active:scale-[0.98]
            "
          >
            {mutation.isPending && <IconSpinner />}

            {mutation.isPending
              ? 'Actualizando…'
              : mutation.isSuccess
                ? '✓ Actualizado'
                : 'Guardar Posición'}
          </button>
        </div>
      </div>
    </div>
  );
}