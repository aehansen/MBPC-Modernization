// src/components/viajes/ModalActualizarPosicion.tsx
// Modal para reportar posición geográfica de un viaje activo (PUT /viajes/:id/posicion).

import { useEffect, useState } from "react";
import { useForm, type SubmitHandler } from "react-hook-form";
import { useActualizarPosicion } from "@/hooks/useActualizarPosicion";

export interface ModalActualizarPosicionProps {
  isOpen: boolean;
  onClose: () => void;
  viajeId: string;
  nombreBuque?: string;
}

interface FormValues {
  latitud: string;
  longitud: string;
  fechaReporteLocal: string;
}

function getDefaultFechaLocal(): string {
  const d = new Date();
  const pad = (n: number) => String(n).padStart(2, "0");
  const offset = d.getTimezoneOffset() * 60_000;
  const local = new Date(d.getTime() - offset);
  return local.toISOString().slice(0, 16);
}

function extractApiErrorMessage(error: unknown, fallback: string): string {
  const ax = error as { response?: { data?: unknown }; message?: string };
  const data = ax?.response?.data;
  if (typeof data === "string" && data.trim()) return data.trim();
  if (data && typeof data === "object") {
    const o = data as Record<string, unknown>;
    if (o.Error != null && String(o.Error).trim()) return String(o.Error);
    if (o.error != null && String(o.error).trim()) return String(o.error);
    if (o.mensaje != null && String(o.mensaje).trim()) return String(o.mensaje);
    if (o.message != null && String(o.message).trim()) return String(o.message);
    if (o.title != null && String(o.title).trim()) return String(o.title);
    if (o.detail != null && String(o.detail).trim()) return String(o.detail);
  }
  if (ax?.message && typeof ax.message === "string" && ax.message.trim()) {
    return ax.message;
  }
  return fallback;
}

function localDateTimeToIsoUtc(local: string): string {
  return new Date(local).toISOString();
}

export default function ModalActualizarPosicion({
  isOpen,
  onClose,
  viajeId,
  nombreBuque,
}: ModalActualizarPosicionProps) {
  const { mutate, isPending, reset: resetMutation } = useActualizarPosicion(viajeId);
  const [submitError, setSubmitError] = useState<string | null>(null);

  const {
    register,
    handleSubmit,
    reset,
    formState: { errors },
  } = useForm<FormValues>({
    defaultValues: {
      latitud: "",
      longitud: "",
      fechaReporteLocal: getDefaultFechaLocal(),
    },
  });

  useEffect(() => {
    if (!isOpen) return;
    reset({
      latitud: "",
      longitud: "",
      fechaReporteLocal: getDefaultFechaLocal(),
    });
    setSubmitError(null);
    resetMutation();
  }, [isOpen, viajeId, reset, resetMutation]);

  useEffect(() => {
    if (!isOpen) return;
    const onKeyDown = (e: KeyboardEvent) => {
      if (e.key === "Escape" && !isPending) onClose();
    };
    document.addEventListener("keydown", onKeyDown);
    return () => document.removeEventListener("keydown", onKeyDown);
  }, [isOpen, isPending, onClose]);

  const onSubmit: SubmitHandler<FormValues> = (values) => {
    setSubmitError(null);
    const lat = Number(String(values.latitud).replace(",", "."));
    const lon = Number(String(values.longitud).replace(",", "."));
    if (Number.isNaN(lat) || Number.isNaN(lon)) {
      setSubmitError("Latitud y longitud deben ser números válidos.");
      return;
    }
    mutate(
      {
        latitud: lat,
        longitud: lon,
        fechaReporte: localDateTimeToIsoUtc(values.fechaReporteLocal),
      },
      {
        onSuccess: () => {
          onClose();
        },
        onError: (err) => {
          setSubmitError(extractApiErrorMessage(err, "No se pudo actualizar la posición."));
        },
      }
    );
  };

  if (!isOpen) return null;
  if (!viajeId?.trim()) return null;

  return (
    <div
      className="fixed inset-0 z-50 flex items-center justify-center p-4"
      role="dialog"
      aria-modal="true"
      aria-labelledby="modal-actualizar-posicion-title"
    >
      <div
        className="absolute inset-0 bg-slate-950/80 backdrop-blur-sm z-0"
        onClick={!isPending ? onClose : undefined}
        aria-hidden="true"
      />

      <div className="relative z-10 w-full max-w-lg max-h-[92vh] flex flex-col bg-slate-900 border border-slate-700/60 rounded-lg shadow-2xl shadow-black/60">
        <div className="h-1 w-full bg-gradient-to-r from-cyan-500 via-sky-400 to-cyan-600 shrink-0 rounded-t-lg" />

        <div className="px-6 py-4 border-b border-slate-700/60 flex items-center justify-between shrink-0">
          <div>
            <h2
              id="modal-actualizar-posicion-title"
              className="text-base font-bold text-slate-100 leading-tight"
            >
              Actualizar posición
            </h2>
            {nombreBuque ? (
              <p className="text-xs text-slate-400 mt-1">
                Viaje <span className="font-mono text-slate-300">{viajeId}</span> — {nombreBuque}
              </p>
            ) : (
              <p className="text-xs text-slate-400 mt-1 font-mono">{viajeId}</p>
            )}
          </div>
          <button
            type="button"
            onClick={onClose}
            disabled={isPending}
            aria-label="Cerrar modal"
            className="text-slate-500 hover:text-slate-200 disabled:opacity-40 rounded p-1 focus:outline-none focus:ring-2 focus:ring-cyan-500/50"
          >
            <span className="text-2xl leading-none">&times;</span>
          </button>
        </div>

        <form
          onSubmit={handleSubmit(onSubmit)}
          className="overflow-y-auto flex-1 px-6 py-5 space-y-4"
          noValidate
        >
          {submitError && (
            <div className="rounded-md border border-red-500/50 bg-red-900/30 px-3 py-2 text-sm text-red-200">
              {submitError}
            </div>
          )}

          <div>
            <label htmlFor="pos-latitud" className="block text-xs font-semibold uppercase tracking-wide text-slate-400 mb-1">
              Latitud (decimal, ej. -34.6037)
            </label>
            <input
              id="pos-latitud"
              type="text"
              inputMode="decimal"
              disabled={isPending}
              className="w-full bg-slate-800/60 border border-slate-600/50 text-slate-100 text-sm rounded px-3 py-2 focus:outline-none focus:border-cyan-500 focus:ring-1 focus:ring-cyan-500/40 disabled:opacity-50"
              {...register("latitud", { required: "Ingrese la latitud" })}
            />
            {errors.latitud && (
              <p className="mt-1 text-xs text-red-400">{errors.latitud.message}</p>
            )}
          </div>

          <div>
            <label htmlFor="pos-longitud" className="block text-xs font-semibold uppercase tracking-wide text-slate-400 mb-1">
              Longitud (decimal, ej. -58.3816)
            </label>
            <input
              id="pos-longitud"
              type="text"
              inputMode="decimal"
              disabled={isPending}
              className="w-full bg-slate-800/60 border border-slate-600/50 text-slate-100 text-sm rounded px-3 py-2 focus:outline-none focus:border-cyan-500 focus:ring-1 focus:ring-cyan-500/40 disabled:opacity-50"
              {...register("longitud", { required: "Ingrese la longitud" })}
            />
            {errors.longitud && (
              <p className="mt-1 text-xs text-red-400">{errors.longitud.message}</p>
            )}
          </div>

          <div>
            <label htmlFor="pos-fecha" className="block text-xs font-semibold uppercase tracking-wide text-slate-400 mb-1">
              Fecha y hora del reporte
            </label>
            <input
              id="pos-fecha"
              type="datetime-local"
              disabled={isPending}
              className="w-full bg-slate-800/60 border border-slate-600/50 text-slate-100 text-sm rounded px-3 py-2 focus:outline-none focus:border-cyan-500 focus:ring-1 focus:ring-cyan-500/40 disabled:opacity-50"
              {...register("fechaReporteLocal", { required: "Seleccione fecha y hora" })}
            />
            {errors.fechaReporteLocal && (
              <p className="mt-1 text-xs text-red-400">{errors.fechaReporteLocal.message}</p>
            )}
          </div>

          <div className="flex justify-end gap-3 pt-2">
            <button
              type="button"
              onClick={onClose}
              disabled={isPending}
              className="px-4 py-2 text-sm font-medium text-slate-400 hover:text-slate-200 border border-slate-600/50 rounded disabled:opacity-40"
            >
              Cancelar
            </button>
            <button
              type="submit"
              disabled={isPending}
              className="px-5 py-2 text-sm font-semibold rounded bg-cyan-600 hover:bg-cyan-500 text-white disabled:opacity-50 disabled:cursor-not-allowed"
            >
              {isPending ? "Guardando…" : "Guardar posición"}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
