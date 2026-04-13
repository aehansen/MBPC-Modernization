// src/components/BotonesAccionViaje.tsx
// ──────────────────────────────────────────────────────────────────────────────
// Botones autónomos para las acciones de transición de estado de un viaje.
// Cada componente encapsula su hook, su lógica de visibilidad y su UI.
// Replica el patrón arquitectónico de BotonZarpar.tsx.
// ──────────────────────────────────────────────────────────────────────────────

import { useAmarrar, useFondear, useReanudar } from "@/hooks/useAccionesViaje";
import type { ViajeDto, NuevoViajeError } from "@/types/viajes.types";

// ─── Tipos compartidos ────────────────────────────────────────────────────────

type EstadoViaje = ViajeDto["estadoActual"];

interface BotonAccionProps {
  viaje: ViajeDto;
  onSuccess?: () => void;
  onError?: (error: NuevoViajeError) => void;
}

interface EstadoBoton {
  habilitado: boolean;
  tooltip: string | undefined;
  visible: boolean;
}

// ─── Helper de error inline ───────────────────────────────────────────────────

function MensajeError({ error }: { error: NuevoViajeError }) {
  return (
    <span
      role="alert"
      aria-live="assertive"
      className="mt-1 block text-xs text-red-600 dark:text-red-400"
    >
      {error.mensaje}
      {error.detail != null && (
        <span className="block text-xs text-red-500 dark:text-red-300">
          {error.detail}
        </span>
      )}
    </span>
  );
}

// ══════════════════════════════════════════════════════════════════════════════
// BOTON AMARRAR
// ══════════════════════════════════════════════════════════════════════════════

/**
 * Estados que habilitan el amarre.
 * Un buque puede amarrarse si está navegando o si reanudó tras un fondeo.
 */
const ESTADOS_HABILITADOS_AMARRAR: ReadonlySet<EstadoViaje> = new Set([
  "Navegando",
  "Reanudado",
]);

/** Estado en el que el botón es visible pero deshabilitado con tooltip. */
const ESTADO_YA_AMARRADO: EstadoViaje = "Amarrado";

const TOOLTIP_YA_AMARRADO = "El buque ya se encuentra amarrado.";

function resolverEstadoAmarrar(estadoActual: EstadoViaje): EstadoBoton {
  if (ESTADOS_HABILITADOS_AMARRAR.has(estadoActual)) {
    return { habilitado: true, tooltip: undefined, visible: true };
  }

  if (estadoActual === ESTADO_YA_AMARRADO) {
    return { habilitado: false, tooltip: TOOLTIP_YA_AMARRADO, visible: true };
  }

  // Cualquier otro estado (Fondeado, Zarpado, Cancelado, etc.): oculto
  return { habilitado: false, tooltip: undefined, visible: false };
}

export function BotonAmarrar({ viaje, onSuccess, onError }: BotonAccionProps) {
  const { mutate: amarrar, isPending, isError, error } = useAmarrar();

  const { habilitado, tooltip, visible } = resolverEstadoAmarrar(viaje.estadoActual);

  if (!visible) return null;

  const deshabilitado = !habilitado || isPending;

  function handleClick() {
    amarrar(
      { id: viaje.id },
      {
        onSuccess: () => onSuccess?.(),
        onError: (err) => onError?.(err),
      }
    );
  }

  return (
    <span title={tooltip} className="inline-flex flex-col">
      <button
        type="button"
        onClick={handleClick}
        disabled={deshabilitado}
        aria-busy={isPending}
        aria-label={
          isPending
            ? `Registrando amarre de ${viaje.buque}…`
            : `Amarrar ${viaje.buque}`
        }
        className={[
          "inline-flex items-center gap-1.5 rounded-md px-3 py-1.5 text-sm font-semibold",
          "transition-colors duration-150 focus-visible:outline focus-visible:outline-2",
          "focus-visible:outline-offset-2 focus-visible:outline-blue-500",
          deshabilitado
            ? "cursor-not-allowed bg-blue-200 text-blue-400 dark:bg-blue-900/40 dark:text-blue-600"
            : "cursor-pointer bg-blue-600 text-white hover:bg-blue-700 active:bg-blue-800 dark:bg-blue-500 dark:hover:bg-blue-600",
        ].join(" ")}
      >
        <span aria-hidden="true">{isPending ? "⏳" : "⚓"}</span>
        {isPending ? "Amarrando…" : "Amarrar"}
      </button>

      {isError && error != null && <MensajeError error={error} />}
    </span>
  );
}

// ══════════════════════════════════════════════════════════════════════════════
// BOTON FONDEAR
// ══════════════════════════════════════════════════════════════════════════════

/**
 * Estados que habilitan el fondeo.
 */
const ESTADOS_HABILITADOS_FONDEAR: ReadonlySet<EstadoViaje> = new Set([
  "Navegando",
  "Reanudado",
]);

const ESTADO_YA_FONDEADO: EstadoViaje = "Fondeado";

const TOOLTIP_YA_FONDEADO = "El buque ya se encuentra fondeado.";

function resolverEstadoFondear(estadoActual: EstadoViaje): EstadoBoton {
  if (ESTADOS_HABILITADOS_FONDEAR.has(estadoActual)) {
    return { habilitado: true, tooltip: undefined, visible: true };
  }

  if (estadoActual === ESTADO_YA_FONDEADO) {
    return { habilitado: false, tooltip: TOOLTIP_YA_FONDEADO, visible: true };
  }

  // Cualquier otro estado: oculto
  return { habilitado: false, tooltip: undefined, visible: false };
}

export function BotonFondear({ viaje, onSuccess, onError }: BotonAccionProps) {
  const { mutate: fondear, isPending, isError, error } = useFondear();

  const { habilitado, tooltip, visible } = resolverEstadoFondear(viaje.estadoActual);

  if (!visible) return null;

  const deshabilitado = !habilitado || isPending;

  function handleClick() {
    fondear(
      { id: viaje.id },
      {
        onSuccess: () => onSuccess?.(),
        onError: (err) => onError?.(err),
      }
    );
  }

  return (
    <span title={tooltip} className="inline-flex flex-col">
      <button
        type="button"
        onClick={handleClick}
        disabled={deshabilitado}
        aria-busy={isPending}
        aria-label={
          isPending
            ? `Registrando fondeo de ${viaje.buque}…`
            : `Fondear ${viaje.buque}`
        }
        className={[
          "inline-flex items-center gap-1.5 rounded-md px-3 py-1.5 text-sm font-semibold",
          "transition-colors duration-150 focus-visible:outline focus-visible:outline-2",
          "focus-visible:outline-offset-2 focus-visible:outline-amber-500",
          deshabilitado
            ? "cursor-not-allowed bg-amber-100 text-amber-400 dark:bg-amber-900/40 dark:text-amber-600"
            : "cursor-pointer bg-amber-500 text-white hover:bg-amber-600 active:bg-amber-700 dark:bg-amber-400 dark:text-amber-950 dark:hover:bg-amber-500",
        ].join(" ")}
      >
        <span aria-hidden="true">{isPending ? "⏳" : "⚓"}</span>
        {isPending ? "Fondeando…" : "Fondear"}
      </button>

      {isError && error != null && <MensajeError error={error} />}
    </span>
  );
}

// ══════════════════════════════════════════════════════════════════════════════
// BOTON REANUDAR
// ══════════════════════════════════════════════════════════════════════════════

/**
 * El botón "Reanudar" es visible y habilitado ÚNICAMENTE desde estado "Fondeado".
 * Cualquier otro estado lo oculta por completo (no hay caso "visible + deshabilitado").
 */
const ESTADO_REANUDAR_HABILITADO: EstadoViaje = "Fondeado";

function resolverEstadoReanudar(estadoActual: EstadoViaje): EstadoBoton {
  if (estadoActual === ESTADO_REANUDAR_HABILITADO) {
    return { habilitado: true, tooltip: undefined, visible: true };
  }

  return { habilitado: false, tooltip: undefined, visible: false };
}

export function BotonReanudar({ viaje, onSuccess, onError }: BotonAccionProps) {
  const { mutate: reanudar, isPending, isError, error } = useReanudar();

  const { habilitado, tooltip, visible } = resolverEstadoReanudar(viaje.estadoActual);

  if (!visible) return null;

  const deshabilitado = !habilitado || isPending;

  function handleClick() {
    reanudar(
      { id: viaje.id },
      {
        onSuccess: () => onSuccess?.(),
        onError: (err) => onError?.(err),
      }
    );
  }

  return (
    <span title={tooltip} className="inline-flex flex-col">
      <button
        type="button"
        onClick={handleClick}
        disabled={deshabilitado}
        aria-busy={isPending}
        aria-label={
          isPending
            ? `Reanudando viaje de ${viaje.buque}…`
            : `Reanudar viaje de ${viaje.buque}`
        }
        className={[
          "inline-flex items-center gap-1.5 rounded-md px-3 py-1.5 text-sm font-semibold",
          "transition-colors duration-150 focus-visible:outline focus-visible:outline-2",
          "focus-visible:outline-offset-2 focus-visible:outline-slate-400",
          deshabilitado
            ? "cursor-not-allowed bg-slate-100 text-slate-400 dark:bg-slate-800 dark:text-slate-600"
            : "cursor-pointer bg-slate-600 text-white hover:bg-slate-700 active:bg-slate-800 dark:bg-slate-500 dark:hover:bg-slate-600",
        ].join(" ")}
      >
        <span aria-hidden="true">{isPending ? "⏳" : "▶"}</span>
        {isPending ? "Reanudando…" : "Reanudar"}
      </button>

      {isError && error != null && <MensajeError error={error} />}
    </span>
  );
}
