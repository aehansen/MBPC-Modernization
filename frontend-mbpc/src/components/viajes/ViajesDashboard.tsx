import { useState, useEffect } from 'react';
import { useViajes } from '../../hooks/useViajesApi';
import type { ViajeDto } from '../../types/viajes.types';
import ModalActualizarPosicion from './ModalActualizarPosicion';
import { ModalPersonalExterno } from './ModalPersonalExterno';
import CargasModal from '../cargas/CargasModal';
import { BotonZarpar } from '../BotonZarpar';
import { BotonAmarrar, BotonFondear, BotonReanudar } from '../BotonesAccionViaje';
import { useFinalizar } from '../../hooks/useAccionesViaje';

const PAGE_SIZE = 10;

// ─── Estado badge ─────────────────────────────────────────────────────────────

const ESTADO_STYLES: Record<string, string> = {
  Navegando: 'bg-green-100 text-green-800 border border-green-200',
  Amarrado: 'bg-blue-100 text-blue-800 border border-blue-200',
  Fondeado: 'bg-yellow-100 text-yellow-800 border border-yellow-200',
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

// ─── Action buttons ───────────────────────────────────────────────────────────

interface AccionesProps {
  viaje: ViajeDto;
  onActualizarPosicion: (viaje: ViajeDto) => void;
  onAbrirPersonal: (viaje: ViajeDto) => void;
  onVerCargas: (viaje: ViajeDto, opts?: { readOnly?: boolean }) => void;
  onFinalizarViaje: (viaje: ViajeDto) => void;
}

function AccionesRow({
  viaje,
  onActualizarPosicion,
  onAbrirPersonal,
  onVerCargas,
  onFinalizarViaje,
}: AccionesProps) {
  const btnBase =
    'px-3 py-1.5 rounded text-xs font-semibold tracking-wide transition-all duration-150 disabled:opacity-40 disabled:cursor-not-allowed border';

  const esFinalizado = (viaje.estadoActual ?? '').toLowerCase() === 'finalizado';

  return (
    <div
      className="flex flex-wrap justify-end gap-1.5"
      // Evita que el clic en los botones de acción propague el evento
      // al <tr> padre y cambie el viajeSeleccionadoId involuntariamente.
      onClick={(e) => e.stopPropagation()}
    >
      {!esFinalizado && (
        <>
          <BotonZarpar viaje={viaje} />
          <BotonAmarrar viaje={viaje} />
          <BotonFondear viaje={viaje} />
          <BotonReanudar viaje={viaje} />
          <button
            className={`${btnBase} bg-red-600 text-white border-red-700 hover:bg-red-700`}
            onClick={() => onFinalizarViaje(viaje)}
            title="Finalizar Viaje Definitivamente"
          >
            🏁 Finalizar
          </button>
        </>
      )}
      <div className="flex items-center gap-2">
        <button
          type="button"
          className={`${btnBase} bg-[#002454] text-white border-[#002454] hover:bg-[#104a8e]`}
          onClick={() => onActualizarPosicion(viaje)}
          title="Actualizar Posición"
        >
          📍 Posición
        </button>
        <button
          type="button"
          onClick={() => onAbrirPersonal(viaje)}
          className={`${btnBase} inline-flex items-center justify-center p-2 min-h-[2rem] bg-teal-50 text-teal-800 border-teal-200 hover:bg-teal-100`}
          title="Gestionar Tripulación y Personal Externo"
        >
          <svg
            className="w-5 h-5 shrink-0"
            fill="none"
            stroke="currentColor"
            viewBox="0 0 24 24"
            xmlns="http://www.w3.org/2000/svg"
            aria-hidden="true"
          >
            <path
              strokeLinecap="round"
              strokeLinejoin="round"
              strokeWidth={2}
              d="M12 4.354a4 4 0 110 5.292M15 21H3v-1a6 6 0 0112 0v1zm0 0h6v-1a6 6 0 00-9-5.197M13 7a4 4 0 11-8 0 4 4 0 018 0z"
            />
          </svg>
          <span className="sr-only">Tripulación</span>
        </button>
      </div>
      <button
        className={`${btnBase} bg-indigo-50 text-indigo-700 border-indigo-200 hover:bg-indigo-100`}
        onClick={() => onVerCargas(viaje, { readOnly: esFinalizado })}
        title="Ver Cargas"
      >
        {esFinalizado ? '📦 Ver Cargas (Histórico)' : '📦 Cargas'}
      </button>
    </div>
  );
}

// ─── Estado modal genérico ────────────────────────────────────────────────────

interface ModalViajeState {
  isOpen: boolean;
  viaje: ViajeDto | null;
}

interface ModalCargasState extends ModalViajeState {
  readOnly?: boolean;
}

// ─── Main Component ───────────────────────────────────────────────────────────

interface ViajesDashboardProps {
  /** Viaje seleccionado en la grilla (controlado por ViajesPage). */
  selectedViajeId?: string | null;
  /** Notifica al padre cuando el usuario selecciona una fila. */
  onViajeSelected?: (viajeId: string | null) => void;
}

export default function ViajesDashboard({
  selectedViajeId: selectedViajeIdProp = null,
  onViajeSelected,
}: ViajesDashboardProps) {
  const [page, setPage] = useState(1);
  const [filtro, setFiltro] = useState('');
  const [debouncedFiltro, setDebouncedFiltro] = useState('');

  const [viajeSeleccionadoIdLocal, setViajeSeleccionadoIdLocal] = useState<string | null>(null);
  const viajeSeleccionadoId = onViajeSelected ? selectedViajeIdProp : viajeSeleccionadoIdLocal;

  const setViajeSeleccionadoId = (id: string | null) => {
    if (onViajeSelected) {
      onViajeSelected(id);
    } else {
      setViajeSeleccionadoIdLocal(id);
    }
  };

  // Debounce: espera 500ms tras el último cambio del filtro antes de disparar la query.
  // Cuando el valor debounced cambia, se resetea la página a 1 para evitar que el usuario
  // esté en una página que no existe para el nuevo criterio de búsqueda.
  useEffect(() => {
    const timer = setTimeout(() => {
      setDebouncedFiltro(filtro);
      setPage(1);
    }, 500);

    return () => clearTimeout(timer);
  }, [filtro]);

  const { data: dataPaginada, isLoading, isError, error } = useViajes(page, PAGE_SIZE, debouncedFiltro);

  const { mutate: finalizarViaje } = useFinalizar();

  const [modalPosicion, setModalPosicion] = useState<ModalViajeState>({
    isOpen: false,
    viaje: null,
  });

  const [modalCargas, setModalCargas] = useState<ModalCargasState>({
    isOpen: false,
    viaje: null,
    readOnly: false,
  });

  const [modalPersonal, setModalPersonal] = useState<ModalViajeState>({
    isOpen: false,
    viaje: null,
  });

  const handleAbrirPosicion = (viaje: ViajeDto) => {
    console.log('Abriendo posición para:', viaje.id);
    setModalPosicion({ isOpen: true, viaje });
  };
  const handleAbrirPersonal = (viaje: ViajeDto) => setModalPersonal({ isOpen: true, viaje });
  const handleAbrirCargas = (viaje: ViajeDto, opts?: { readOnly?: boolean }) =>
    setModalCargas({ isOpen: true, viaje, readOnly: opts?.readOnly ?? false });
  const handleFinalizarViaje = (viaje: ViajeDto) => finalizarViaje({ id: viaje.id });

  // Los datos ya vienen filtrados desde el servidor; no se aplica ningún .filter() local.
  const filas: ViajeDto[] = dataPaginada ?? [];

  return (
    <div className="bg-gray-50 text-gray-900 font-sans p-6 rounded-xl">
      {/* Header */}
      <div className="mb-6 flex items-center justify-between gap-4 flex-wrap">
        <div>
          <h2 className="text-xl font-bold tracking-tight text-[#002454]">
            Grilla Operativa de Tráfico
          </h2>
          <p className="mt-1 text-sm text-gray-500">
            Módulo de Viajes — Acciones y posicionamiento
          </p>
        </div>

        <div className="flex items-center gap-3 flex-wrap">
          {/* Filtro server-side por nombre de buque con debounce de 500ms */}
          <input
            type="text"
            placeholder="Filtrar por buque…"
            value={filtro}
            onChange={(e) => setFiltro(e.target.value)}
            className="px-3 py-2 text-sm bg-white border border-gray-300 rounded-lg focus:ring-2 focus:ring-[#104a8e] focus:border-transparent outline-none transition-all w-52"
          />
        </div>
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
                  <td colSpan={6} className="px-4 py-8 text-center text-gray-500">
                    Cargando viajes…
                  </td>
                </tr>
              ) : filas.length === 0 ? (
                <tr>
                  <td colSpan={6} className="px-4 py-16 text-center text-gray-500">
                    <div className="flex flex-col items-center gap-2">
                      <span className="text-3xl">🚢</span>
                      <span>
                        {debouncedFiltro.trim() !== ''
                          ? 'Sin resultados para el filtro aplicado.'
                          : 'No se encontraron viajes registrados.'}
                      </span>
                    </div>
                  </td>
                </tr>
              ) : (
                filas.map((viaje) => {
                  const isSeleccionado = viaje.id === viajeSeleccionadoId;
                  return (
                    <tr
                      key={viaje.id}
                      onClick={() => setViajeSeleccionadoId(viaje.id)}
                      className={[
                        'transition-colors cursor-pointer',
                        isSeleccionado
                          ? 'bg-blue-50 border-l-4 border-blue-600'
                          : 'hover:bg-gray-50 border-l-4 border-transparent',
                      ].join(' ')}
                    >
                      {/* Columna Buque: se usa viaje.buque (nombre real hidratado del DTO) */}
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
                          onActualizarPosicion={handleAbrirPosicion}
                          onAbrirPersonal={handleAbrirPersonal}
                          onVerCargas={handleAbrirCargas}
                          onFinalizarViaje={handleFinalizarViaje}
                        />
                      </td>
                    </tr>
                  );
                })
              )}
            </tbody>
          </table>
        </div>

        {/* Paginación */}
        {dataPaginada && (
          <div className="flex gap-2 justify-end px-4 py-3 border-t border-gray-100 bg-gray-50">
            <button
              onClick={() => setPage((p) => Math.max(1, p - 1))}
              disabled={page === 1}
              className="px-3 py-1 border border-gray-300 rounded text-gray-700 disabled:opacity-40 hover:bg-white transition-colors"
            >
              Anterior
            </button>
            <span className="px-3 py-1 text-gray-600 text-sm font-medium flex items-center">
              Página {page}
            </span>
            <button
              onClick={() => setPage((p) => p + 1)}
              disabled={dataPaginada.length < PAGE_SIZE}
              className="px-3 py-1 border border-gray-300 rounded text-gray-700 disabled:opacity-40 hover:bg-white transition-colors"
            >
              Siguiente
            </button>
          </div>
        )}
      </div>

      {/* Modal de Posición */}
      {modalPosicion.isOpen && modalPosicion.viaje && (
        <ModalActualizarPosicion
          isOpen={modalPosicion.isOpen}
          viajeId={modalPosicion.viaje.id}
          nombreBuque={modalPosicion.viaje.buque}
          onClose={() => setModalPosicion({ isOpen: false, viaje: null })}
        />
      )}

      {modalPersonal.isOpen && modalPersonal.viaje && (
        <ModalPersonalExterno
          isOpen={modalPersonal.isOpen}
          viajeId={modalPersonal.viaje.id}
          onClose={() => setModalPersonal({ isOpen: false, viaje: null })}
        />
      )}

      {/* Modal de Cargas */}
      {modalCargas.isOpen && modalCargas.viaje && (
        <CargasModal
          viajeId={modalCargas.viaje.id}
          viajeNombreBuque={modalCargas.viaje.buque}
          readOnly={modalCargas.readOnly}
          onClose={() => setModalCargas({ isOpen: false, viaje: null, readOnly: false })}
        />
      )}

    </div>
  );
}