// src/components/convoy/PanelGestionConvoy.tsx
// Hito 10.4 — Orquestador principal de convoyes

import { isAxiosError } from 'axios';
import {
  useFondearBarcaza,
  useAdjuntarBarcazas,
  useSepararConvoy,
} from '@/hooks/useGestionConvoy';
import type { DotNetProblemDetails } from '@/hooks/useGestionConvoy';
import type { ConvoyDto, EstadoBarcaza } from '@/types/convoy.types';
import { useState, useRef, useEffect } from 'react';
import EmbarcacionSelect from "@/components/viajes/EmbarcacionSelect";

// ============================================================================
// DTOs
// ============================================================================

/** Forma mínima que retorna EmbarcacionSelect al seleccionar un ítem */
interface EmbarcacionSugerida {
  idBuque: number | string;
  nombre: string;
  tipo: string;
  matricula: string | null;
  omi: string | null;
}

/** Barcaza/remolcador que el usuario agrega al borrador antes de guardar */
interface EmbarcacionBorrador {
  id: string;
  nombre: string;
  tipo: string;
  matricula: string | null;
}

// ============================================================================
// Props
// ============================================================================

export interface PanelGestionConvoyProps {
  viajeId: string;
  /** Composición actual del convoy (cargada por el modal contenedor). */
  convoy: ConvoyDto;
  /** Recarga la data del convoy tras mutaciones exitosas. */
  onRefreshConvoy: () => void;
}

// ============================================================================
// Modal State Types
// ============================================================================

type AccionModal = 'fondear';

interface EstadoModal {
  abierto: boolean;
  accion: AccionModal | null;
  barcazaId: string | null;
  barcazaNombre: string;
  destino: string;
}

interface EstadoModalSeparar {
  abierto: boolean;
  barcazaId: string;
  barcazaNombre: string;
  ubicacion: string;
}

const MODAL_INICIAL: EstadoModal = {
  abierto: false,
  accion: null,
  barcazaId: null,
  barcazaNombre: '',
  destino: '',
};

const MODAL_SEPARAR_INICIAL: EstadoModalSeparar = {
  abierto: false,
  barcazaId: '',
  barcazaNombre: '',
  ubicacion: '',
};

// ============================================================================
// Helpers
// ============================================================================

const esNumerico = (str: string): boolean => /^\d+$/.test(str);

function resolverMensajeError(error: Error | null): string {
  if (!error) return '';
  if (isAxiosError<DotNetProblemDetails | string>(error) && error.response?.data) {
    const data = error.response.data;
    if (typeof data === 'string') return data;
    if (typeof data === 'object' && data !== null) {
      if (data.errors && Object.keys(data.errors).length > 0) {
        return data.errors[Object.keys(data.errors)[0]][0];
      }
      if (data.detail) return data.detail;
      if (data.mensaje) return data.mensaje;
      if (data.title && !data.title.toLowerCase().includes('validation')) return data.title;
    }
  }
  return error.message;
}

const ESTADO_CONFIG: Record<EstadoBarcaza, { label: string; badgeCls: string; dotCls: string }> = {
  EnTransito: { label: 'En Tránsito', badgeCls: 'bg-blue-100 text-blue-800 border-blue-200', dotCls: 'bg-blue-500' },
  Amarrada: { label: 'Amarrada', badgeCls: 'bg-emerald-100 text-emerald-800 border-emerald-200', dotCls: 'bg-emerald-500' },
  Fondeada: { label: 'Fondeada', badgeCls: 'bg-amber-100 text-amber-800 border-amber-200', dotCls: 'bg-amber-500' },
  EnCarga: { label: 'En Carga', badgeCls: 'bg-violet-100 text-violet-800 border-violet-200', dotCls: 'bg-violet-500' },
  EnDescarga: { label: 'En Descarga', badgeCls: 'bg-orange-100 text-orange-800 border-orange-200', dotCls: 'bg-orange-500' },
  FueraDeServicio: { label: 'Fuera de Servicio', badgeCls: 'bg-red-100 text-red-700 border-red-200', dotCls: 'bg-red-500' },
};

// ============================================================================
// Sub-componentes UI
// ============================================================================

function AlertaError({ mensaje, onDismiss }: { mensaje: string; onDismiss: () => void }) {
  return (
    <div
      role="alert"
      className="flex items-start gap-3 px-4 py-3 bg-red-950/40 border border-red-500/40 rounded-xl mb-4 shadow-sm"
    >
      <svg className="w-5 h-5 text-red-400 mt-0.5 flex-shrink-0" fill="none" stroke="currentColor" viewBox="0 0 24 24">
        <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 8v4m0 4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
      </svg>
      <div className="flex-1 min-w-0">
        <p className="text-red-300 text-sm font-semibold">Error en la operación</p>
        <p className="text-red-400 text-xs mt-0.5 leading-snug break-words">{mensaje}</p>
      </div>
      <button type="button" onClick={onDismiss} className="text-red-500 hover:text-red-300 transition-colors">
        <svg className="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" />
        </svg>
      </button>
    </div>
  );
}

/* dead code removed in 10.4 — fetch lives in ModalGestionConvoy */
function _SkeletonHeaderUnused() {
  return (
    <div className="bg-slate-800/70 p-5 rounded-xl flex justify-between items-center mb-6 shadow-md animate-pulse border border-slate-700/50">
      <div className="space-y-2">
        <div className="h-5 w-48 bg-slate-600/50 rounded-md" />
        <div className="h-3 w-24 bg-slate-700/50 rounded-md" />
      </div>
      <div className="text-right space-y-2">
        <div className="h-3 w-20 bg-slate-700/50 rounded-md ml-auto" />
        <div className="h-7 w-28 bg-slate-600/50 rounded-md ml-auto" />
      </div>
    </div>
  );
}

function SkeletonBarcaza() {
  return (
    <div className="bg-slate-800/50 rounded-xl border border-slate-700/50 p-5 space-y-3 animate-pulse">
      <div className="flex justify-between items-start">
        <div className="space-y-1.5 flex-1 pr-4">
          <div className="h-4 w-3/4 bg-slate-700/60 rounded" />
          <div className="h-3 w-1/2 bg-slate-700/40 rounded" />
        </div>
        <div className="h-5 w-20 bg-slate-700/40 rounded-full" />
      </div>
      <div className="grid grid-cols-2 gap-2">
        <div className="h-12 bg-slate-700/40 rounded-lg" />
        <div className="h-12 bg-slate-700/40 rounded-lg" />
      </div>
      <div className="flex gap-2 pt-2 border-t border-slate-700/50">
        <div className="flex-1 h-8 bg-slate-600/40 rounded-lg" />
        <div className="flex-1 h-8 bg-slate-700/30 rounded-lg" />
      </div>
    </div>
  );
}

function ErrorFetchConvoy({ mensaje, onRetry }: { mensaje: string; onRetry: () => void }) {
  return (
    <div className="flex flex-col items-center justify-center py-16 gap-4">
      <div className="w-14 h-14 rounded-full bg-red-950/50 border border-red-500/30 flex items-center justify-center">
        <svg className="w-7 h-7 text-red-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 9v2m0 4h.01M10.29 3.86L1.82 18a2 2 0 001.71 3h16.94a2 2 0 001.71-3L13.71 3.86a2 2 0 00-3.42 0z" />
        </svg>
      </div>
      <div className="text-center">
        <p className="text-slate-200 font-semibold text-sm">No se pudo cargar el convoy</p>
        <p className="text-slate-500 text-xs mt-1 max-w-xs">{mensaje}</p>
      </div>
      <button
        onClick={onRetry}
        className="px-4 py-2 bg-cyan-600 text-white text-xs font-semibold rounded-lg hover:bg-cyan-500 transition-colors"
      >
        Reintentar
      </button>
    </div>
  );
}

// ============================================================================
// Sección: Borrador de embarcaciones a adjuntar
// ============================================================================

interface BorradorItem {
  embarcacion: EmbarcacionBorrador;
  onEliminar: (id: string) => void;
}

function BorradorRow({ embarcacion, onEliminar }: BorradorItem) {
  const tipoBadge =
    embarcacion.tipo === 'remolcador'
      ? 'bg-amber-500/10 text-amber-300 border-amber-500/30'
      : 'bg-cyan-500/10 text-cyan-300 border-cyan-500/30';

  return (
    <li className="flex items-center gap-3 px-3 py-2.5 rounded-lg bg-slate-800/60 border border-slate-700/60 group hover:border-slate-600/80 transition-colors">
      {/* Ícono de embarcación */}
      <div className="w-7 h-7 rounded-md bg-slate-700/80 flex items-center justify-center flex-shrink-0">
        <svg className="w-4 h-4 text-slate-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={1.5}
            d="M3 17l2-8h14l2 8H3zM12 9V3M8 9l1-3M16 9l-1-3" />
        </svg>
      </div>

      {/* Info */}
      <div className="flex-1 min-w-0">
        <p className="text-sm font-semibold text-slate-100 truncate">{embarcacion.nombre}</p>
        <p className="text-[11px] text-slate-500 font-mono">{embarcacion.matricula ?? 'S/M'}</p>
      </div>

      {/* Badge tipo */}
      <span className={`hidden sm:inline-flex px-2 py-0.5 rounded-full text-[10px] font-bold uppercase tracking-wider border ${tipoBadge}`}>
        {embarcacion.tipo}
      </span>

      {/* Eliminar */}
      <button
        type="button"
        onClick={() => onEliminar(embarcacion.id)}
        title="Quitar del borrador"
        className="ml-1 w-6 h-6 flex items-center justify-center rounded-md text-slate-600 hover:text-red-400 hover:bg-red-950/40 transition-colors opacity-0 group-hover:opacity-100 focus:opacity-100"
      >
        <svg className="w-3.5 h-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2.5} d="M6 18L18 6M6 6l12 12" />
        </svg>
      </button>
    </li>
  );
}

// ============================================================================
// Modales de Interacción
// ============================================================================

interface ModalDestinoProps {
  estado: EstadoModal;
  isPending: boolean;
  onChange: (destino: string) => void;
  onConfirmar: () => void;
  onCancelar: () => void;
}

function ModalDestino({ estado, isPending, onChange, onConfirmar, onCancelar }: ModalDestinoProps) {
  const inputRef = useRef<HTMLInputElement>(null);

  useEffect(() => {
    if (estado.abierto) {
      const t = setTimeout(() => inputRef.current?.focus(), 50);
      return () => clearTimeout(t);
    }
  }, [estado.abierto]);

  useEffect(() => {
    if (!estado.abierto) return;
    const handler = (e: KeyboardEvent) => {
      if (e.key === 'Escape') onCancelar();
    };
    window.addEventListener('keydown', handler);
    return () => window.removeEventListener('keydown', handler);
  }, [estado.abierto, onCancelar]);

  if (!estado.abierto) return null;

  return (
    <div
      role="dialog"
      aria-modal="true"
      className="fixed inset-0 z-50 flex items-center justify-center p-4"
      onClick={(e) => { if (e.target === e.currentTarget) onCancelar(); }}
    >
      <div className="absolute inset-0 bg-black/60 backdrop-blur-[3px]" aria-hidden="true" />
      <div className="relative bg-slate-900 rounded-2xl shadow-2xl w-full max-w-sm border border-slate-700 overflow-hidden">
        <div className="bg-slate-800 px-6 py-4 flex items-center gap-3 border-b border-slate-700">
          <div className="w-8 h-8 rounded-full bg-amber-500/20 flex items-center justify-center flex-shrink-0">
            <svg className="w-5 h-5 text-amber-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M5 12H3l9-9 9 9h-2M5 12v7a2 2 0 002 2h10a2 2 0 002-2v-7" />
            </svg>
          </div>
          <div className="min-w-0">
            <h2 className="font-bold text-base text-slate-100 leading-tight">Fondear Barcaza</h2>
            {estado.barcazaNombre && (
              <p className="text-slate-400 text-xs mt-0.5 truncate">{estado.barcazaNombre}</p>
            )}
          </div>
        </div>
        <div className="px-6 py-5 space-y-4">
          <div>
            <label className="block text-xs font-semibold text-slate-400 uppercase tracking-wider mb-1.5">
              Zona de fondeo
            </label>
            <input
              ref={inputRef}
              type="text"
              value={estado.destino}
              onChange={(e) => onChange(e.target.value)}
              onKeyDown={(e) => {
                if (e.key === 'Enter' && estado.destino.trim() && !isPending) onConfirmar();
              }}
              placeholder="Ej: Zona Beta"
              disabled={isPending}
              className="w-full border border-slate-600 bg-slate-800 rounded-lg px-3 py-2.5 text-sm text-slate-100 placeholder:text-slate-500 focus:outline-none focus:ring-2 focus:ring-cyan-500/40 focus:border-cyan-500 disabled:opacity-50 transition-colors"
            />
          </div>
          <div className="flex gap-2.5 pt-1">
            <button
              onClick={onCancelar}
              disabled={isPending}
              className="flex-1 border border-slate-600 text-slate-300 py-2.5 rounded-lg text-sm font-semibold hover:bg-slate-800 disabled:opacity-40 transition-colors"
            >
              Cancelar
            </button>
            <button
              onClick={onConfirmar}
              disabled={isPending || !estado.destino.trim()}
              className="flex-1 bg-amber-600 text-white py-2.5 rounded-lg text-sm font-semibold hover:bg-amber-500 disabled:opacity-40 flex items-center justify-center gap-1.5 transition-colors"
            >
              {isPending ? 'Procesando…' : 'Fondear'}
            </button>
          </div>
        </div>
      </div>
    </div>
  );
}

interface ModalSepararProps {
  estado: EstadoModalSeparar;
  isPending: boolean;
  setEstado: React.Dispatch<React.SetStateAction<EstadoModalSeparar>>;
  onConfirmar: () => void;
  onCancelar: () => void;
}

function ModalSeparar({ estado, isPending, setEstado, onConfirmar, onCancelar }: ModalSepararProps) {
  const inputRef = useRef<HTMLInputElement>(null);

  useEffect(() => {
    if (estado.abierto) {
      const t = setTimeout(() => inputRef.current?.focus(), 50);
      return () => clearTimeout(t);
    }
  }, [estado.abierto]);

  if (!estado.abierto) return null;

  return (
    <div
      role="dialog"
      className="fixed inset-0 z-50 flex items-center justify-center p-4"
      onClick={(e) => { if (e.target === e.currentTarget) onCancelar(); }}
    >
      <div className="absolute inset-0 bg-black/60 backdrop-blur-[3px]" aria-hidden="true" />
      <div className="relative bg-slate-900 rounded-2xl shadow-2xl w-full max-w-sm border border-slate-700 overflow-hidden">
        <div className="bg-red-950/60 border-b border-red-900/60 px-6 py-4 flex items-center gap-3">
          <div className="w-8 h-8 rounded-full bg-red-500/20 flex items-center justify-center flex-shrink-0">
            <svg className="w-5 h-5 text-red-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" />
            </svg>
          </div>
          <div className="min-w-0">
            <h2 className="font-bold text-base text-slate-100 leading-tight">Liberar Barcaza</h2>
            <p className="text-red-400 text-xs mt-0.5 truncate">{estado.barcazaNombre}</p>
          </div>
        </div>
        <div className="px-6 py-5 space-y-4">
          <p className="text-sm text-slate-400">
            Estás a punto de liberar <strong className="text-slate-200">{estado.barcazaNombre}</strong> del convoy.
            ¿Dónde quedará ubicada?
          </p>
          <div>
            <label className="block text-xs font-semibold text-slate-400 uppercase tracking-wider mb-1.5">
              Ubicación final
            </label>
            <input
              ref={inputRef}
              type="text"
              value={estado.ubicacion}
              onChange={(e) => setEstado({ ...estado, ubicacion: e.target.value })}
              placeholder="Ej: Muelle Principal"
              disabled={isPending}
              className="w-full border border-slate-600 bg-slate-800 rounded-lg px-3 py-2.5 text-sm text-slate-100 placeholder:text-slate-500 focus:outline-none focus:ring-2 focus:ring-red-500/40 focus:border-red-500 disabled:opacity-50"
            />
          </div>
          <div className="flex gap-2.5 pt-1">
            <button
              onClick={onCancelar}
              disabled={isPending}
              className="flex-1 border border-slate-600 text-slate-300 py-2.5 rounded-lg text-sm font-semibold hover:bg-slate-800 disabled:opacity-40 transition-colors"
            >
              Cancelar
            </button>
            <button
              onClick={onConfirmar}
              disabled={isPending || !estado.ubicacion.trim()}
              className="flex-1 bg-red-600 text-white py-2.5 rounded-lg text-sm font-semibold hover:bg-red-500 disabled:opacity-40 transition-colors"
            >
              {isPending ? 'Liberando…' : 'Liberar Barcaza'}
            </button>
          </div>
        </div>
      </div>
    </div>
  );
}

// ============================================================================
// Componente Principal
// ============================================================================

export default function PanelGestionConvoy({
  viajeId,
  convoy,
  onRefreshConvoy,
}: PanelGestionConvoyProps) {
  const [errorMutacion, setErrorMutacion] = useState<string | null>(null);
  const [pendingBarcazaId, setPendingBarcazaId] = useState<string | null>(null);

  // ─── Estado del Borrador (Hito 10.4) ──────────────────────────────────────
  const [borradorEmbarcaciones, setBorradorEmbarcaciones] = useState<EmbarcacionBorrador[]>([]);

  // ─── Estado de los Modales ─────────────────────────────────────────────────
  const [modal, setModal] = useState<EstadoModal>(MODAL_INICIAL);
  const [modalSeparar, setModalSeparar] = useState<EstadoModalSeparar>(MODAL_SEPARAR_INICIAL);

  // ─── Mutaciones ────────────────────────────────────────────────────────────
  const mutFondear = useFondearBarcaza();
  const mutAdjuntar = useAdjuntarBarcazas();
  const mutSeparar = useSepararConvoy();

  // ─── Lógica del Borrador ───────────────────────────────────────────────────

  /**
   * Agrega una embarcación al borrador si no existe ya (por id).
   * Llamado desde el onSelect de EmbarcacionSelect.
   */
  function handleSelectEmbarcacion(embarcacion: EmbarcacionSugerida): void {
    const id = String(embarcacion.idBuque);
    const yaExiste = borradorEmbarcaciones.some((e) => e.id === id);
    if (yaExiste) return;

    setBorradorEmbarcaciones((prev) => [
      ...prev,
      {
        id,
        nombre: embarcacion.nombre,
        tipo: embarcacion.tipo,
        matricula: embarcacion.matricula,
      },
    ]);
  }

  function eliminarDelBorrador(id: string): void {
    setBorradorEmbarcaciones((prev) => prev.filter((e) => e.id !== id));
  }

  function handleGuardarConvoy(): void {
    if (borradorEmbarcaciones.length === 0) return;

    mutAdjuntar.mutate(
      {
        viajeId,
        payload: {
          barcazasIds: borradorEmbarcaciones.map((e) => e.id),
          ubicacion: 'Convoy activo',
        },
      },
      {
        onSuccess: () => {
          onRefreshConvoy();
          setBorradorEmbarcaciones([]);
        },
        onError: (err: unknown) => setErrorMutacion(resolverMensajeError(err as Error)),
      },
    );
  }

  // ─── Apertura de Modales ───────────────────────────────────────────────────

  function abrirModalFondear(barcazaId: string, barcazaNombre: string): void {
    setErrorMutacion(null);
    setModal({ abierto: true, accion: 'fondear', barcazaId, barcazaNombre, destino: '' });
  }

  function abrirModalSeparar(barcazaId: string, barcazaNombre: string): void {
    setErrorMutacion(null);
    setModalSeparar({ abierto: true, barcazaId, barcazaNombre, ubicacion: '' });
  }

  function cerrarModales(): void {
    setModal(MODAL_INICIAL);
    setModalSeparar(MODAL_SEPARAR_INICIAL);
  }

  function actualizarDestino(destino: string): void {
    setModal((prev) => ({ ...prev, destino }));
  }

  // ─── Confirmación desde el Modal ──────────────────────────────────────────

  function handleConfirmarDestino(): void {
    if (!modal.barcazaId || !modal.destino.trim() || !modal.accion) return;
    const { barcazaId, destino } = modal;
    setPendingBarcazaId(barcazaId);
    cerrarModales();

    mutFondear.mutate(
      {
        barcazaId,
        viajeId,
        payload: { zonaFondeo: destino.trim() },
      },
      {
        onSuccess: onRefreshConvoy,
        onError: (err: unknown) => setErrorMutacion(resolverMensajeError(err as Error)),
        onSettled: () => setPendingBarcazaId(null),
      },
    );
  }

  function handleConfirmarSeparar(): void {
    if (!modalSeparar.barcazaId || !modalSeparar.ubicacion.trim()) return;
    const { barcazaId, ubicacion } = modalSeparar;
    setPendingBarcazaId(barcazaId);
    cerrarModales();

    mutSeparar.mutate(
      { viajeId, payload: { barcazasIds: [barcazaId], ubicacion: ubicacion.trim() } },
      {
        onSuccess: onRefreshConvoy,
        onError: (err: unknown) => setErrorMutacion(resolverMensajeError(err as Error)),
        onSettled: () => setPendingBarcazaId(null),
      },
    );
  }

  // ─── Derivados ─────────────────────────────────────────────────────────────

  const tonelajeTotal = convoy.barcazas.reduce((acc, b) => acc + b.tonelaje, 0);
  const modalFondearIsPending = modal.abierto && mutFondear.isPending;
  const rawTractorName = convoy.remolcador?.nombre ?? convoy.nombreBuque ?? 'Remolcador Desconocido';
  const tractorVisual = esNumerico(rawTractorName) ? `Remolcador ${rawTractorName}` : rawTractorName;
  const hayBorrador = borradorEmbarcaciones.length > 0;

  // ============================================================================
  // Render
  // ============================================================================

  return (
    <>
      <ModalDestino
        estado={modal}
        isPending={modalFondearIsPending}
        onChange={actualizarDestino}
        onConfirmar={handleConfirmarDestino}
        onCancelar={cerrarModales}
      />
      <ModalSeparar
        estado={modalSeparar}
        isPending={mutSeparar.isPending}
        setEstado={setModalSeparar}
        onConfirmar={handleConfirmarSeparar}
        onCancelar={cerrarModales}
      />

      <div className="bg-slate-900 rounded-xl border border-slate-700/60 overflow-hidden font-sans shadow-lg">
        <div className="p-6 space-y-6">

          {/* ── Alertas de error de mutación ── */}
          {errorMutacion && (
            <AlertaError mensaje={errorMutacion} onDismiss={() => setErrorMutacion(null)} />
          )}

          {/* ── Header del Convoy ── */}
          <div className="bg-gradient-to-r from-slate-800 to-slate-800/70 border border-slate-700/60 p-5 rounded-xl flex flex-wrap justify-between items-center gap-4 shadow-md">
            <div>
              <p className="text-[10px] font-bold text-cyan-500 uppercase tracking-[0.15em] mb-1">
                Convoy Activo
              </p>
              <h2 className="text-xl font-bold text-slate-100 tracking-tight">
                {tractorVisual}
              </h2>
              <p className="text-xs text-slate-400 mt-0.5">
                Estado:{' '}
                <span className="text-slate-300 font-medium">
                  {convoy.remolcador?.estado ?? 'Operativo'}
                </span>
              </p>
            </div>
            <div className="text-right">
              <p className="text-[10px] text-slate-500 uppercase tracking-widest font-bold">Tonelaje Total</p>
              <p className="text-2xl font-bold tabular-nums text-slate-100">
                {tonelajeTotal.toLocaleString('es-AR')}{' '}
                <span className="text-sm font-normal text-slate-400">TN</span>
              </p>
              <p className="text-[11px] text-slate-500 mt-0.5">
                {convoy.barcazas.length} barcaza{convoy.barcazas.length !== 1 ? 's' : ''} en convoy
              </p>
            </div>
          </div>

          {/* ──────────────────────────────────────────────────────────────
                  SECCIÓN: Agregar embarcaciones al convoy (Hito 10.4)
              ────────────────────────────────────────────────────────────── */}
          <section className="space-y-4">
            <div className="flex items-center gap-2">
              <div className="h-px flex-1 bg-slate-700/60" />
              <h3 className="text-xs font-bold text-slate-400 uppercase tracking-widest px-2">
                Agregar al Convoy
              </h3>
              <div className="h-px flex-1 bg-slate-700/60" />
            </div>

            {/* Selector de embarcación */}
            {/* @ts-expect-error — EmbarcacionSelect es JSX sin tipos exportados aún */}
            <EmbarcacionSelect
              allowedTipos={["barcaza"]}
              onSelect={handleSelectEmbarcacion}
              disabled={mutAdjuntar.isPending}
            />

            {/* Lista del borrador */}
            {hayBorrador ? (
              <div className="rounded-xl border border-slate-700/60 bg-slate-800/30 overflow-hidden">
                {/* Encabezado de la lista */}
                <div className="flex items-center justify-between px-4 py-2.5 border-b border-slate-700/50 bg-slate-800/50">
                  <span className="text-xs font-semibold text-slate-300">
                    Borrador —{' '}
                    <span className="text-cyan-400">
                      {borradorEmbarcaciones.length} embarcación{borradorEmbarcaciones.length !== 1 ? 'es' : ''}
                    </span>
                  </span>
                  <button
                    type="button"
                    onClick={() => setBorradorEmbarcaciones([])}
                    className="text-[11px] text-slate-500 hover:text-red-400 font-medium transition-colors"
                  >
                    Limpiar todo
                  </button>
                </div>

                {/* Ítems del borrador */}
                <ul className="p-3 space-y-1.5">
                  {borradorEmbarcaciones.map((emb) => (
                    <BorradorRow
                      key={emb.id}
                      embarcacion={emb}
                      onEliminar={eliminarDelBorrador}
                    />
                  ))}
                </ul>

                {/* Botón Guardar Convoy */}
                <div className="px-3 pb-3">
                  <button
                    type="button"
                    onClick={handleGuardarConvoy}
                    disabled={mutAdjuntar.isPending}
                    className="w-full flex items-center justify-center gap-2 bg-cyan-600 hover:bg-cyan-500
                                   disabled:opacity-50 disabled:cursor-not-allowed
                                   text-white font-semibold text-sm rounded-lg px-4 py-2.5
                                   transition-colors shadow-md shadow-cyan-900/30"
                  >
                    {mutAdjuntar.isPending ? (
                      <>
                        <svg className="w-4 h-4 animate-spin" fill="none" viewBox="0 0 24 24">
                          <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" />
                          <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8v8H4z" />
                        </svg>
                        Guardando…
                      </>
                    ) : (
                      <>
                        <svg className="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                          <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2}
                            d="M9 12l2 2 4-4m6 2a9 9 0 11-18 0 9 9 0 0118 0z" />
                        </svg>
                        Guardar Convoy ({borradorEmbarcaciones.length})
                      </>
                    )}
                  </button>
                </div>
              </div>
            ) : (
              /* Empty state del borrador */
              <p className="text-center text-xs text-slate-600 py-2">
                Buscá una barcaza o remolcador para agregarla al convoy.
              </p>
            )}
          </section>

          {/* ──────────────────────────────────────────────────────────────
                  SECCIÓN: Barcazas actuales del convoy
              ────────────────────────────────────────────────────────────── */}
          <section className="space-y-4">
            <div className="flex items-center gap-2">
              <div className="h-px flex-1 bg-slate-700/60" />
              <h3 className="text-xs font-bold text-slate-400 uppercase tracking-widest px-2">
                Barcazas en Convoy
              </h3>
              <div className="h-px flex-1 bg-slate-700/60" />
            </div>

            {convoy.barcazas.length === 0 ? (
              <div className="border-2 border-dashed border-slate-700/50 rounded-xl p-10 text-center">
                <p className="text-slate-500 text-sm font-medium">No hay barcazas asociadas a este convoy.</p>
                <p className="text-slate-600 text-xs mt-1">Usá el selector de arriba para agregar.</p>
              </div>
            ) : (
              <div className="grid grid-cols-1 sm:grid-cols-2 xl:grid-cols-3 gap-4">
                {convoy.barcazas.map((b) => {
                  const estadoCfg = ESTADO_CONFIG[b.estado] ?? ESTADO_CONFIG.EnTransito;
                  const isPending = pendingBarcazaId === b.id && (mutSeparar.isPending || mutFondear.isPending);
                  const barcazaVisual = esNumerico(b.nombre)
                    ? (b.matricula ? b.matricula : `BZA-${b.nombre}`)
                    : b.nombre;

                  return (
                    <div
                      key={b.id}
                      className="bg-slate-800/60 rounded-xl border border-slate-700/60 p-5 space-y-3 hover:border-cyan-500/30 transition-colors"
                    >
                      <div className="flex justify-between items-start">
                        <div className="min-w-0 pr-2">
                          <h3 className="font-bold text-slate-100 truncate" title={barcazaVisual}>
                            {barcazaVisual}
                          </h3>
                          <p className="text-[11px] text-slate-500 font-mono mt-0.5">
                            {b.matricula ?? 'S/M'}
                          </p>
                        </div>
                        <span className={`inline-flex items-center gap-1.5 px-2.5 py-1 rounded-full text-[10px] font-bold uppercase tracking-wider border ${estadoCfg.badgeCls}`}>
                          {estadoCfg.label}
                        </span>
                      </div>

                      <div className="grid grid-cols-2 gap-2">
                        <div className="bg-slate-700/30 rounded-lg p-2 text-center border border-slate-700/40">
                          <p className="text-[10px] text-slate-500 uppercase font-bold">Peso</p>
                          <p className="text-xs font-mono font-semibold text-cyan-400">
                            {b.tonelaje > 0
                              ? `${b.tonelaje.toLocaleString('es-AR')} ${b.unidad || 'Tn'}`
                              : <span className="italic text-slate-600">—</span>}
                          </p>
                        </div>
                        <div className="bg-slate-700/30 rounded-lg p-2 text-center border border-slate-700/40">
                          <p className="text-[10px] text-slate-500 uppercase font-bold">Carga</p>
                          <p className="text-xs font-semibold text-slate-300 truncate" title={b.tipoCarga ?? undefined}>
                            {b.tipoCarga && b.tipoCarga !== 'A Definir'
                              ? b.tipoCarga
                              : <span className="italic text-slate-600">A Definir</span>}
                          </p>
                        </div>
                      </div>

                      <div className="flex gap-2 pt-2 border-t border-slate-700/50">
                        <button
                          onClick={() => abrirModalFondear(b.id, barcazaVisual)}
                          disabled={isPending || b.estado === 'Fondeada'}
                          className="flex-1 border border-slate-600 text-slate-300 py-1.5 rounded-lg text-xs font-semibold
                                         hover:bg-slate-700 disabled:opacity-30 disabled:cursor-not-allowed transition-all"
                        >
                          Fondear
                        </button>
                        <button
                          onClick={() => abrirModalSeparar(b.id, barcazaVisual)}
                          disabled={isPending || b.estado !== 'Amarrada'}
                          className="flex-1 bg-slate-700 text-slate-200 py-1.5 rounded-lg text-xs font-semibold
                                         hover:bg-slate-600 disabled:opacity-30 disabled:cursor-not-allowed transition-all"
                        >
                          {isPending && mutSeparar.isPending ? 'Liberando…' : 'Liberar'}
                        </button>
                      </div>
                    </div>
                  );
                })}
              </div>
            )}
          </section>
        </div>
      </div>
    </>
  );
}
