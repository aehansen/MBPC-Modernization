import { useState } from "react";
import { cargaApi } from "../../axiosClient";

/**
 * CargaDeleteModal
 *
 * Modal de confirmación para eliminar una carga del manifiesto.
 *
 * Props:
 * carga     {CargaDto}  – Carga a eliminar (id, viajeId, descripcionLista, etc.)
 * onClose   {Function}  – Callback para cerrar el modal sin cambios.
 * onSuccess {Function}  – Callback ejecutado tras una eliminación exitosa.
 */
export default function CargaDeleteModal({ isOpen, onClose, carga, viajeId, onSuccess }) {
  const [isLoading, setIsLoading] = useState(false);
  const [serverError, setServerError] = useState(null);

  const handleConfirm = async () => {
    setIsLoading(true);
    setServerError(null);
    try {
        const idViajeReal = viajeId || carga?.viajeId;

      // 2. Si sigue sin existir, frenamos todo y avisamos en la UI
      if (!idViajeReal) {
        setServerError("Falta el ID del viaje. El componente padre no está pasando el dato correctamente.");
        setIsLoading(false);
        return;
      }

      // 3. Enviamos los datos reales y validados
      await cargaApi.delete(idViajeReal, carga.id);
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
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 backdrop-blur-sm p-4">
      <div
        className="w-full max-w-md rounded-xl bg-white shadow-2xl ring-1 ring-black/5 overflow-hidden flex flex-col"
        role="dialog"
        aria-modal="true"
        aria-labelledby="delete-modal-title"
      >
        <div className="p-6">
          <h2
            id="delete-modal-title"
            className="text-xl font-semibold text-slate-800 mb-2 flex items-center gap-2"
          >
            <svg
              className="h-6 w-6 text-red-600"
              fill="none"
              strokeWidth="1.5"
              stroke="currentColor"
              viewBox="0 0 24 24"
            >
              <path
                strokeLinecap="round"
                strokeLinejoin="round"
                d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z"
              />
            </svg>
            Confirmar Eliminación
          </h2>
          <p className="text-sm text-slate-600 mb-4">
            ¿Estás seguro que deseás eliminar la carga{" "}
            <strong className="text-slate-800">
              {carga.descripcionLista || `ID: ${carga.id}`}
            </strong>{" "}
            del manifiesto?
            <br />
            <span className="text-red-600 font-medium block mt-2">
              Esta acción no se puede deshacer.
            </span>
          </p>

          {serverError && (
            <div className="rounded-md bg-red-50 p-3 mb-4 ring-1 ring-red-200">
              <p className="text-sm text-red-800">{serverError}</p>
            </div>
          )}
        </div>

        <div className="flex justify-end gap-3 bg-slate-50 border-t border-slate-200 px-6 py-4">
          <button
            type="button"
            onClick={onClose}
            disabled={isLoading}
            className="rounded-md px-4 py-2 text-sm font-medium text-slate-600 hover:bg-slate-200 transition-colors disabled:opacity-50"
          >
            Cancelar
          </button>
          <button
            type="button"
            onClick={handleConfirm}
            disabled={isLoading}
            className="flex items-center gap-2 rounded-md bg-red-600 px-4 py-2 text-sm font-medium text-white hover:bg-red-700 focus:outline-none focus:ring-2 focus:ring-red-500 focus:ring-offset-2 transition-colors disabled:opacity-60"
          >
            {isLoading ? "Eliminando..." : "Eliminar Carga"}
          </button>
        </div>
      </div>
    </div>
  );
}