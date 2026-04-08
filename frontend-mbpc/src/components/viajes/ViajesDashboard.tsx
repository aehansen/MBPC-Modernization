import React, { useState } from 'react';
import {
  useAmarrarViaje,
  useFondearViaje,
  useReanudarViaje,
  useViajes,
  useZarparViaje,
} from '../../hooks/useViajes';
import type { ViajeDto } from '../../types/viajes.types';
import ModalActualizarPosicion from './ModalActualizarPosicion';

const PAGE_SIZE = 10;

// ─── Estado badge ─────────────────────────────────────────────────────────────

const ESTADO_STYLES: Record<string, string> = {
  EnViaje: 'bg-green-100 text-green-800 border border-green-200',
  Amarrado: 'bg-blue-100 text-blue-800 border border-blue-200',
  Fondeado: 'bg-yellow-100 text-yellow-800 border border-yellow-200',
  Programado: 'bg-gray-100 text-gray-800 border border-gray-200',
};

function EstadoBadge({ estado }: { estado: string }) {
  const classes =
    ESTADO_STYLES[estado] ?? 'bg-gray-100 text-gray-800 border border-gray-200';
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
  onActualizarPosicion: (viaje: ViajeDto) => void;
  isLoading: boolean;
}

function AccionesRow({
  viaje,
  onZarpar,
  onAmarrar,
  onFondear,
  onReanudar,
  onActualizarPosicion,
  isLoading,
}: AccionesProps) {
  const btnBase =
    'px-3 py-1.5 rounded text-xs font-semibold tracking-wide transition-all duration-150 disabled:opacity-40 disabled:cursor-not-allowed';

  return (
    <div className="flex flex-wrap justify-end gap-1.5">
      <button
        className={`${btnBase} bg-green-50 text-green-700 border border-green-200 hover:bg-green-100`}
        disabled={isLoading || viaje.estadoActual === 'EnViaje'}
        onClick={() => onZarpar(viaje.id)}
        title="Zarpar"
      >
        Zarpar
      </button>
      <button
        className={`${btnBase} bg-blue-50 text-blue-700 border border-blue-200 hover:bg-blue-100`}
        disabled={isLoading || viaje.estadoActual === 'Amarrado'}
        onClick={() => onAmarrar(viaje.id)}
        title="Amarrar"
      >
        Amarrar
      </button>
      <button
        className={`${btnBase} bg-yellow-50 text-yellow-700 border border-yellow-200 hover:bg-yellow-100`}
        disabled={isLoading || viaje.estadoActual === 'Fondeado'}
        onClick={() => onFondear(viaje.id)}
        title="Fondear"
      >
        Fondear
      </button>
      <button
        className={`${btnBase} bg-gray-50 text-gray-700 border border-gray-200 hover:bg-gray-100`}
        disabled={isLoading || viaje.estadoActual !== 'Fondeado'}
        onClick={() => onReanudar(viaje.id)}
        title="Reanudar"
      >
        Reanudar
      </button>
      
      {/* NUEVO BOTÓN DE POSICIÓN */}
      <button
        className={`${btnBase} bg-[#002454] text-white border border-[#002454] hover:bg-[#104a8e]`}
        disabled={isLoading}
        onClick={() => onActualizarPosicion(viaje)}
        title="Actualizar Posición"
      >
        📍 Posición
      </button>
    </div>
  );
}

// ─── Main Component ───────────────────────────────────────────────────────────

export default function ViajesDashboard() {
  const [page, setPage] = useState(1);
  const { data, isLoading, isError, error } = useViajes(page, PAGE_SIZE);

  const mutZarpar = useZarparViaje();
  const mutAmarrar = useAmarrarViaje();
  const mutFondear = useFondearViaje();
  const mutReanudar = useReanudarViaje();

  const anyMutating =
    mutZarpar.isPending || mutAmarrar.isPending || mutFondear.isPending || mutReanudar.isPending;

  // Estado para controlar nuestro Modal de Posición
  const [modalPosicion, setModalPosicion] = useState<{ isOpen: boolean; viaje: ViajeDto | null }>({
    isOpen: false,
    viaje: null,
  });

  const handleZarpar = (id: string) => mutZarpar.mutate(id);
  const handleAmarrar = (id: string) => mutAmarrar.mutate(id);
  const handleFondear = (id: string) => mutFondear.mutate(id);
  const handleReanudar = (id: string) => mutReanudar.mutate(id);

  // Función que atrapa el viaje y abre el modal
  const handleAbrirPosicion = (viaje: ViajeDto) => {
    setModalPosicion({ isOpen: true, viaje });
  };

  return (
    <div className="bg-gray-50 text-gray-900 font-sans p-6 rounded-xl">
      {/* Header */}
      <div className="mb-6 flex items-center justify-between">
        <div>
          <h2 className="text-xl font-bold tracking-tight text-[#002454]">
            Grilla Operativa de Tráfico
          </h2>
          <p className="mt-1 text-sm text-gray-500">
            Módulo de Viajes — Acciones y posicionamiento
          </p>
        </div>
        {anyMutating && (
          <div className="flex items-center gap-2 rounded-full bg-blue-50 border border-blue-200 px-4 py-1.5 text-sm text-blue-700">
            <span className="inline-block h-2 w-2 rounded-full bg-blue-500 animate-pulse" />
            Procesando acción…
          </div>
        )}
      </div>

      {/* Error banner */}
      {isError && (
        <div className="mb-4 rounded-lg border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">
          <span className="font-semibold">Error al cargar viajes:</span>{' '}
          {error instanceof Error ? error.message : 'Error desconocido'}
        </div>
      )}

      {/* Table card */}
      <div className="rounded-xl border border-gray-200 bg-white shadow-sm overflow-hidden">
        <div className="overflow-x-auto">
          <table className="w-full text-sm text-left">
            <thead>
              <tr className="border-b border-gray-200 bg-[#002454] text-xs uppercase tracking-wider text-white">
                <th className="px-4 py-3 font-semibold">Buque</th>
                <th className="px-4 py-3 font-semibold">Ruta</th>
                <th className="px-4 py-3 font-semibold">Fecha Inicio</th>
                <th className="px-4 py-3 font-semibold">Estado</th>
                <th className="px-4 py-3 font-semibold">Costera</th>
                <th className="px-4 py-3 font-semibold text-right">Acciones</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-100">
              {isLoading ? (
                <tr>
                  <td colSpan={6} className="px-4 py-8 text-center text-gray-500">Cargando viajes...</td>
                </tr>
              ) : !data || data.length === 0 ? (
                <tr>
                  <td colSpan={6} className="px-4 py-16 text-center text-gray-500">
                    <div className="flex flex-col items-center gap-2">
                      <span className="text-3xl">🚢</span>
                      <span>No se encontraron viajes registrados.</span>
                    </div>
                  </td>
                </tr>
              ) : (
                data.map((viaje: ViajeDto) => (
                  <tr key={viaje.id} className="transition-colors hover:bg-gray-50">
                    <td className="px-4 py-3 font-semibold text-[#002454]">
                      {viaje.buque || 'N/D'}
                    </td>
                    <td className="px-4 py-3 text-gray-600">{viaje.ruta || 'N/D'}</td>
                    <td className="px-4 py-3 text-gray-500 tabular-nums">
                      {viaje.fechaInicioFormateada || 'N/D'}
                    </td>
                    <td className="px-4 py-3">
                      <EstadoBadge estado={viaje.estadoActual} />
                    </td>
                    <td className="px-4 py-3 text-gray-500">
                      {viaje.costeraId ?? <span className="italic">—</span>}
                    </td>
                    <td className="px-4 py-3 text-right">
                      <AccionesRow
                        viaje={viaje}
                        onZarpar={handleZarpar}
                        onAmarrar={handleAmarrar}
                        onFondear={handleFondear}
                        onReanudar={handleReanudar}
                        onActualizarPosicion={handleAbrirPosicion}
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
          <div className="flex gap-2 justify-end px-4 py-3 border-t border-gray-100 bg-gray-50">
            <button
              onClick={() => setPage(p => Math.max(1, p - 1))}
              disabled={page === 1}
              className="px-3 py-1 border border-gray-300 rounded text-gray-700 disabled:opacity-40 hover:bg-white transition-colors"
            >
              Anterior
            </button>
            <span className="px-3 py-1 text-gray-600 text-sm font-medium flex items-center">Página {page}</span>
            <button
              onClick={() => setPage(p => p + 1)}
              disabled={data.length < PAGE_SIZE}
              className="px-3 py-1 border border-gray-300 rounded text-gray-700 disabled:opacity-40 hover:bg-white transition-colors"
            >
              Siguiente
            </button>
          </div>
        )}
      </div>

      {/* Renderizado del Modal de Posición */}
      {modalPosicion.isOpen && modalPosicion.viaje && (
        <ModalActualizarPosicion
          viajeId={modalPosicion.viaje.id}
          nombreBuque={modalPosicion.viaje.buque}
          onClose={() => setModalPosicion({ isOpen: false, viaje: null })}
        />
      )}
    </div>
  );
}