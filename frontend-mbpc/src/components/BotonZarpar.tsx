// src/components/BotonZarpar.tsx
// ──────────────────────────────────────────────────────────────────────────────
// Botón funcional para ejecutar la acción "Zarpar" sobre un viaje.
// Consume useZarpar y aplica las reglas de negocio del dominio marítimo.
// ──────────────────────────────────────────────────────────────────────────────

import { useZarpar } from "@/hooks/useZarpar";
import type { ViajeDto } from "@/types/viajes.types";
import type { NuevoViajeError } from "@/types/viajes.types";

// ─── Tipos ────────────────────────────────────────────────────────────────────

type EstadoViaje = ViajeDto["estadoActual"];

// ─── Constantes de dominio ────────────────────────────────────────────────────

/** Estados que habilitan el zarpe según reglas de negocio MBPC. */
const ESTADOS_HABILITADOS_PARA_ZARPAR: ReadonlySet<EstadoViaje> = new Set([
  "Amarrado",
  "Reanudado",
]);

/** Estado que bloquea el zarpe con un mensaje específico al usuario. */
const ESTADO_FONDEADO: EstadoViaje = "Fondeado";

const TOOLTIP_FONDEADO =
  "El buque se encuentra fondeado. Debe reanudar el viaje antes de zarpar.";

// ─── Props ────────────────────────────────────────────────────────────────────

interface BotonZarparProps {
  viaje: ViajeDto;
  /** Callback opcional invocado tras un zarpe exitoso. */
  onSuccess?: () => void;
  /** Callback opcional invocado si la mutación falla. */
  onError?: (error: NuevoViajeError) => void;
}

// ─── Helpers ──────────────────────────────────────────────────────────────────

function resolverEstadoBoton(estadoActual: EstadoViaje): {
  habilitado: boolean;
  tooltip: string | undefined;
  visible: boolean;
} {
  if (ESTADOS_HABILITADOS_PARA_ZARPAR.has(estadoActual)) {
    return { habilitado: true, tooltip: undefined, visible: true };
  }

  if (estadoActual === ESTADO_FONDEADO) {
    return { habilitado: false, tooltip: TOOLTIP_FONDEADO, visible: true };
  }

  // Cualquier otro estado (Zarpado, Cancelado, etc.): no se muestra el botón
  return { habilitado: false, tooltip: undefined, visible: false };
}

// ─── Componente ───────────────────────────────────────────────────────────────

export function BotonZarpar({ viaje, onSuccess, onError }: BotonZarparProps) {
  const { mutate: zarpar, isPending, isError, error } = useZarpar();

  const { habilitado, tooltip, visible } = resolverEstadoBoton(viaje.estadoActual);

  if (!visible) {
    return null;
  }

  const deshabilitado = !habilitado || isPending;

  function handleClick() {
    zarpar(
      { id: viaje.id },
      {
        onSuccess: () => {
          onSuccess?.();
        },
        onError: (err) => {
          onError?.(err);
        },
      }
    );
  }

  return (
    <span title={tooltip}>
      <button
        type="button"
        onClick={handleClick}
        disabled={deshabilitado}
        aria-busy={isPending}
        aria-label={
          isPending
            ? `Registrando zarpe de ${viaje.buque}…`
            : `Zarpar ${viaje.buque}`
        }
      >
        {isPending ? "Zarpando…" : "⚓ Zarpar"}
      </button>

      {isError && error != null && (
        <span role="alert" aria-live="assertive">
          {error.mensaje}
          {error.detail != null && (
            <span> — {error.detail}</span>
          )}
        </span>
      )}
    </span>
  );
}
