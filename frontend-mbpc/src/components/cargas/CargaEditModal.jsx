import { useState } from "react";
import { useForm } from "react-hook-form";
import axios from "axios";
import TipoCargaAutocomplete from "./TipoCargaAutocomplete";

/**
 * CargaEditModal
 *
 * Modal de edición de una carga existente.
 *
 * Props:
 * carga     {CargaDto}  – Datos actuales de la carga (id, tonelaje, etc.)
 * onClose   {Function}  – Callback para cerrar el modal sin cambios.
 * onSuccess {Function}  – Callback ejecutado tras una edición exitosa.
 */
export default function CargaEditModal({ carga, onClose, onSuccess }) {
  const [serverError, setServerError] = useState(null);

  // Estado para el buscador de mercadería
  const [mercaderiaSeleccionada, setMercaderiaSeleccionada] = useState({
    oracleId: carga?.mercaderiaId || null,
    nombre: carga?.mercaderiaNombre || "",
  });

  const {
    register,
    handleSubmit,
    watch,
    formState: { errors, isSubmitting },
  } = useForm({
    defaultValues: {
      barcazaId: carga?.id ? Number(carga.id) : "",
      tipo: carga?.tipo ?? "Barcaza",
      tonelaje: carga?.tonelaje ?? "",
    },
  });

  // Escuchamos el select de tipo en tiempo real
  const tipoSeleccionado = watch("tipo");
  const esBodega = tipoSeleccionado === "Bodega";

  const onSubmit = async (data) => {
    setServerError(null);

    // Validación manual de la mercadería
    if (!mercaderiaSeleccionada?.oracleId) {
      setServerError("Seleccione la naturaleza de la mercadería.");
      return;
    }

    const token = localStorage.getItem("mbpc_token");

    try {
      await axios.put(
        `/api/carga/${encodeURIComponent(carga.id)}`,
        {
          // Si es bodega forzamos 0, si es barcaza mandamos el número ingresado
          viajeId: carga.viajeId,
          barcazaId: esBodega ? 0 : Number(data.barcazaId),
          tipo: data.tipo,
          tonelaje: parseFloat(data.tonelaje),
          mercaderiaId: mercaderiaSeleccionada.oracleId,
        },
        {
          headers: {
            "Content-Type": "application/json",
            Authorization: `Bearer ${token}`,
          },
        }
      );

      onSuccess?.();
      onClose?.();
    } catch (err) {
      const mensaje =
        err.response?.data?.mensaje ??
        "Ocurrió un error al actualizar la carga. Intentá nuevamente.";
      setServerError(mensaje);
    }
  };

  return (
    /* ── Overlay ── */
    <div
      className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 backdrop-blur-sm"
      role="dialog"
      aria-modal="true"
      aria-labelledby="edit-modal-title"
    >
      {/* ── Panel ── */}
      <div className="w-full max-w-md rounded-md bg-white shadow-lg ring-1 ring-slate-200">
        {/* Header */}
        <div className="flex items-center justify-between border-b border-slate-200 px-6 py-4">
          <h2 id="edit-modal-title" className="text-base font-semibold text-slate-800">
            Editar Carga
          </h2>
          <button
            type="button"
            onClick={onClose}
            className="rounded p-1 text-slate-400 hover:bg-slate-100 hover:text-slate-600 transition-colors"
            aria-label="Cerrar modal"
          >
            <svg xmlns="http://www.w3.org/2000/svg" className="h-5 w-5" viewBox="0 0 20 20" fill="currentColor">
              <path fillRule="evenodd" d="M4.293 4.293a1 1 0 011.414 0L10 8.586l4.293-4.293a1 1 0 111.414 1.414L11.414 10l4.293 4.293a1 1 0 01-1.414 1.414L10 11.414l-4.293 4.293a1 1 0 01-1.414-1.414L8.586 10 4.293 5.707a1 1 0 010-1.414z" clipRule="evenodd" />
            </svg>
          </button>
        </div>

        {/* Body */}
        <form onSubmit={handleSubmit(onSubmit)} noValidate>
          <div className="space-y-5 px-6 py-5">
            {serverError && (
              <p className="rounded-md bg-red-50 px-4 py-2 text-sm font-medium text-red-600 ring-1 ring-red-200">
                {serverError}
              </p>
            )}

            {/* Tipo */}
            <div>
              <label htmlFor="tipo" className="mb-1 block text-sm font-medium text-slate-700">
                Tipo de Carga
              </label>
              <select
                id="tipo"
                className={`w-full rounded-md border px-3 py-2 text-sm shadow-sm focus:outline-none focus:ring-2 focus:ring-sky-500 transition bg-white ${errors.tipo ? "border-red-400 focus:ring-red-400" : "border-slate-300"
                  }`}
                {...register("tipo", { required: "El tipo de carga es requerido." })}
              >
                <option value="Barcaza">Barcaza</option>
                <option value="Bodega">Bodega</option>
              </select>
              {errors.tipo && <p className="mt-1 text-xs text-red-500">{errors.tipo.message}</p>}
            </div>

            {/* BarcazaId — Oculto con CSS en vez de desmontarlo para resetear la validación */}
            <div className={esBodega ? "hidden" : "block"}>
              <label htmlFor="barcazaId" className="mb-1 block text-sm font-medium text-slate-700">
                ID de Barcaza
              </label>
              <input
                id="barcazaId"
                type="number"
                step="1"
                className={`w-full rounded-md border px-3 py-2 text-sm shadow-sm placeholder-slate-400 focus:outline-none focus:ring-2 focus:ring-sky-500 transition ${errors.barcazaId ? "border-red-400 focus:ring-red-400" : "border-slate-300"
                  }`}
                placeholder="Ej: 1042"
                {...register("barcazaId", {
                  // Validación dinámica e inteligente
                  validate: (value, formValues) => {
                    if (formValues.tipo === "Bodega") return true; // Si es bodega, ignorar validación
                    if (!value) return "El ID de barcaza es requerido.";
                    if (Number(value) < 1) return "Debe ser un entero positivo.";
                    return true;
                  }
                })}
              />
              {errors.barcazaId && <p className="mt-1 text-xs text-red-500">{errors.barcazaId.message}</p>}
            </div>

            {/* Mercadería / Naturaleza */}
            <div className="relative z-50">
              <TipoCargaAutocomplete
                label="Mercadería / Naturaleza"
                value={mercaderiaSeleccionada?.oracleId || null}
                onChange={(val) => setMercaderiaSeleccionada(val)}
              />
            </div>

            {/* Tonelaje */}
            <div>
              <label htmlFor="tonelaje" className="mb-1 block text-sm font-medium text-slate-700">
                Tonelaje (Tn)
              </label>
              <input
                id="tonelaje"
                type="number"
                step="0.01"
                className={`w-full rounded-md border px-3 py-2 text-sm shadow-sm placeholder-slate-400 focus:outline-none focus:ring-2 focus:ring-sky-500 transition ${errors.tonelaje ? "border-red-400 focus:ring-red-400" : "border-slate-300"
                  }`}
                placeholder="Ej: 1250.50"
                {...register("tonelaje", {
                  required: "El tonelaje es requerido.",
                  min: { value: 0.01, message: "El tonelaje debe ser mayor a 0." },
                })}
              />
              {errors.tonelaje && <p className="mt-1 text-xs text-red-500">{errors.tonelaje.message}</p>}
            </div>
          </div>

          {/* Footer */}
          <div className="flex justify-end gap-3 border-t border-slate-200 px-6 py-4 bg-slate-50 rounded-b-md">
            <button
              type="button"
              onClick={onClose}
              disabled={isSubmitting}
              className="rounded-md px-4 py-2 text-sm font-medium text-slate-600 hover:bg-slate-200 transition-colors disabled:opacity-50"
            >
              Cancelar
            </button>
            <button
              type="submit"
              disabled={isSubmitting}
              className="flex items-center gap-2 rounded-md bg-sky-600 px-4 py-2 text-sm font-medium text-white hover:bg-sky-700 focus:outline-none focus:ring-2 focus:ring-sky-500 focus:ring-offset-2 transition-colors disabled:opacity-60"
            >
              {isSubmitting ? "Guardando..." : "Guardar cambios"}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}