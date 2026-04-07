import React, { useState } from 'react';

import {
  useAmarrarViaje,
  useFondearViaje,
  useReanudarViaje,
  useViajes,
  useZarparViaje,
} from '../../hooks/useViajes';
import type { ViajeDto } from '../../types/viajes.types';

// ─── Estado badge ─────────────────────────────────────────────────────────────

const ESTADO_STYLES: Record<string, string> = {
  EnViaje: 'bg-emerald-900/60 text-emerald-300 border border-emerald-700',
  Amarrado: 'bg-sky-900/60 text-sky-300 border border-sky-700',
  Fondeado: 'bg-amber-900/60 text-amber-300 border border-amber-700',
  Programado: 'bg-slate-700/60 text-slate-300 border border-slate-600',
};

function EstadoBadge({ estado }: { estado: string }) {
  const classes =
    ESTADO_STYLES[estado] ?? 'bg-slate-700/60 text-slate-300 border border-slate-600';
  return (
    <span className={`inline-block rounded-full px-2.5 py-0.5 text-xs font-semibold tracking-wide ${classes}`}>
      {estado || 'Desconocido'}
    </span>
  );
}

// ─── Action buttons ───────────────────────────────────────────────────────────

interface AccionesProps {
  viaje: ViajeDto;
  onZarpar: (id: string) => void;
  onAmarrar: (id: string) => void;
  onFondear: (id: string) => void;
  onReanudar: (id: string) => void;
  isLoading: boolean;
}

function AccionesRow({
  viaje,
  onZarpar,
  onAmarrar,
  onFondear,
  onReanudar,
  isLoading,
}: AccionesProps) {
  const btnBase =
    'px-3 py-1.5 rounded text-xs font-semibold tracking-wide transition-all duration-150 disabled:opacity-40 disabled:cursor-not-allowed';

  return (
    <div className="flex flex-wrap gap-1.5">
      <button
        className={`${btnBase} bg-emerald-600 hover:bg-emerald-500 text-white`}
        disabled={isLoading || viaje.estadoActual === 'EnViaje'}
        onClick={() => onZarpar(viaje.id)}
        title="Zarpar"
      >
        ⚓ Zarpar
      </button>
      <button
        className={`${btnBase} bg-sky-600 hover:bg-sky-500 text-white`}
        disabled={isLoading || viaje.estadoActual === 'Amarrado'}
        onClick={() => onAmarrar(viaje.id)}
        title="Amarrar"
      >
        🔗 Amarrar
      </button>
      <button
        className={`${btnBase} bg-amber-600 hover:bg-amber-500 text-white`}
        disabled={isLoading || viaje.estadoActual === 'Fondeado'}
        onClick={() => onFondear(viaje.id)}
        title="Fondear"
      >
        🪝 Fondear
      </button>
      <button
        className={`${btnBase} bg-violet-600 hover:bg-violet-500 text-white`}
        disabled={isLoading}
        onClick={() => onReanudar(viaje.id)}
        title="Reanudar"
      >
        ▶ Reanudar
      </button>
    </div>
  );
}

// ─── Skeleton loader ──────────────────────────────────────────────────────────

function TableSkeleton({ rows = 6 }: { rows?: number }) {
  return (
    <>
      {Array.from({ length: rows }).map((_, i) => (
        <tr key={i} className="border-b border-slate-700/50">
          {Array.from({ length: 6 }).map((__, j) => (
            <td key={j} className="px-4 py-3">
              <div className="h-4 rounded bg-slate-700 animate-pulse" style={{ width: `${60 + Math.random() * 30}%` }} />
            </td>
          ))}
        </tr>
      ))}
    </>
  );
}

// ─── Toast notification ───────────────────────────────────────────────────────

interface ToastProps {
  message: string;
  type: 'success' | 'error';
  onClose: () => void;
}

function Toast({ message, type, onClose }: ToastProps) {
  const bg = type === 'success' ? 'bg-emerald-800 border-emerald-600' : 'bg-red-900 border-red-700';
  const icon = type === 'success' ? '✓' : '✕';
  return (
    <div className={`fixed bottom-6 right-6 z-50 flex items-center gap-3 rounded-lg border px-4 py-3 shadow-2xl text-sm text-white ${bg}`}>
      <span className="text-base font-bold">{icon}</span>
      <span>{message}</span>
      <button onClick={onClose} className="ml-2 opacity-60 hover:opacity-100 transition-opacity text-base leading-none">×</button>
    </div>
  );
}

// ─── Main Dashboard ───────────────────────────────────────────────────────────

const PAGE_SIZE = 10;

export default function ViajesDashboard() {
  const [page, setPage] = useState(1);
  const [toast, setToast] = useState<{ message: string; type: 'success' | 'error' } | null>(null);

  const showToast = (message: string, type: 'success' | 'error' = 'success') => {
    setToast({ message, type });
    setTimeout(() => setToast(null), 4000);
  };

  const { data, isLoading, isError, error } = useViajes(page, PAGE_SIZE);

  const mutationConfig = {
    onSuccess: (res: { mensaje: string }) => showToast(res.mensaje),
    onError: (err: Error) => showToast(err.message, 'error'),
  };

  const zarpar = useZarparViaje();
  const amarrar = useAmarrarViaje();
  const fondear = useFondearViaje();
  const reanudar = useReanudarViaje();

  const anyMutating =
    zarpar.isPending || amarrar.isPending || fondear.isPending || reanudar.isPending;

  const handleZarpar = (id: string) => zarpar.mutate(id, mutationConfig);
  const handleAmarrar = (id: string) => amarrar.mutate(id, mutationConfig);
  const handleFondear = (id: string) => fondear.mutate(id, mutationConfig);
  const handleReanudar = (id: string) => reanudar.mutate(id, mutationConfig);

  return (
    <div className="min-h-screen bg-slate-950 text-slate-100 font-sans p-6">
      {/* Header */}
      <div className="mb-6 flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold tracking-tight text-white">
            Gestión de Tráfico Marítimo
          </h1>
          <p className="mt-1 text-sm text-slate-400">
            Módulo de Viajes — vista operativa en tiempo real
          </p>
        </div>
        {anyMutating && (
          <div className="flex items-center gap-2 rounded-full bg-amber-900/40 border border-amber-700 px-4 py-1.5 text-sm text-amber-300">
            <span className="inline-block h-2 w-2 rounded-full bg-amber-400 animate-pulse" />
            Procesando acción…
          </div>
        )}
      </div>

      {/* Error banner */}
      {isError && (
        <div className="mb-4 rounded-lg border border-red-700 bg-red-900/40 px-4 py-3 text-sm text-red-300">
          <span className="font-semibold">Error al cargar viajes:</span>{' '}
          {error instanceof Error ? error.message : 'Error desconocido'}
        </div>
      )}

      {/* Table card */}
      <div className="rounded-xl border border-slate-700 bg-slate-900 shadow-xl overflow-hidden">
        <div className="overflow-x-auto">
          <table className="w-full text-sm">
            <thead>
              <tr className="border-b border-slate-700 bg-slate-800/80 text-xs uppercase tracking-wider text-slate-400">
                <th className="px-4 py-3 text-left font-semibold">Buque</th>
                <th className="px-4 py-3 text-left font-semibold">Ruta</th>
                <th className="px-4 py-3 text-left font-semibold">Fecha Inicio</th>
                <th className="px-4 py-3 text-left font-semibold">Estado</th>
                <th className="px-4 py-3 text-left font-semibold">Costera</th>
                <th className="px-4 py-3 text-left font-semibold">Acciones</th>
              </tr>
            </thead>
            <tbody>
              {isLoading ? (
                <TableSkeleton rows={PAGE_SIZE} />
              ) : !data || data.length === 0 ? (
                <tr>
                  <td colSpan={6} className="px-4 py-16 text-center text-slate-500">
                    <div className="flex flex-col items-center gap-2">
                      <span className="text-3xl">🚢</span>
                      <span>No se encontraron viajes registrados.</span>
                    </div>
                  </td>
                </tr>
              ) : (
                data?.map((viaje: ViajeDto) => (
                  <tr
                    key={viaje.id}
                    className="border-b border-slate-700/50 transition-colors hover:bg-slate-800/50"
                  >
                    <td className="px-4 py-3 font-semibold text-white">
                      {viaje.buque || 'N/D'}
                      {viaje.barcazas && viaje.barcazas.length > 0 && (
                        <span className="ml-2 text-xs text-slate-400">
                          +{viaje.barcazas.length} barcaza{viaje.barcazas.length > 1 ? 's' : ''}
                        </span>
                      )}
                    </td>
                    <td className="px-4 py-3 text-slate-300">{viaje.ruta || 'N/D'}</td>
                    <td className="px-4 py-3 text-slate-400 tabular-nums">
                      {viaje.fechaInicioFormateada || 'N/D'}
                    </td>
                    <td className="px-4 py-3">
                      <EstadoBadge estado={viaje.estadoActual} />
                    </td>
                    <td className="px-4 py-3 text-slate-400">
                      {viaje.costeraId ?? (
                        <span className="text-slate-600 italic">—</span>
                      )}
                    </td>
                    <td className="px-4 py-3">
                      <AccionesRow
                        viaje={viaje}
                        onZarpar={handleZarpar}
                        onAmarrar={handleAmarrar}
                        onFondear={handleFondear}
                        onReanudar={handleReanudar}
                        isLoading={anyMutating}
                      />
                    </td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>

        {data && (
          <div className="mt-4 flex gap-2 justify-end px-4 py-3 border-t border-slate-700">
            <button onClick={() => setPage(p => Math.max(1, p - 1))} disabled={page === 1} className="px-3 py-1 border border-slate-600 rounded disabled:opacity-50 hover:bg-slate-800">Anterior</button>
            <span className="px-3 py-1">Página {page}</span>
            <button onClick={() => setPage(p => p + 1)} disabled={data.length < PAGE_SIZE} className="px-3 py-1 border border-slate-600 rounded disabled:opacity-50 hover:bg-slate-800">Siguiente</button>
          </div>
        )}
      </div>

      {/* Toast */}
      {toast && (
        <Toast
          message={toast.message}
          type={toast.type}
          onClose={() => setToast(null)}
        />
      )}
    </div>
  );
}