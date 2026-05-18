// src/components/viajes/ModalNuevoViaje.tsx
// ──────────────────────────────────────────────────────────────────────────────
// Modal para registrar un nuevo viaje en el sistema MBPC.
// Usa react-hook-form para manejo del formulario y validaciones,
// Tailwind CSS para estilos, y el hook useNuevoViaje para la mutación.
// ──────────────────────────────────────────────────────────────────────────────

import { useEffect } from "react";
import { useForm, type SubmitHandler } from "react-hook-form";
import { useNuevoViaje } from "@/hooks/useNuevoViaje";
import EmbarcacionSelect from "@/components/viajes/EmbarcacionSelect";
import {
  DeclaracionMalvinasEnum,
  DECLARACION_MALVINAS_LABELS,
  type NuevoViajeFormValues,
  type NuevoViajeRequest,
  type NuevoViajeResponse,
  type NuevoViajeError,
} from "@/types/viajes.types";

// ─── Props ────────────────────────────────────────────────────────────────────

interface ModalNuevoViajeProps {
  /** Controla la visibilidad del modal */
  isOpen: boolean;
  /** Callback para cerrar el modal (desde botón cancelar, overlay, o éxito) */
  onClose: () => void;
  /**
   * Callback opcional para notificar éxito al componente padre.
   * Recibe el objeto completo NuevoViajeResponse para que el padre
   * pueda mostrar el mensaje descriptivo que viene del servidor.
   */
  onSuccess?: (response: NuevoViajeResponse) => void;
  /**
   * Callback opcional para notificar error al componente padre.
   */
  onError?: (error: NuevoViajeError) => void;
}

// ─── Helpers ──────────────────────────────────────────────────────────────────

/**
 * Convierte un string datetime-local ("YYYY-MM-DDTHH:mm") a ISO 8601 UTC.
 * El input datetime-local devuelve hora local del navegador; la convertimos
 * explícitamente a UTC antes de enviar al servidor.
 */
function toIsoUtcString(localDateTimeString: string): string {
  return new Date(localDateTimeString).toISOString();
}

/**
 * Devuelve el valor mínimo para datetime-local (ahora mismo).
 * Se llama en tiempo de validación (no de render) para comparar
 * contra el instante real del submit.
 */
function getNowLocalMin(): string {
  const now = new Date();
  const offset = now.getTimezoneOffset() * 60_000;
  return new Date(now.getTime() - offset).toISOString().slice(0, 16);
}

// ─── Subcomponentes internos ──────────────────────────────────────────────────

interface FieldErrorProps {
  message?: string;
}

function FieldError({ message }: FieldErrorProps) {
  if (!message) return null;
  return (
    <p className="mt-1 text-xs text-red-400 flex items-center gap-1">
      <span aria-hidden="true">⚠</span>
      {message}
    </p>
  );
}

interface LabelProps {
  htmlFor: string;
  children: React.ReactNode;
  required?: boolean;
}

function Label({ htmlFor, children, required = false }: LabelProps) {
  return (
    <label
      htmlFor={htmlFor}
      className="block text-xs font-semibold tracking-widest uppercase text-slate-400 mb-1"
    >
      {children}
      {required && (
        <span className="text-cyan-400 ml-1" aria-label="requerido">
          *
        </span>
      )}
    </label>
  );
}

// Clases base reutilizables para inputs y selects
const inputClass =
  "w-full bg-slate-800/60 border border-slate-600/50 text-slate-100 text-sm " +
  "rounded px-3 py-2 placeholder-slate-500 " +
  "focus:outline-none focus:border-cyan-500 focus:ring-1 focus:ring-cyan-500/40 " +
  "disabled:opacity-50 disabled:cursor-not-allowed " +
  "transition-colors duration-150";

const errorInputClass =
  "w-full bg-slate-800/60 border border-red-500/60 text-slate-100 text-sm " +
  "rounded px-3 py-2 placeholder-slate-500 " +
  "focus:outline-none focus:border-red-400 focus:ring-1 focus:ring-red-400/40 " +
  "disabled:opacity-50 transition-colors duration-150";

function getInputClass(hasError: boolean): string {
  return hasError ? errorInputClass : inputClass;
}

// ─── Componente principal ─────────────────────────────────────────────────────

export function ModalNuevoViaje({
  isOpen,
  onClose,
  onSuccess,
  onError,
}: ModalNuevoViajeProps) {
  const {
    register,
    handleSubmit,
    reset,
    watch,
    setValue,
    formState: { errors },
  } = useForm<NuevoViajeFormValues>({
    defaultValues: {
      buqueId: undefined as unknown as number,
      NombreBuque: "",
      origen: "",
      destino: "",
      proximoPuntoControl: "",
      fechaPartida: "",
      eta: "",
      declaracionMalvinas: DeclaracionMalvinasEnum.NoVieneDeMalvinas_L,
      muelleSalida: "",
      agenciaMaritima: "",
      motivoViaje: "",
      zoe: "",
      posicion: "",
      rioCanalKmPar: undefined,
    },
  });

  const { mutate, isPending } = useNuevoViaje();

  // Resetear el formulario cuando el modal se cierra
  useEffect(() => {
    if (!isOpen) {
      reset();
    }
  }, [isOpen, reset]);

  // Cerrar con Escape
  useEffect(() => {
    if (!isOpen) return;
    const handleKeyDown = (e: KeyboardEvent) => {
      if (e.key === "Escape" && !isPending) onClose();
    };
    document.addEventListener("keydown", handleKeyDown);
    return () => document.removeEventListener("keydown", handleKeyDown);
  }, [isOpen, isPending, onClose]);

  // Leer FechaPartida para validar que ETA sea posterior
  const fechaPartidaValue = watch("fechaPartida");

  const onSubmit: SubmitHandler<NuevoViajeFormValues> = (formValues) => {
    const payload: NuevoViajeRequest = {
      buqueId: Number(formValues.buqueId),
      nombreBuque: formValues.NombreBuque || undefined,
      origen: formValues.origen,
      destino: formValues.destino,
      proximoPuntoControl: formValues.proximoPuntoControl,
      fechaPartida: toIsoUtcString(formValues.fechaPartida),
      eta: toIsoUtcString(formValues.eta),
      declaracionMalvinas: formValues.declaracionMalvinas,
      muelleSalida: formValues.muelleSalida || undefined,
      agenciaMaritima: formValues.agenciaMaritima || undefined,
      motivoViaje: formValues.motivoViaje || undefined,
      zoe: formValues.zoe || undefined,
      posicion: formValues.posicion || undefined,
      rioCanalKmPar:
        formValues.rioCanalKmPar != null &&
        formValues.rioCanalKmPar !== (undefined as unknown as number)
          ? Number(formValues.rioCanalKmPar)
          : undefined,
    };

    mutate(payload, {
      onSuccess: (response) => {
        onSuccess?.(response);
        onClose();
      },
      onError: (error) => {
        onError?.(error);
      },
    });
  };

  if (!isOpen) return null;

  return (
    <div
      className="fixed inset-0 z-50 flex items-center justify-center p-4"
      role="dialog"
      aria-modal="true"
      aria-labelledby="modal-nuevo-viaje-title"
      // 🔥 Atajo CTRL+S / CMD+S capturado como Evento Sintético de React.
      onKeyDown={(e) => {
        const isS = e.key === "s" || e.key === "S" || e.nativeEvent.code === "KeyS";
        const isModifier = e.ctrlKey || e.metaKey;

        if (isModifier && isS) {
          e.preventDefault();
          e.stopPropagation();

          if (!isPending) {
            handleSubmit(onSubmit)();
          }
        }
      }}
    >
      {/* Backdrop */}
      <div
        className="absolute inset-0 bg-slate-950/80 backdrop-blur-sm"
        onClick={!isPending ? onClose : undefined}
        aria-hidden="true"
      />

      {/* ── Panel del modal ─────────────────────────────────────────── */}
      {/*
        FIX: Se eliminó `overflow-hidden` del div principal del panel.
        Ese overflow creaba un contexto de apilamiento que recortaba
        físicamente el dropdown absoluto, haciéndolo invisible.
        El scroll se controla en el <form> interno con overflow-y-auto,
        y el alto máximo del panel se mantiene con max-h-[92vh].
      */}
      <div className="relative z-10 w-full max-w-3xl max-h-[92vh] flex flex-col bg-slate-900 border border-slate-700/60 rounded-lg shadow-2xl shadow-black/60">
        {/* Franja de color superior */}
        <div className="h-1 w-full bg-gradient-to-r from-cyan-500 via-sky-400 to-cyan-600 shrink-0 rounded-t-lg" />

        {/* ── Header ──────────────────────────────────────────────────── */}
        <div className="px-6 py-4 border-b border-slate-700/60 flex items-center justify-between shrink-0">
          <div className="flex items-center gap-3">
            <span className="text-2xl" aria-hidden="true">
              ⚓
            </span>
            <div>
              <h2
                id="modal-nuevo-viaje-title"
                className="text-base font-bold text-slate-100 leading-tight"
              >
                Registrar Nuevo Viaje
              </h2>
              <p className="text-xs text-slate-400 mt-0.5">
                Completá los datos del buque y la ruta para iniciar el viaje.
              </p>
            </div>
          </div>
          <button
            type="button"
            onClick={onClose}
            disabled={isPending}
            aria-label="Cerrar modal"
            className="text-slate-500 hover:text-slate-200 disabled:opacity-40 
                       disabled:cursor-not-allowed transition-colors duration-150 
                       rounded p-1 focus:outline-none focus:ring-2 focus:ring-cyan-500/50"
          >
            <svg
              className="w-5 h-5"
              fill="none"
              stroke="currentColor"
              viewBox="0 0 24 24"
              aria-hidden="true"
            >
              <path
                strokeLinecap="round"
                strokeLinejoin="round"
                strokeWidth="2"
                d="M6 18L18 6M6 6l12 12"
              />
            </svg>
          </button>
        </div>

        {/* ── Cuerpo scrollable ────────────────────────────────────────── */}
        <form
          id="form-nuevo-viaje"
          onSubmit={handleSubmit(onSubmit)}
          noValidate
          className="overflow-y-auto flex-1 px-6 py-5 space-y-6 scrollbar-thin scrollbar-track-slate-800 scrollbar-thumb-slate-600"
        >
          {/* ── SECCIÓN 1: Identificación del Buque ─────────────────── */}
          <section>
            <h3 className="text-xs font-bold tracking-widest uppercase text-cyan-500 mb-3 flex items-center gap-2">
              <span className="block w-4 h-px bg-cyan-500/50" aria-hidden="true" />
              Identificación del Buque
            </h3>

            <div className="grid grid-cols-1 sm:grid-cols-2 gap-x-4 gap-y-4">

              {/* ── Autocomplete de Buque (Hito 10.3) ── */}
              <div className="sm:col-span-2">
                <Label htmlFor="emb-busqueda" required>
                  Buque
                </Label>
                {/*
                  Campo oculto registrado en RHF para que la validación de buqueId
                  funcione correctamente. EmbarcacionSelect llama a setValue al
                  seleccionar, lo que actualiza este campo y dispara shouldValidate.
                */}
                <input
                  type="hidden"
                  {...register("buqueId", {
                    required: "Debe seleccionar un buque de la lista.",
                  })}
                />
                <EmbarcacionSelect
                  onSelect={(b) => {
                    setValue("buqueId", b.idBuque, { shouldValidate: true });
                    setValue("NombreBuque", b.nombre);
                  }}
                  error={errors.buqueId?.message}
                  disabled={isPending}
                  allowedTipos={["buque", "remolcador"]}
                />
              </div>

              {/* Agencia Marítima */}
              <div>
                <Label htmlFor="agenciaMaritima">Agencia Marítima</Label>
                <input
                  id="agenciaMaritima"
                  type="text"
                  placeholder="Ej: Agencia del Plata S.A."
                  disabled={isPending}
                  className={getInputClass(!!errors.agenciaMaritima)}
                  {...register("agenciaMaritima", {
                    maxLength: {
                      value: 200,
                      message: "Máximo 200 caracteres.",
                    },
                  })}
                />
                <FieldError message={errors.agenciaMaritima?.message} />
              </div>

              {/* Muelle de Salida */}
              <div>
                <Label htmlFor="muelleSalida">Muelle de Salida</Label>
                <input
                  id="muelleSalida"
                  type="text"
                  placeholder="Ej: Muelle 5 - Puerto Nuevo"
                  disabled={isPending}
                  className={getInputClass(!!errors.muelleSalida)}
                  {...register("muelleSalida", {
                    maxLength: {
                      value: 200,
                      message: "Máximo 200 caracteres.",
                    },
                  })}
                />
                <FieldError message={errors.muelleSalida?.message} />
              </div>

              {/* Motivo del Viaje */}
              <div className="sm:col-span-2">
                <Label htmlFor="motivoViaje">Motivo del Viaje</Label>
                <input
                  id="motivoViaje"
                  type="text"
                  placeholder="Ej: Transporte de carga general"
                  disabled={isPending}
                  className={getInputClass(!!errors.motivoViaje)}
                  {...register("motivoViaje", {
                    maxLength: {
                      value: 500,
                      message: "Máximo 500 caracteres.",
                    },
                  })}
                />
                <FieldError message={errors.motivoViaje?.message} />
              </div>
            </div>
          </section>

          {/* ── SECCIÓN 2: Ruta y Tiempos ───────────────────────────── */}
          <section>
            <h3 className="text-xs font-bold tracking-widest uppercase text-cyan-500 mb-3 flex items-center gap-2">
              <span className="block w-4 h-px bg-cyan-500/50" aria-hidden="true" />
              Ruta y Tiempos
            </h3>

            <div className="grid grid-cols-1 sm:grid-cols-2 gap-x-4 gap-y-4">
              {/* Origen */}
              <div>
                <Label htmlFor="origen" required>
                  Origen
                </Label>
                <input
                  id="origen"
                  type="text"
                  placeholder="Ej: Buenos Aires"
                  disabled={isPending}
                  className={getInputClass(!!errors.origen)}
                  {...register("origen", {
                    required: "El origen es requerido.",
                    minLength: { value: 2, message: "Mínimo 2 caracteres." },
                    maxLength: {
                      value: 200,
                      message: "Máximo 200 caracteres.",
                    },
                  })}
                />
                <FieldError message={errors.origen?.message} />
              </div>

              {/* Destino */}
              <div>
                <Label htmlFor="destino" required>
                  Destino
                </Label>
                <input
                  id="destino"
                  type="text"
                  placeholder="Ej: Montevideo"
                  disabled={isPending}
                  className={getInputClass(!!errors.destino)}
                  {...register("destino", {
                    required: "El destino es requerido.",
                    minLength: { value: 2, message: "Mínimo 2 caracteres." },
                    maxLength: {
                      value: 200,
                      message: "Máximo 200 caracteres.",
                    },
                  })}
                />
                <FieldError message={errors.destino?.message} />
              </div>

              {/* Próximo Punto de Control */}
              <div className="sm:col-span-2">
                <Label htmlFor="proximoPuntoControl" required>
                  Próximo Punto de Control
                </Label>
                <input
                  id="proximoPuntoControl"
                  type="text"
                  placeholder="Ej: Prefectura Zárate"
                  disabled={isPending}
                  className={getInputClass(!!errors.proximoPuntoControl)}
                  {...register("proximoPuntoControl", {
                    required: "El próximo punto de control es requerido.",
                    minLength: { value: 2, message: "Mínimo 2 caracteres." },
                    maxLength: {
                      value: 200,
                      message: "Máximo 200 caracteres.",
                    },
                  })}
                />
                <FieldError message={errors.proximoPuntoControl?.message} />
              </div>

              {/* Fecha de Partida — FIX Hito 8.0: se eliminó min={nowMin};
                  la validación de fecha pasada se hace vía react-hook-form
                  para evitar el bug del desplegable de horas nativo. */}
              <div>
                <Label htmlFor="fechaPartida" required>
                  Fecha y Hora de Partida
                </Label>
                <input
                  id="fechaPartida"
                  type="datetime-local"
                  disabled={isPending}
                  className={getInputClass(!!errors.fechaPartida)}
                  {...register("fechaPartida", {
                    required: "La fecha de partida es requerida.",
                    validate: (value) =>
                      new Date(value) >= new Date(getNowLocalMin()) ||
                      "La fecha de partida no puede ser en el pasado.",
                  })}
                />
                <FieldError message={errors.fechaPartida?.message} />
              </div>

              {/* ETA — FIX Hito 8.0: se eliminó min={fechaPartidaValue || nowMin};
                  la validación de orden temporal se mantiene vía react-hook-form. */}
              <div>
                <Label htmlFor="eta" required>
                  ETA (Arribo Estimado)
                </Label>
                <input
                  id="eta"
                  type="datetime-local"
                  disabled={isPending}
                  className={getInputClass(!!errors.eta)}
                  {...register("eta", {
                    required: "La ETA es requerida.",
                    validate: (value) => {
                      if (!fechaPartidaValue) return true;
                      return (
                        new Date(value) > new Date(fechaPartidaValue) ||
                        "La ETA debe ser posterior a la fecha de partida."
                      );
                    },
                  })}
                />
                <FieldError message={errors.eta?.message} />
              </div>
            </div>
          </section>

          {/* ── SECCIÓN 3: Posición y Datos Geográficos ─────────── */}
          <section>
            <h3 className="text-xs font-bold tracking-widest uppercase text-cyan-500 mb-3 flex items-center gap-2">
              <span className="block w-4 h-px bg-cyan-500/50" aria-hidden="true" />
              Posición y Datos Geográficos
            </h3>

            <div className="grid grid-cols-1 sm:grid-cols-2 gap-x-4 gap-y-4">
              {/* Posición */}
              <div>
                <Label htmlFor="posicion">Posición Inicial</Label>
                <input
                  id="posicion"
                  type="text"
                  placeholder="Ej: 34°36'S 058°22'W"
                  disabled={isPending}
                  className={getInputClass(!!errors.posicion)}
                  {...register("posicion", {
                    maxLength: {
                      value: 200,
                      message: "Máximo 200 caracteres.",
                    },
                  })}
                />
                <FieldError message={errors.posicion?.message} />
              </div>

              {/* Km Par */}
              <div>
                <Label htmlFor="rioCanalKmPar">Río/Canal — Km Par</Label>
                <input
                  id="rioCanalKmPar"
                  type="number"
                  step="0.1"
                  min="0"
                  max="9999.9"
                  placeholder="Ej: 274.5"
                  disabled={isPending}
                  className={getInputClass(!!errors.rioCanalKmPar)}
                  {...register("rioCanalKmPar", {
                    min: { value: 0, message: "Debe ser un valor positivo." },
                    max: { value: 9999.9, message: "Máximo 9999.9 km." },
                    valueAsNumber: true,
                  })}
                />
                <FieldError message={errors.rioCanalKmPar?.message} />
              </div>

              {/* ZOE */}
              <div>
                <Label htmlFor="zoe">ZOE (Zona Operación Especial)</Label>
                <input
                  id="zoe"
                  type="text"
                  placeholder="Ej: ZR-Paraná"
                  disabled={isPending}
                  className={getInputClass(!!errors.zoe)}
                  {...register("zoe", {
                    maxLength: {
                      value: 100,
                      message: "Máximo 100 caracteres.",
                    },
                  })}
                />
                <FieldError message={errors.zoe?.message} />
              </div>
            </div>
          </section>

          {/* ── SECCIÓN 4: Declaración Jurada Malvinas ──────────── */}
          <section>
            <h3 className="text-xs font-bold tracking-widest uppercase text-cyan-500 mb-3 flex items-center gap-2">
              <span className="block w-4 h-px bg-cyan-500/50" aria-hidden="true" />
              Declaración Jurada Malvinas
            </h3>

            <div>
              <Label htmlFor="declaracionMalvinas" required>
                Código de Declaración
              </Label>
              <select
                id="declaracionMalvinas"
                disabled={isPending}
                className={getInputClass(!!errors.declaracionMalvinas)}
                {...register("declaracionMalvinas", {
                  required: "La declaración de Malvinas es requerida.",
                })}
              >
                {(
                  Object.values(
                    DeclaracionMalvinasEnum
                  ) as DeclaracionMalvinasEnum[]
                ).map((value) => (
                  <option key={value} value={value}>
                    {DECLARACION_MALVINAS_LABELS[value]}
                  </option>
                ))}
              </select>
              <FieldError message={errors.declaracionMalvinas?.message} />
            </div>
          </section>
        </form>

        {/* ── Footer con acciones ──────────────────────────────────── */}
        <div className="px-6 py-4 border-t border-slate-700/60 bg-slate-900/80 flex items-center justify-end gap-3 shrink-0">
          <button
            type="button"
            onClick={onClose}
            disabled={isPending}
            className="px-4 py-2 text-sm font-medium text-slate-400 hover:text-slate-200 
                       border border-slate-600/50 hover:border-slate-500 rounded 
                       disabled:opacity-40 disabled:cursor-not-allowed
                       transition-colors duration-150"
          >
            Cancelar
          </button>

          <button
            type="submit"
            form="form-nuevo-viaje"
            disabled={isPending}
            className="px-5 py-2 text-sm font-semibold rounded
                       bg-cyan-600 hover:bg-cyan-500 text-white
                       disabled:bg-cyan-900 disabled:text-cyan-600 disabled:cursor-not-allowed
                       flex items-center gap-2 transition-colors duration-150
                       focus:outline-none focus:ring-2 focus:ring-cyan-500/50"
          >
            {isPending ? (
              <>
                <svg
                  className="animate-spin w-4 h-4 text-cyan-400"
                  xmlns="http://www.w3.org/2000/svg"
                  fill="none"
                  viewBox="0 0 24 24"
                  aria-hidden="true"
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
                <span>Guardando...</span>
              </>
            ) : (
              <>
                <span aria-hidden="true">⚓</span>
                <span>Registrar Viaje</span>
              </>
            )}
          </button>
        </div>
      </div>
    </div>
  );
}

export default ModalNuevoViaje;
