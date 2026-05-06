// src/components/convoy/PanelGestionConvoy.tsx

import { useQueryClient, useQuery } from '@tanstack/react-query';
import { isAxiosError } from 'axios';
import axiosInstance from '@/axiosClient';
import {
  useFondearBarcaza,
  useAdjuntarBarcazas,
  useSepararConvoy,
} from '@/hooks/useGestionConvoy';
import type { DotNetProblemDetails } from '@/hooks/useGestionConvoy';
import type { EstadoBarcaza } from '@/types/convoy.types';
import { useState, useRef, useEffect } from 'react';
import BarcazaAutocomplete from './BarcazaAutocomplete';

// ============================================================================
// DTOs
// ============================================================================

interface RemolcadorConvoyDto {
  id: string;
  nombre: string;
  estado: string;
  fechaSalida: string | null;
}

interface BarcazaConvoyDto {
  id: string;
  nombre: string;
  bandera: string;
  matricula: string | null;
  tipoCarga: string;
  tonelaje: number;
  unidad: string;
  muelleActual: string | null;
  estado: EstadoBarcaza;
}

interface ConvoyDto {
  viajeId: string;
  nombreBuque: string;
  remolcador: RemolcadorConvoyDto | null;
  barcazas: readonly BarcazaConvoyDto[];
}

// ============================================================================
// Props
// ============================================================================

interface PanelGestionConvoyProps {
  viajeId: string;
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

const MODAL_INICIAL: EstadoModal = {
  abierto: false,
  accion: null,
  barcazaId: null,
  barcazaNombre: '',
  destino: '',
};

interface EstadoModalAdjuntar {
  abierto: boolean;
  barcazaId: string;
  barcazaNombre: string;
  ubicacion: string;
}

interface EstadoModalSeparar {
  abierto: boolean;
  barcazaId: string;
  barcazaNombre: string;
  ubicacion: string;
}

const MODAL_ADJUNTAR_INICIAL: EstadoModalAdjuntar = { abierto: false, barcazaId: '', barcazaNombre: '', ubicacion: '' };
const MODAL_SEPARAR_INICIAL: EstadoModalSeparar = { abierto: false, barcazaId: '', barcazaNombre: '', ubicacion: '' };

// ============================================================================
// Query Keys & Fetcher
// ============================================================================

const convoyKeys = {
  detail: (viajeId: string) => ['convoy', viajeId] as const,
};

const fetchConvoy = async (id: string) => {
    const { data } = await axiosInstance.get<ConvoyDto>(`/convoyes/viaje/${id}`);
    return data;
};

// ============================================================================
// Helpers
// ============================================================================

const esNumerico = (str: string) => /^\d+$/.test(str);

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
  EnTransito:      { label: 'En Tránsito',      badgeCls: 'bg-blue-100 text-blue-800 border-blue-200',     dotCls: 'bg-blue-500'    },
  Amarrada:        { label: 'Amarrada',          badgeCls: 'bg-emerald-100 text-emerald-800 border-emerald-200', dotCls: 'bg-emerald-500' },
  Fondeada:        { label: 'Fondeada',          badgeCls: 'bg-amber-100 text-amber-800 border-amber-200',   dotCls: 'bg-amber-500'   },
  EnCarga:         { label: 'En Carga',          badgeCls: 'bg-violet-100 text-violet-800 border-violet-200', dotCls: 'bg-violet-500' },
  EnDescarga:      { label: 'En Descarga',       badgeCls: 'bg-orange-100 text-orange-800 border-orange-200', dotCls: 'bg-orange-500' },
  FueraDeServicio: { label: 'Fuera de Servicio', badgeCls: 'bg-red-100 text-red-700 border-red-200',        dotCls: 'bg-red-500'     },
};

// ============================================================================
// Sub-componentes UI
// ============================================================================

function AlertaError({ mensaje, onDismiss }: { mensaje: string; onDismiss: () => void }) {
  return (
    <div role="alert" className="flex items-start gap-3 px-4 py-3 bg-red-50 border border-red-200 rounded-xl mb-4 shadow-sm">
      <svg className="w-5 h-5 text-red-500 mt-0.5 flex-shrink-0" fill="none" stroke="currentColor" viewBox="0 0 24 24">
        <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 8v4m0 4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
      </svg>
      <div className="flex-1 min-w-0">
        <p className="text-red-800 text-sm font-semibold">Error en la operación</p>
        <p className="text-red-700 text-xs mt-0.5 leading-snug break-words">{mensaje}</p>
      </div>
      <button type="button" onClick={onDismiss} className="text-red-400 hover:text-red-600 transition-colors">
        <svg className="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" />
        </svg>
      </button>
    </div>
  );
}

function SkeletonHeader() {
  return (
    <div className="bg-[#002454] p-5 rounded-xl flex justify-between items-center mb-6 shadow-md animate-pulse">
      <div className="space-y-2">
        <div className="h-5 w-48 bg-white/20 rounded-md" />
        <div className="h-3 w-24 bg-white/10 rounded-md" />
      </div>
      <div className="text-right space-y-2">
        <div className="h-3 w-20 bg-white/10 rounded-md ml-auto" />
        <div className="h-7 w-28 bg-white/20 rounded-md ml-auto" />
      </div>
    </div>
  );
}

function SkeletonBarcaza() {
  return (
    <div className="bg-white rounded-xl border border-gray-200 p-5 space-y-3 shadow-sm animate-pulse">
      <div className="flex justify-between items-start">
        <div className="space-y-1.5 flex-1 pr-4">
          <div className="h-4 w-3/4 bg-gray-200 rounded" />
          <div className="h-3 w-1/2 bg-gray-100 rounded" />
        </div>
        <div className="h-5 w-20 bg-gray-100 rounded-full" />
      </div>
      <div className="grid grid-cols-2 gap-2">
        <div className="h-12 bg-gray-100 rounded-lg" />
        <div className="h-12 bg-gray-100 rounded-lg" />
      </div>
      <div className="flex gap-2 pt-2 border-t border-gray-100">
        <div className="flex-1 h-8 bg-gray-200 rounded-lg" />
        <div className="flex-1 h-8 bg-gray-100 rounded-lg" />
      </div>
    </div>
  );
}

function ErrorFetchConvoy({ mensaje, onRetry }: { mensaje: string; onRetry: () => void }) {
  return (
    <div className="flex flex-col items-center justify-center py-16 gap-4">
      <div className="w-14 h-14 rounded-full bg-red-100 flex items-center justify-center">
        <svg className="w-7 h-7 text-red-500" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 9v2m0 4h.01M10.29 3.86L1.82 18a2 2 0 001.71 3h16.94a2 2 0 001.71-3L13.71 3.86a2 2 0 00-3.42 0z" />
        </svg>
      </div>
      <div className="text-center">
        <p className="text-gray-800 font-semibold text-sm">No se pudo cargar el convoy</p>
        <p className="text-gray-500 text-xs mt-1 max-w-xs">{mensaje}</p>
      </div>
      <button
        onClick={onRetry}
        className="px-4 py-2 bg-[#104a8e] text-white text-xs font-semibold rounded-lg hover:bg-[#002454] transition-colors"
      >
        Reintentar
      </button>
    </div>
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
      <div className="absolute inset-0 bg-black/40 backdrop-blur-[2px]" aria-hidden="true" />
      <div className="relative bg-white rounded-2xl shadow-2xl w-full max-w-sm border border-gray-200 overflow-hidden">
        <div className="bg-[#104a8e] px-6 py-4 flex items-center gap-3 text-white">
          <div className="w-8 h-8 rounded-full bg-white/20 flex items-center justify-center flex-shrink-0">
            <svg className="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M5 12H3l9-9 9 9h-2M5 12v7a2 2 0 002 2h10a2 2 0 002-2v-7" />
            </svg>
          </div>
          <div className="min-w-0">
            <h2 className="font-bold text-base leading-tight">Fondear Barcaza</h2>
            {estado.barcazaNombre && (
              <p className="text-blue-200 text-xs mt-0.5 truncate">{estado.barcazaNombre}</p>
            )}
          </div>
        </div>
        <div className="px-6 py-5 space-y-4">
          <div>
            <label className="block text-xs font-semibold text-gray-600 uppercase tracking-wider mb-1.5">
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
              className="w-full border border-gray-300 rounded-lg px-3 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-[#104a8e]/40 focus:border-[#104a8e] disabled:bg-gray-50 transition-colors"
            />
          </div>
          <div className="flex gap-2.5 pt-1">
            <button onClick={onCancelar} disabled={isPending} className="flex-1 border border-gray-300 text-gray-700 py-2.5 rounded-lg text-sm font-semibold hover:bg-gray-50 disabled:opacity-40 transition-colors">
              Cancelar
            </button>
            <button onClick={onConfirmar} disabled={isPending || !estado.destino.trim()} className="flex-1 bg-[#104a8e] text-white py-2.5 rounded-lg text-sm font-semibold hover:bg-[#002454] disabled:opacity-40 flex items-center justify-center gap-1.5 transition-colors">
              {isPending ? 'Procesando...' : 'Fondear'}
            </button>
          </div>
        </div>
      </div>
    </div>
  );
}

interface ModalAdjuntarProps {
  viajeId: string;
  estado: EstadoModalAdjuntar;
  isPending: boolean;
  setEstado: React.Dispatch<React.SetStateAction<EstadoModalAdjuntar>>;
  onConfirmar: () => void;
  onCancelar: () => void;
}

function ModalAdjuntar({ viajeId, estado, isPending, setEstado, onConfirmar, onCancelar }: ModalAdjuntarProps) {
  if (!estado.abierto) return null;

  return (
    <div role="dialog" className="fixed inset-0 z-50 flex items-center justify-center p-4" onClick={(e) => { if (e.target === e.currentTarget) onCancelar(); }}>
      <div className="absolute inset-0 bg-black/40 backdrop-blur-[2px]" aria-hidden="true" />
      <div className="relative bg-white rounded-2xl shadow-2xl w-full max-w-sm border border-gray-200 overflow-hidden">
        <div className="bg-[#104a8e] px-6 py-4 flex items-center gap-3 text-white">
          <div className="w-8 h-8 rounded-full bg-white/20 flex items-center justify-center flex-shrink-0">
            <svg className="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 4v16m8-8H4" />
            </svg>
          </div>
          <h2 className="font-bold text-base leading-tight">Adjuntar Barcaza</h2>
        </div>
        <div className="px-6 py-5 space-y-4">
          <div>
            <div className="mb-3">
              <BarcazaAutocomplete 
                etapaId={viajeId} 
                onSelect={(b) => setEstado({ ...estado, barcazaId: b.idBuque.toString(), barcazaNombre: b.nombre })} 
                onClear={() => setEstado({ ...estado, barcazaId: '', barcazaNombre: '' })}
                disabled={isPending}
              />
            </div>
            <label className="block text-xs font-semibold text-gray-600 uppercase tracking-wider mb-1.5">Ubicación</label>
            <input
              type="text"
              value={estado.ubicacion}
              onChange={(e) => setEstado({ ...estado, ubicacion: e.target.value })}
              placeholder="Ej: Km 420"
              disabled={isPending}
              className="w-full border border-gray-300 rounded-lg px-3 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-[#104a8e]/40 focus:border-[#104a8e]"
            />
          </div>
          <div className="flex gap-2.5 pt-1">
            <button onClick={onCancelar} disabled={isPending} className="flex-1 border border-gray-300 text-gray-700 py-2.5 rounded-lg text-sm font-semibold hover:bg-gray-50 disabled:opacity-40 transition-colors">Cancelar</button>
            <button onClick={onConfirmar} disabled={isPending || !estado.barcazaId.trim() || !estado.ubicacion.trim()} className="flex-1 bg-[#104a8e] text-white py-2.5 rounded-lg text-sm font-semibold hover:bg-[#002454] disabled:opacity-40 transition-colors">
              {isPending ? 'Adjuntando...' : 'Adjuntar'}
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
    <div role="dialog" className="fixed inset-0 z-50 flex items-center justify-center p-4" onClick={(e) => { if (e.target === e.currentTarget) onCancelar(); }}>
      <div className="absolute inset-0 bg-black/40 backdrop-blur-[2px]" aria-hidden="true" />
      <div className="relative bg-white rounded-2xl shadow-2xl w-full max-w-sm border border-gray-200 overflow-hidden">
        <div className="bg-red-600 px-6 py-4 flex items-center gap-3 text-white">
          <div className="w-8 h-8 rounded-full bg-white/20 flex items-center justify-center flex-shrink-0">
            <svg className="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" />
            </svg>
          </div>
          <div className="min-w-0">
            <h2 className="font-bold text-base leading-tight">Liberar Barcaza</h2>
            <p className="text-red-200 text-xs mt-0.5 truncate">{estado.barcazaNombre}</p>
          </div>
        </div>
        <div className="px-6 py-5 space-y-4">
          <p className="text-sm text-gray-600">Estás a punto de liberar la barcaza <strong>{estado.barcazaNombre}</strong> del convoy. ¿Dónde quedará ubicada amarrada?</p>
          <div>
            <label className="block text-xs font-semibold text-gray-600 uppercase tracking-wider mb-1.5">Ubicación final</label>
            <input
              ref={inputRef}
              type="text"
              value={estado.ubicacion}
              onChange={(e) => setEstado({ ...estado, ubicacion: e.target.value })}
              placeholder="Ej: Muelle Principal"
              disabled={isPending}
              className="w-full border border-gray-300 rounded-lg px-3 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-red-500/40 focus:border-red-500"
            />
          </div>
          <div className="flex gap-2.5 pt-1">
            <button onClick={onCancelar} disabled={isPending} className="flex-1 border border-gray-300 text-gray-700 py-2.5 rounded-lg text-sm font-semibold hover:bg-gray-50 disabled:opacity-40 transition-colors">Cancelar</button>
            <button onClick={onConfirmar} disabled={isPending || !estado.ubicacion.trim()} className="flex-1 bg-red-600 text-white py-2.5 rounded-lg text-sm font-semibold hover:bg-red-700 disabled:opacity-40 transition-colors">
              {isPending ? 'Liberando...' : 'Liberar Barcaza'}
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

export default function PanelGestionConvoy({ viajeId }: PanelGestionConvoyProps) {
  const queryClient = useQueryClient();
  const [errorMutacion, setErrorMutacion] = useState<string | null>(null);
  const [pendingBarcazaId, setPendingBarcazaId] = useState<string | null>(null);

  // ─── Estado de los Modales ──────────────────────────────────────────────────
  const [modal, setModal] = useState<EstadoModal>(MODAL_INICIAL);
  const [modalAdjuntar, setModalAdjuntar] = useState<EstadoModalAdjuntar>(MODAL_ADJUNTAR_INICIAL);
  const [modalSeparar, setModalSeparar] = useState<EstadoModalSeparar>(MODAL_SEPARAR_INICIAL);

  // ─── Data Fetching ─────────────────────────────────────────────────────────
  const {
    data: convoy,
    isLoading,
    isError,
    error: queryError,
    refetch,
  } = useQuery<ConvoyDto, Error>({
    queryKey: convoyKeys.detail(viajeId),
    queryFn: () => fetchConvoy(viajeId),
    retry: (failureCount, error) => {
      if (isAxiosError(error) && error.response?.status === 404) return false;
      return failureCount < 2;
    },
  });

  // ─── Mutaciones ────────────────────────────────────────────────────────────
  const mutFondear = useFondearBarcaza();
  const mutAdjuntar = useAdjuntarBarcazas();
  const mutSeparar = useSepararConvoy();

  function invalidarConvoy() {
    queryClient.invalidateQueries({ queryKey: convoyKeys.detail(viajeId) });
  }

  // ─── Apertura de Modales ───────────────────────────────────────────────────

  function abrirModalFondear(barcazaId: string, barcazaNombre: string) {
    setErrorMutacion(null);
    setModal({ abierto: true, accion: 'fondear', barcazaId, barcazaNombre, destino: '' });
  }

  function abrirModalAdjuntar() {
    setErrorMutacion(null);
    setModalAdjuntar({ abierto: true, barcazaId: '', barcazaNombre: '', ubicacion: '' });
  }

  function abrirModalSeparar(barcazaId: string, barcazaNombre: string) {
    setErrorMutacion(null);
    setModalSeparar({ abierto: true, barcazaId, barcazaNombre, ubicacion: '' });
  }

  function cerrarModales() {
    setModal(MODAL_INICIAL);
    setModalAdjuntar(MODAL_ADJUNTAR_INICIAL);
    setModalSeparar(MODAL_SEPARAR_INICIAL);
  }

  function actualizarDestino(destino: string) {
    setModal((prev) => ({ ...prev, destino }));
  }

  // ─── Confirmación desde el Modal ───────────────────────────────────────────

  function handleConfirmarDestino() {
    if (!modal.barcazaId || !modal.destino.trim() || !modal.accion) return;
    const { barcazaId, destino } = modal;
    setPendingBarcazaId(barcazaId);
    cerrarModales();

    const handlers = {
      onSuccess: invalidarConvoy,
      onError: (err: unknown) => setErrorMutacion(resolverMensajeError(err as Error)),
      onSettled: () => setPendingBarcazaId(null),
    };

    mutFondear.mutate({ barcazaId, payload: { zonaFondeo: destino.trim() } }, handlers);
  }

  function handleConfirmarAdjuntar() {
    if (!modalAdjuntar.barcazaId.trim() || !modalAdjuntar.ubicacion.trim()) return;
    const { barcazaId, barcazaNombre, ubicacion } = modalAdjuntar;
    cerrarModales();

    mutAdjuntar.mutate(
      { viajeId, payload: { barcazasIds: [barcazaId.trim()], barcazaNombre: barcazaNombre.trim(), ubicacion: ubicacion.trim() } as any },
      {
        onSuccess: invalidarConvoy,
        onError: (err: unknown) => setErrorMutacion(resolverMensajeError(err as Error)),
      }
    );
  }

  function handleConfirmarSeparar() {
    if (!modalSeparar.barcazaId || !modalSeparar.ubicacion.trim()) return;
    const { barcazaId, ubicacion } = modalSeparar;
    setPendingBarcazaId(barcazaId);
    cerrarModales();

    mutSeparar.mutate(
      { viajeId, payload: { barcazasIds: [barcazaId], ubicacion: ubicacion.trim() } },
      {
        onSuccess: invalidarConvoy,
        onError: (err: unknown) => setErrorMutacion(resolverMensajeError(err as Error)),
        onSettled: () => setPendingBarcazaId(null),
      }
    );
  }

  const tonelajeTotal = convoy?.barcazas.reduce((acc, b) => acc + b.tonelaje, 0) ?? 0;
  const modalIsPending = modal.abierto && (mutFondear.isPending);

  // Parseamos un tractor o buque amigable para el frontend
  const rawTractorName = convoy?.remolcador?.nombre ?? convoy?.nombreBuque ?? "Remolcador Desconocido";
  const tractorVisual = esNumerico(rawTractorName) ? `Remolcador ${rawTractorName}` : rawTractorName;

  return (
    <>
      <ModalDestino estado={modal} isPending={modalIsPending} onChange={actualizarDestino} onConfirmar={handleConfirmarDestino} onCancelar={cerrarModales} />
      <ModalAdjuntar viajeId={viajeId} estado={modalAdjuntar} isPending={mutAdjuntar.isPending} setEstado={setModalAdjuntar} onConfirmar={handleConfirmarAdjuntar} onCancelar={cerrarModales} />
      <ModalSeparar estado={modalSeparar} isPending={mutSeparar.isPending} setEstado={setModalSeparar} onConfirmar={handleConfirmarSeparar} onCancelar={cerrarModales} />

      <div className="bg-gray-50 rounded-xl border border-gray-200 overflow-hidden font-sans shadow-sm">
        <div className="p-6">
          {errorMutacion && <AlertaError mensaje={errorMutacion} onDismiss={() => setErrorMutacion(null)} />}

          {isLoading && (
            <>
              <SkeletonHeader />
              <div className="grid grid-cols-1 sm:grid-cols-2 xl:grid-cols-3 gap-4">
                {Array.from({ length: 3 }).map((_, i) => <SkeletonBarcaza key={i} />)}
              </div>
            </>
          )}

          {isError && <ErrorFetchConvoy mensaje={resolverMensajeError(queryError)} onRetry={refetch} />}

          {convoy && (
            <>
              {/* Header Institucional */}
              <div className="bg-[#002454] p-5 rounded-xl flex justify-between items-center text-white mb-6 shadow-md">
                <div className="flex flex-col gap-2">
                  <div>
                    <h2 className="text-xl font-bold tracking-tight">
                      Convoy: {tractorVisual}
                    </h2>
                    <p className="text-sm text-blue-200 mt-0.5">
                      Estado: {convoy.remolcador?.estado ?? 'Operativo'}
                    </p>
                  </div>
                  <button
                    onClick={abrirModalAdjuntar}
                    className="w-max mt-1 border border-blue-400 text-blue-100 hover:bg-blue-800/50 hover:text-white px-3 py-1.5 rounded-lg text-xs font-semibold transition-colors flex items-center gap-1.5"
                  >
                    <svg className="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                      <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 4v16m8-8H4" />
                    </svg>
                    Adjuntar Barcaza
                  </button>
                </div>

                <div className="text-right">
                  <p className="text-xs text-blue-200 uppercase tracking-widest font-bold">Tonelaje Total</p>
                  <p className="text-2xl font-bold tabular-nums">
                    {tonelajeTotal.toLocaleString('es-AR')}{' '}
                    <span className="text-sm font-normal">TN</span>
                  </p>
                </div>
              </div>

              {/* Grid de Barcazas / Empty State */}
              {convoy.barcazas.length === 0 ? (
                <div className="bg-white border-2 border-dashed border-gray-300 rounded-xl p-12 text-center">
                  <p className="text-gray-500 font-semibold">No hay barcazas asociadas a este convoy.</p>
                </div>
              ) : (
                <div className="grid grid-cols-1 sm:grid-cols-2 xl:grid-cols-3 gap-4">
                  {convoy.barcazas.map((b) => {
                    const estadoCfg = ESTADO_CONFIG[b.estado] ?? ESTADO_CONFIG.EnTransito;
                    const isPending = pendingBarcazaId === b.id && (mutSeparar.isPending);
                    
                    // Fallback para nombres estrictamente numéricos (Hito 5.8 visual)
                    const barcazaVisual = esNumerico(b.nombre) 
                      ? (b.matricula ? b.matricula : `BZA-${b.nombre}`) 
                      : b.nombre;

                    return (
                      <div
                        key={b.id}
                        className="bg-white rounded-xl border border-gray-200 p-5 space-y-3 shadow-sm hover:border-[#104a8e]/30 transition-colors"
                      >
                        <div className="flex justify-between items-start">
                          <div className="min-w-0 pr-2">
                            <h3 className="font-bold text-gray-900 truncate" title={barcazaVisual}>
                              {barcazaVisual}
                            </h3>
                            <p className="text-[11px] text-gray-400 font-mono mt-0.5">
                              {b.matricula ?? 'S/M'}
                            </p>
                          </div>
                          <div className="flex items-center gap-2 flex-shrink-0">
                            <span className={`inline-flex items-center gap-1.5 px-2.5 py-1 rounded-full text-[10px] font-bold uppercase tracking-wider border ${estadoCfg.badgeCls}`}>
                              {estadoCfg.label}
                            </span>
                          </div>
                        </div>

                        <div className="grid grid-cols-3 gap-2 mt-2">
                          <div className="bg-gray-50 rounded-lg p-2 text-center border border-gray-100">
                            <p className="text-[10px] text-gray-400 uppercase font-bold">Tipo</p>
                            <p className="text-xs font-semibold text-gray-700 truncate" title={b.tipoCarga}>
                              {b.tipoCarga && b.tipoCarga !== 'A Definir' ? b.tipoCarga : <span className="italic text-gray-400">Sin datos</span>}
                            </p>
                          </div>
                          <div className="bg-gray-50 rounded-lg p-2 text-center border border-gray-100">
                            <p className="text-[10px] text-gray-400 uppercase font-bold">Peso</p>
                            <p className="text-xs font-mono font-semibold text-[#104a8e]">
                              {b.tonelaje > 0
                                ? `${b.tonelaje.toLocaleString('es-AR')} ${b.unidad || 'Tn'}`
                                : <span className="italic text-gray-400">—</span>}
                            </p>
                          </div>
                          {/* Mercadería: nombre real del producto (Soja, Gasoil, etc.) */}
                          <div className="bg-gray-50 rounded-lg p-2 text-center border border-gray-100">
                            <p className="text-[10px] text-gray-400 uppercase font-bold">Mercadería</p>
                            <p className="text-xs font-semibold text-gray-700 truncate" title={b.tipoCarga ?? undefined}>
                              {b.tipoCarga && b.tipoCarga !== 'A Definir'
                                ? b.tipoCarga
                                : <span className="italic text-gray-400">A Definir</span>}
                            </p>
                          </div>
                        </div>

                        <div className="flex gap-2 pt-2 border-t border-gray-100 mt-auto">
                          <button
                            onClick={() => abrirModalSeparar(b.id, barcazaVisual)}
                            disabled={isPending || b.estado !== 'Amarrada'}
                            className="w-full bg-[#104a8e] text-white py-1.5 rounded-lg text-xs font-semibold hover:bg-[#002454] disabled:opacity-40 disabled:cursor-not-allowed transition-all"
                          >
                            {isPending && mutSeparar.isPending ? 'Liberando...' : 'Liberar Barcaza'}
                          </button>
                        </div>
                      </div>
                    );
                  })}
                </div>
              )}
            </>
          )}
        </div>
      </div>
    </>
  );
}