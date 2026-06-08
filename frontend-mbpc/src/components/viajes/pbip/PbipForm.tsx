import { useEffect } from "react";
import { useForm } from "react-hook-form";
import { useActualizarDatosPbip } from "../../../hooks/viajes/useViajeComplementos";
import type { DatosPbip, ActualizarDatosPbipDto } from "../../../types/complementos.types";

interface PbipFormProps {
  viajeId: string;
  datosPbip: DatosPbip | null;
}

const NIVELES_PROTECCION = [
  { value: 1, label: "Nivel 1 — Normal" },
  { value: 2, label: "Nivel 2 — Heightened" },
  { value: 3, label: "Nivel 3 — Excepción" },
];

export default function PbipForm({ viajeId, datosPbip }: PbipFormProps) {
  const {
    register,
    handleSubmit,
    reset,
    formState: { errors, isDirty },
  } = useForm<ActualizarDatosPbipDto>({
    defaultValues: {
      contactoOcpm: datosPbip?.contactoOcpm ?? "",
      nroInmarsat: datosPbip?.nroInmarsat ?? "",
      arqueoBruto: datosPbip?.arqueoBruto ?? 0,
      nivelProteccion: datosPbip?.nivelProteccion ?? 1,
    },
  });

  const mutation = useActualizarDatosPbip(viajeId);

  useEffect(() => {
    if (!datosPbip) return;
    reset({
      contactoOcpm: datosPbip.contactoOcpm ?? "",
      nroInmarsat: datosPbip.nroInmarsat ?? "",
      arqueoBruto: datosPbip.arqueoBruto ?? 0,
      nivelProteccion: datosPbip.nivelProteccion ?? 1,
    });
  }, [datosPbip, reset]);

  const onSubmit = async (values: ActualizarDatosPbipDto) => {
    const payload = {
      ...values,
      nivelProteccion: Number(values.nivelProteccion),
      arqueoBruto: Number(values.arqueoBruto),
    };
    await mutation.mutateAsync(payload);
  };

  return (
    <form onSubmit={handleSubmit(onSubmit)} className="flex flex-col gap-4">
      <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
        <div className="flex flex-col gap-1">
          <label htmlFor="contactoOcpm" className="text-xs font-semibold text-slate-400 uppercase tracking-wider">
            Contacto OCPM
          </label>
          <input
            id="contactoOcpm"
            {...register("contactoOcpm", { required: "Campo obligatorio." })}
            className="bg-slate-900 border border-slate-700 rounded-lg px-3 py-2 text-sm text-slate-200 placeholder-slate-600 focus:outline-none focus:ring-1 focus:ring-emerald-500 w-full"
            placeholder="Ej: Cte. García / +54 11 0000-0000"
          />
          {errors.contactoOcpm && (
            <p className="text-red-400 text-xs mt-1">{errors.contactoOcpm.message}</p>
          )}
        </div>

        <div className="flex flex-col gap-1">
          <label htmlFor="nroInmarsat" className="text-xs font-semibold text-slate-400 uppercase tracking-wider">
            Nro. Inmarsat
          </label>
          <input
            id="nroInmarsat"
            {...register("nroInmarsat", { required: "Campo obligatorio." })}
            className="bg-slate-900 border border-slate-700 rounded-lg px-3 py-2 text-sm text-slate-200 placeholder-slate-600 focus:outline-none focus:ring-1 focus:ring-emerald-500 w-full"
            placeholder="Ej: 764XXXXXXX"
          />
          {errors.nroInmarsat && (
            <p className="text-red-400 text-xs mt-1">{errors.nroInmarsat.message}</p>
          )}
        </div>

        <div className="flex flex-col gap-1">
          <label htmlFor="arqueoBruto" className="text-xs font-semibold text-slate-400 uppercase tracking-wider">
            Arqueo Bruto (GT)
          </label>
          <input
            id="arqueoBruto"
            type="number"
            step="0.01"
            {...register("arqueoBruto", {
              required: "Campo obligatorio.",
              valueAsNumber: true,
              min: { value: 0, message: "Debe ser ≥ 0." },
            })}
            className="bg-slate-900 border border-slate-700 rounded-lg px-3 py-2 text-sm text-slate-200 placeholder-slate-600 focus:outline-none focus:ring-1 focus:ring-emerald-500 w-full"
          />
          {errors.arqueoBruto && (
            <p className="text-red-400 text-xs mt-1">{errors.arqueoBruto.message}</p>
          )}
        </div>

        <div className="flex flex-col gap-1">
          <label htmlFor="nivelProteccion" className="text-xs font-semibold text-slate-400 uppercase tracking-wider">
            Nivel de Protección
          </label>
          <select
            id="nivelProteccion"
            {...register("nivelProteccion", { valueAsNumber: true, required: true })}
            className="bg-slate-900 border border-slate-700 rounded-lg px-3 py-2 text-sm text-slate-300 focus:outline-none focus:ring-1 focus:ring-emerald-500 w-full"
          >
            {NIVELES_PROTECCION.map((n) => (
              <option key={n.value} value={n.value}>
                {n.label}
              </option>
            ))}
          </select>
        </div>
      </div>

      <div className="flex items-center gap-3 pt-2 border-t border-slate-700/50">
        <button
          type="submit"
          disabled={mutation.isPending || !isDirty}
          className="bg-emerald-600 hover:bg-emerald-500 disabled:opacity-50 disabled:cursor-not-allowed text-white text-sm font-semibold rounded-lg px-5 py-2 transition-colors flex items-center gap-2"
        >
          {mutation.isPending ? (
            <>
              <svg
                className="animate-spin h-3.5 w-3.5 text-white"
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
                  d="M4 12a8 8 0 018-8v4a4 4 0 00-4 4H4z"
                />
              </svg>
              Sincronizando…
            </>
          ) : (
            "Guardar datos PBIP"
          )}
        </button>
        {mutation.isSuccess && (
          <span className="text-emerald-400 text-xs">✓ Datos PBIP actualizados.</span>
        )}
        {mutation.isError && (
          <span className="text-red-400 text-xs">
            Error: {(mutation.error as Error).message}
          </span>
        )}
      </div>
    </form>
  );
}
