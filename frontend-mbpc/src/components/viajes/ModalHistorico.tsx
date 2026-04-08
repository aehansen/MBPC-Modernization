// src/components/viajes/ModalHistorico.tsx
import { useState } from 'react';
import { useViajesHistoricos } from '../../hooks/useViajesApi';
import type { FiltroHistoricoDto } from '../../hooks/useViajesApi';
import type { ViajeHistoricoDto } from '../../types/viajes.types';

interface ModalHistoricoProps {
  onClose: () => void;
}

// ─── Estado badge (replicado para no acoplar al dashboard) ────────────────────

const ESTADO_STYLES: Record<string, string> = {
  EnViaje:    'bg-green-100 text-green-800 border border-green-200',
  Amarrado:   'bg-blue-100 text-blue-800 border border-blue-200',
  Fondeado:   'bg-yellow-100 text-yellow-800 border border-yellow-200',
  Programado: 'bg-gray-100 text-gray-800 border border-gray-200',
};

function EstadoBadge({ estado }: { estado: string }) {
  const classes =
    ESTADO_STYLES[estado] ?? 'bg-gray-100 text-gray-800 border border-gray-200';
  return (
    <span
      className={`inline-block rounded-full px-2.5 py-0.5 text-xs font-semibold tracking-wide ${classes}`}
    >
      {estado || 'Desconocido'}
    </span>
  );
}

// ─── Valores iniciales del formulario ─────────────────────────────────────────

const FILTROS_VACIOS: FiltroHistoricoDto = {
  nombre:    '',
  omi:       '',
  matricula: '',
  origen:    '',
  destino:   '',
  desde:     '',
  hasta:     '',
};

// ─── Componente principal ─────────────────────────────────────────────────────

export default function ModalHistorico({ onClose }: ModalHistoricoProps) {
  const [filtros, setFiltros] = useState<FiltroHistoricoDto>(FILTROS_VACIOS);

  // El hook se instancia con los filtros actuales del estado.
  // enabled:false garantiza que no hace fetch al montar.
  const { data, isFetching, isError, error, isSuccess, refetch } =
    useViajesHistoricos(filtros);

  const handleChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const { name, value } = e.target;
    setFiltros((prev) => ({ ...prev, [name]: value }));
  };

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    refetch();
  };

  const handleLimpiar = () => {
    setFiltros(FILTROS_VACIOS);
  };

  // Cierre al hacer clic fuera del panel
  const handleBackdropClick = (e: React.MouseEvent<HTMLDivElement>) => {
    if (e.target === e.currentTarget) onClose();
  };

  const resultados: ViajeHistoricoDto[] = data ?? [];

  return (
    <div
      className="fixed inset-0 z-50 flex items-center justify-center bg-black/60 backdrop-blur-sm p-4"
      onClick={handleBackdropClick}
    >
      <div className="relative flex flex-col w-full max-w-5xl max-h-[90vh] rounded-2xl shadow-2xl overflow-hidden bg-white">

        {/* ── Cabecera ────────────────────────────────────────────────────── */}
        <div className="flex items-center justify-between px-6 py-4 bg-[#002454] shrink-0">
          <div className="flex items-center gap-3">
            <svg className="w-5 h-5 text-white opacity-80" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth="2"
                d="M12 8v4l3 3m6-3a9 9 0 11-18 0 9 9 0 0118 0z" />
            </svg>
            <div>
              <h2 className="text-base font-bold text-white tracking-tight">
                Búsqueda de Viajes Históricos
              </h2>
              <p className="text-xs text-blue-200 mt-0.5">
                Consulta global sobre el histórico de Oracle
              </p>
            </div>
          </div>
          <button
            onClick={onClose}
            className="text-white opacity-60 hover:opacity-100 transition-opacity p-1 rounded-lg hover:bg-white/10"
            aria-label="Cerrar modal"
          >
            <svg className="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth="2" d="M6 18L18 6M6 6l12 12" />
            </svg>
          </button>
        </div>

        {/* ── Cuerpo con scroll ───────────────────────────────────────────── */}
        <div className="overflow-y-auto flex-1 px-6 py-5 space-y-6">

          {/* ── Formulario de filtros ──────────────────────────────────────── */}
          <form onSubmit={handleSubmit} className="space-y-4">
            <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-4">

              {/* Nombre */}
              <div className="flex flex-col gap-1">
                <label className="text-[10px] font-bold text-gray-500 uppercase tracking-wider">
                  Nombre del Buque
                </label>
                <input
                  type="text"
                  name="nombre"
                  value={filtros.nombre}
                  onChange={handleChange}
                  placeholder="Ej: Rio Paraná"
                  className="px-3 py-2 text-sm bg-gray-50 border border-gray-300 rounded-lg focus:ring-2 focus:ring-[#104a8e] focus:border-transparent focus:bg-white outline-none transition-all"
                />
              </div>

              {/* OMI */}
              <div className="flex flex-col gap-1">
                <label className="text-[10px] font-bold text-gray-500 uppercase tracking-wider">
                  Número OMI
                </label>
                <input
                  type="text"
                  name="omi"
                  value={filtros.omi}
                  onChange={handleChange}
                  placeholder="Ej: 9123456"
                  className="px-3 py-2 text-sm bg-gray-50 border border-gray-300 rounded-lg focus:ring-2 focus:ring-[#104a8e] focus:border-transparent focus:bg-white outline-none transition-all"
                />
              </div>

              {/* Matrícula */}
              <div className="flex flex-col gap-1">
                <label className="text-[10px] font-bold text-gray-500 uppercase tracking-wider">
                  Matrícula
                </label>
                <input
                  type="text"
                  name="matricula"
                  value={filtros.matricula}
                  onChange={handleChange}
                  placeholder="Ej: ARG-0012"
                  className="px-3 py-2 text-sm bg-gray-50 border border-gray-300 rounded-lg focus:ring-2 focus:ring-[#104a8e] focus:border-transparent focus:bg-white outline-none transition-all"
                />
              </div>

              {/* Origen */}
              <div className="flex flex-col gap-1">
                <label className="text-[10px] font-bold text-gray-500 uppercase tracking-wider">
                  Origen
                </label>
                <input
                  type="text"
                  name="origen"
                  value={filtros.origen}
                  onChange={handleChange}
                  placeholder="Ej: Buenos Aires"
                  className="px-3 py-2 text-sm bg-gray-50 border border-gray-300 rounded-lg focus:ring-2 focus:ring-[#104a8e] focus:border-transparent focus:bg-white outline-none transition-all"
                />
              </div>

              {/* Destino */}
              <div className="flex flex-col gap-1">
                <label className="text-[10px] font-bold text-gray-500 uppercase tracking-wider">
                  Destino
                </label>
                <input
                  type="text"
                  name="destino"
                  value={filtros.destino}
                  onChange={handleChange}
                  placeholder="Ej: Montevideo"
                  className="px-3 py-2 text-sm bg-gray-50 border border-gray-300 rounded-lg focus:ring-2 focus:ring-[#104a8e] focus:border-transparent focus:bg-white outline-none transition-all"
                />
              </div>

              {/* Separador visual para el grupo de fechas */}
              <div className="hidden lg:block" />

              {/* Desde */}
              <div className="flex flex-col gap-1">
                <label className="text-[10px] font-bold text-gray-500 uppercase tracking-wider">
                  Desde
                </label>
                <input
                  type="date"
                  name="desde"
                  value={filtros.desde}
                  onChange={handleChange}
                  className="px-3 py-2 text-sm bg-gray-50 border border-gray-300 rounded-lg focus:ring-2 focus:ring-[#104a8e] focus:border-transparent focus:bg-white outline-none transition-all"
                />
              </div>

              {/* Hasta */}
              <div className="flex flex-col gap-1">
                <label className="text-[10px] font-bold text-gray-500 uppercase tracking-wider">
                  Hasta
                </label>
                <input
                  type="date"
                  name="hasta"
                  value={filtros.hasta}
                  onChange={handleChange}
                  className="px-3 py-2 text-sm bg-gray-50 border border-gray-300 rounded-lg focus:ring-2 focus:ring-[#104a8e] focus:border-transparent focus:bg-white outline-none transition-all"
                />
              </div>
            </div>

            {/* Acciones del formulario */}
            <div className="flex items-center gap-3 pt-1">
              <button
                type="submit"
                disabled={isFetching}
                className="flex items-center gap-2 px-5 py-2 text-sm font-semibold rounded-lg text-white bg-[#002454] hover:bg-[#104a8e] disabled:opacity-50 disabled:cursor-not-allowed transition-colors"
              >
                {isFetching ? (
                  <>
                    <span className="inline-block h-3 w-3 rounded-full border-2 border-white border-t-transparent animate-spin" />
                    Buscando…
                  </>
                ) : (
                  <>
                    <svg className="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                      <path strokeLinecap="round" strokeLinejoin="round" strokeWidth="2"
                        d="M21 21l-4.35-4.35M17 11A6 6 0 115 11a6 6 0 0112 0z" />
                    </svg>
                    Buscar
                  </>
                )}
              </button>
              <button
                type="button"
                onClick={handleLimpiar}
                disabled={isFetching}
                className="px-4 py-2 text-sm font-medium rounded-lg border border-gray-300 text-gray-600 hover:bg-gray-100 disabled:opacity-50 transition-colors"
              >
                Limpiar filtros
              </button>
            </div>
          </form>

          {/* ── Error ─────────────────────────────────────────────────────── */}
          {isError && (
            <div className="rounded-lg border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">
              <span className="font-semibold">Error en la búsqueda:</span>{' '}
              {error instanceof Error ? error.message : 'Error desconocido'}
            </div>
          )}

          {/* ── Resultados ────────────────────────────────────────────────── */}
          {isSuccess && (
            <div>
              <div className="flex items-center justify-between mb-3">
                <h3 className="text-sm font-bold text-gray-700 uppercase tracking-wider">
                  Resultados
                </h3>
                <span className="text-xs text-gray-500">
                  {resultados.length === 0
                    ? 'Sin resultados'
                    : `${resultados.length} viaje${resultados.length !== 1 ? 's' : ''} encontrado${resultados.length !== 1 ? 's' : ''}`}
                </span>
              </div>

              <div className="rounded-xl border border-gray-200 bg-white shadow-sm overflow-hidden">
                <div className="overflow-x-auto">
                  <table className="w-full text-sm text-left">
                    <thead>
                      <tr className="border-b border-gray-200 bg-[#002454] text-xs uppercase tracking-wider text-white">
                        <th className="px-4 py-3 font-semibold">Buque</th>
                        <th className="px-4 py-3 font-semibold">OMI</th>
                        <th className="px-4 py-3 font-semibold">Matrícula</th>
                        <th className="px-4 py-3 font-semibold">Ruta</th>
                        <th className="px-4 py-3 font-semibold">Fecha Partida</th>
                        <th className="px-4 py-3 font-semibold">Estado</th>
                      </tr>
                    </thead>
                    <tbody className="divide-y divide-gray-100">
                      {resultados.length === 0 ? (
                        <tr>
                          <td colSpan={6} className="px-4 py-12 text-center text-gray-500">
                            <div className="flex flex-col items-center gap-2">
                              <span className="text-3xl">🔍</span>
                              <span>No se encontraron viajes para los filtros indicados.</span>
                            </div>
                          </td>
                        </tr>
                      ) : (
                        resultados.map((viaje) => (
                          <tr key={viaje.id} className="transition-colors hover:bg-gray-50">
                            <td className="px-4 py-3 font-semibold text-[#002454]">
                              {viaje.buque || 'N/D'}
                            </td>
                            <td className="px-4 py-3 text-gray-600 tabular-nums">
                              {viaje.omi || '—'}
                            </td>
                            <td className="px-4 py-3 text-gray-600">
                              {viaje.matricula || '—'}
                            </td>
                            <td className="px-4 py-3 text-gray-600">
                              {viaje.origen && viaje.destino
                                ? `${viaje.origen} → ${viaje.destino}`
                                : viaje.origen || viaje.destino || '—'}
                            </td>
                            <td className="px-4 py-3 text-gray-500 tabular-nums">
                              {viaje.fechaPartida || '—'}
                            </td>
                            <td className="px-4 py-3">
                              <EstadoBadge estado={viaje.estado} />
                            </td>
                          </tr>
                        ))
                      )}
                    </tbody>
                  </table>
                </div>
              </div>
            </div>
          )}
        </div>

        {/* ── Footer ──────────────────────────────────────────────────────── */}
        <div className="shrink-0 flex justify-end px-6 py-3 border-t border-gray-100 bg-gray-50">
          <button
            onClick={onClose}
            className="px-5 py-2 text-sm font-semibold rounded-lg border border-gray-300 text-gray-700 hover:bg-gray-100 transition-colors"
          >
            Cerrar
          </button>
        </div>
      </div>
    </div>
  );
}