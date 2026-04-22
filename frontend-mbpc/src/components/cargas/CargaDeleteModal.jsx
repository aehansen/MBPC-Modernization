import { useState } from "react";
import axios from "axios";

/**
 * CargaDeleteModal
 *
 * Modal de confirmación para eliminar una carga del manifiesto.
 *
 * Props:
 *   carga     {CargaDto}  – Carga a eliminar (id, viajeId, descripcionLista, etc.)
 *   onClose   {Function}  – Callback para cerrar el modal sin cambios.
 *   onSuccess {Function}  – Callback ejecutado tras una eliminación exitosa.
 */
export default function CargaDeleteModal({ carga, onClose, onSuccess }) {
  const [isLoading, setIsLoading] = useState(false);
  const [serverError, setServerError] = useState(null);

  const handleConfirm = async () => {
    setIsLoading(true);
    setServerError(null);

    const token = localStorage.getItem("mbpc_token");

    try {
      // Hito 5.8: La ruta ahora incluye el viajeId para el doble filtro en MongoDB.
      // Esto previene que bodegas con id="0" sean eliminadas del viaje incorrecto.
      await axios.delete(
        `/api/carga/viaje/${encodeURIComponent(carga.viajeId)}/carga/${encodeURIComponent(carga.id)}`,
        {
          headers: {
            Authorization: `Bearer ${token}`,
          },
        }
      );

      onSuccess?.();
      onClose?.();
    } catch (err) {
      const mensaje =
        err.response?.data?.mensaje ??
        "No se pudo eliminar la carga. Intentá nuevamente.";
      setServerError(mensaje);
    } finally {
      setIsLoading(false);
    }
  };

  return (
    /* ── Overlay ── */
    <div
      className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 backdrop-blur-sm"
      role="alertdialog"
      aria-modal="true"
      aria-labelledby="delete-modal-title"
      aria-describedby="delete-modal-desc"
    >
      {/* ── Panel ── */}
      <div className="w-full max-w-sm rounded-md bg-white shadow-lg ring-1 ring-slate-200">
        {/* Header */}
        <div className="flex items-center gap-3 border-b border-slate-200 px-6 py-4">
          {/* Warning icon */}
          <span className="flex h-9 w-9 shrink-0 items-center justify-center rounded-full bg-red-100">
            <svg
              xmlns="http://www.w3.org/2000/svg"
              className="h-5 w-5 text-red-600"
              viewBox="0 0 20 20"
              fill="currentColor"
            >
              <path
                fillRule="evenodd"
                d="M8.257 3.099c.765-1.36 2.722-1.36 3.486 0l5.58 9.92c.75 1.334-.213 2.98-1.742 2.98H4.42c-1.53 0-2.493-1.646-1.743-2.98l5.58-9.92zM11 13a1 1 0 11-2 0 1 1 0 012 0zm-1-8a1 1 0 00-1 1v3a1 1 0 002 0V6a1 1 0 00-1-1z"
                clipRule="evenodd"
              />
            </svg>
          </span>
          <h2
            id="delete-modal-title"
            className="text-base font-semibold text-slate-800"
          >
            Eliminar Carga
          </h2>
        </div>

        {/* Body */}
        <div className="px-6 py-5 space-y-3">
          <p
            id="delete-modal-desc"
            className="text-sm text-slate-600"
          >
            Estás por eliminar la siguiente carga del manifiesto:
          </p>

          <div className="rounded-md bg-slate-50 px-4 py-3 ring-1 ring-slate-200">
            <p className="text-sm font-medium text-slate-800">
              {carga?.descripcionLista ?? carga?.id}
            </p>
            {carga?.tonelaje != null && (
              <p className="mt-0.5 text-xs text-slate-500">
                {carga.tonelaje.toLocaleString("es-AR")} Tn
              </p>
            )}
          </div>

          <p className="text-sm font-medium text-red-600">
            ⚠ Esta acción no se puede deshacer. La carga será removida de Oracle
            y de MongoDB.
          </p>

          {/* Error del servidor */}
          {serverError && (
            <p className="rounded-md bg-red-50 px-4 py-2 text-sm font-medium text-red-600 ring-1 ring-red-200">
              {serverError}
            </p>
          )}
        </div>

        {/* Footer */}
        <div className="flex justify-end gap-3 border-t border-slate-200 px-6 py-4">
          <button
            type="button"
            onClick={onClose}
            disabled={isLoading}
            className="rounded-md px-4 py-2 text-sm font-medium text-slate-600 hover:bg-slate-100 transition-colors disabled:opacity-50"
          >
            Cancelar
          </button>
          <button
            type="button"
            onClick={handleConfirm}
            disabled={isLoading}
            className="flex items-center gap-2 rounded-md bg-red-600 px-4 py-2 text-sm font-medium text-white hover:bg-red-700 focus:outline-none focus:ring-2 focus:ring-red-500 focus:ring-offset-2 transition-colors disabled:opacity-60"
          >
            {isLoading && (
              <svg
                className="h-4 w-4 animate-spin text-white"
                xmlns="http://www.w3.org/2000/svg"
                fill="none"
                viewBox="0 0 24 24"
              >
                <circle
                  className="opacity-25"
                  cx="12"
                  cy="12"
                  r="10"
                  stroke="currentColor"
                  strokeWidth="4"
                />
                <path
                  className="opacity-75"
                  fill="currentColor"
                  d="M4 12a8 8 0 018-8v8H4z"
                />
              </svg>
            )}
            {isLoading ? "Eliminando..." : "Sí, eliminar"}
          </button>
        </div>
      </div>
    </div>
  );
}
