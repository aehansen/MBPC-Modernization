// src/components/convoy/PanelGestionConvoy.tsx

import { useQueryClient, useQuery } from '@tanstack/react-query';
import { isAxiosError } from 'axios';
import axiosInstance from '@/axiosClient';
import {
  useAmarrarBarcaza,
  useFondearBarcaza,
} from '@/hooks/useGestionConvoy';
import type { DotNetProblemDetails } from '@/hooks/useGestionConvoy';
import type { EstadoBarcaza } from '@/types/convoy.types';
import { useState } from 'react';

// ============================================================================
// DTOs — reflejan el ConvoyDto del backend en camelCase estricto
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
// Query Keys — objeto centralizado para evitar magic strings
// ============================================================================

const convoyKeys = {
  detail: (viajeId: string) => ['convoy', viajeId] as const,
};

// ============================================================================
// Fetcher
// ============================================================================

const fetchConvoy = async (id: string) => {
    // FIX: Ruta correcta del controlador de .NET. axiosClient ya inyecta el '/api' base.
    const { data } = await axiosInstance.get<ConvoyDto>(`/convoyes/viaje/${id}`);
    return data;
};

// ============================================================================
// Helpers
// ============================================================================

/**
 * Extrae un mensaje legible del error de la mutación sin recurrir a `any`.
 * Maneja texto plano, ProblemDetails estándar y ValidationProblemDetails.
 */
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

/** Skeleton para el header del convoy mientras carga. */
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

/** Skeleton para una tarjeta de barcaza mientras carga. */
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

/** Estado de error de la query principal (no de mutaciones). */
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
// Componente Principal
// ============================================================================

export default function PanelGestionConvoy({ viajeId }: PanelGestionConvoyProps) {
  const queryClient = useQueryClient();
  const [errorMutacion, setErrorMutacion] = useState<string | null>(null);
  const [pendingBarcazaId, setPendingBarcazaId] = useState<string | null>(null);

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
    // No reintentar en 404: el viaje no existe, no es un error transitorio.
    retry: (failureCount, error) => {
      if (isAxiosError(error) && error.response?.status === 404) return false;
      return failureCount < 2;
    },
  });

  // ─── Mutaciones ────────────────────────────────────────────────────────────
  const mutAmarrar = useAmarrarBarcaza();
  const mutFondear = useFondearBarcaza();

  /**
   * Invalida la query del convoy actual para que React Query refetch
   * y la UI refleje el nuevo estado de la barcaza tras una mutación exitosa.
   */
  function invalidarConvoy() {
    queryClient.invalidateQueries({ queryKey: convoyKeys.detail(viajeId) });
  }

  function handleAmarrar(barcazaId: string) {
    setErrorMutacion(null);
    setPendingBarcazaId(barcazaId);
    mutAmarrar.mutate(
      { barcazaId, payload: { nuevoMuelle: 'Muelle 1' } },
      {
        onSuccess: invalidarConvoy,
        onError:   (err) => setErrorMutacion(resolverMensajeError(err)),
        onSettled: () => setPendingBarcazaId(null),
      }
    );
  }

  function handleFondear(barcazaId: string) {
    setErrorMutacion(null);
    setPendingBarcazaId(barcazaId);
    mutFondear.mutate(
      { barcazaId, payload: { zonaFondeo: 'Zona Alfa' } },
      {
        onSuccess: invalidarConvoy,
        onError:   (err) => setErrorMutacion(resolverMensajeError(err)),
        onSettled: () => setPendingBarcazaId(null),
      }
    );
  }

  // ─── Tonelaje total (derivado del ConvoyDto, no calculado en el cliente) ──
  const tonelajeTotal = convoy?.barcazas.reduce((acc, b) => acc + b.tonelaje, 0) ?? 0;

  // ─── Render ────────────────────────────────────────────────────────────────
  return (
    <div className="bg-gray-50 rounded-xl border border-gray-200 overflow-hidden font-sans shadow-sm">
      <div className="p-6">
        {/* Alerta de errores de mutación */}
        {errorMutacion && (
          <AlertaError mensaje={errorMutacion} onDismiss={() => setErrorMutacion(null)} />
        )}

        {/* ── Estado: Cargando ───────────────────────────────────────────── */}
        {isLoading && (
          <>
            <SkeletonHeader />
            <div className="grid grid-cols-1 sm:grid-cols-2 xl:grid-cols-3 gap-4">
              {Array.from({ length: 3 }).map((_, i) => (
                <SkeletonBarcaza key={i} />
              ))}
            </div>
          </>
        )}

        {/* ── Estado: Error de fetch ─────────────────────────────────────── */}
        {isError && (
          <ErrorFetchConvoy
            mensaje={resolverMensajeError(queryError)}
            onRetry={refetch}
          />
        )}

        {/* ── Estado: Datos disponibles ──────────────────────────────────── */}
        {convoy && (
          <>
            {/* Header Institucional PNA */}
            <div className="bg-[#002454] p-5 rounded-xl flex justify-between items-center text-white mb-6 shadow-md">
              <div>
                <h2 className="text-xl font-bold tracking-tight">
                  Tractor: {convoy.remolcador?.nombre ?? convoy.nombreBuque}
                </h2>
                <p className="text-sm text-blue-200 mt-0.5">
                  Estado: {convoy.remolcador?.estado ?? 'Operativo'}
                </p>
              </div>

              <div className="text-right">
                <p className="text-xs text-blue-200 uppercase tracking-widest font-bold">Tonelaje Total</p>
                <p className="text-2xl font-bold tabular-nums">
                  {tonelajeTotal.toLocaleString('es-AR')}{' '}
                  <span className="text-sm font-normal">TN</span>
                </p>
              </div>
            </div>

            {/* Grid de Barcazas */}
            {convoy.barcazas.length === 0 ? (
              <div className="bg-white border-2 border-dashed border-gray-300 rounded-xl p-12 text-center">
                <p className="text-gray-500 font-semibold">No hay unidades asignadas a este convoy.</p>
              </div>
            ) : (
              <div className="grid grid-cols-1 sm:grid-cols-2 xl:grid-cols-3 gap-4">
                {convoy.barcazas.map((b) => {
                  const estadoCfg = ESTADO_CONFIG[b.estado] ?? ESTADO_CONFIG.EnTransito;
                  const isPending =
                    pendingBarcazaId === b.id &&
                    (mutAmarrar.isPending || mutFondear.isPending);

                  return (
                    <div
                      key={b.id}
                      className="bg-white rounded-xl border border-gray-200 p-5 space-y-3 shadow-sm hover:border-[#104a8e]/30 transition-colors"
                    >
                      {/* Encabezado Tarjeta */}
                      <div className="flex justify-between items-start">
                        <div className="min-w-0 pr-2">
                          <h3 className="font-bold text-gray-900 truncate" title={b.nombre}>
                            {b.nombre}
                          </h3>
                          <p className="text-[11px] text-gray-400 font-mono mt-0.5">
                            {b.matricula ?? 'S/M'}
                          </p>
                        </div>
                        <span
                          className={`inline-flex items-center gap-1.5 px-2.5 py-1 rounded-full text-[10px] font-bold uppercase tracking-wider border flex-shrink-0 ${estadoCfg.badgeCls}`}
                        >
                          {estadoCfg.label}
                        </span>
                      </div>

                      {/* Datos Carga */}
                      <div className="grid grid-cols-2 gap-2 mt-2">
                        <div className="bg-gray-50 rounded-lg p-2 text-center border border-gray-100">
                          <p className="text-[10px] text-gray-400 uppercase font-bold">Carga</p>
                          <p className="text-xs font-semibold text-gray-700 truncate" title={b.tipoCarga}>
                            {b.tipoCarga}
                          </p>
                        </div>
                        <div className="bg-gray-50 rounded-lg p-2 text-center border border-gray-100">
                          <p className="text-[10px] text-gray-400 uppercase font-bold">Peso</p>
                          <p className="text-xs font-mono font-semibold text-[#104a8e]">
                            {b.tonelaje.toLocaleString('es-AR')} {b.unidad}
                          </p>
                        </div>
                      </div>

                      {/* Acciones */}
                      <div className="flex gap-2 pt-2 border-t border-gray-100 mt-auto">
                        <button
                          onClick={() => handleAmarrar(b.id)}
                          disabled={isPending || b.estado === 'Amarrada' || b.estado === 'FueraDeServicio'}
                          className="flex-1 bg-[#104a8e] text-white py-1.5 rounded-lg text-xs font-semibold hover:bg-[#002454] disabled:opacity-40 disabled:cursor-not-allowed transition-all"
                        >
                          {isPending && mutAmarrar.isPending ? 'Amarrando...' : 'Amarrar'}
                        </button>
                        <button
                          onClick={() => handleFondear(b.id)}
                          disabled={isPending || b.estado === 'Fondeada' || b.estado === 'FueraDeServicio'}
                          className="flex-1 border border-[#104a8e] text-[#104a8e] py-1.5 rounded-lg text-xs font-semibold hover:bg-blue-50 disabled:opacity-40 disabled:cursor-not-allowed transition-all"
                        >
                          {isPending && mutFondear.isPending ? 'Fondeando...' : 'Fondear'}
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
  );
}
