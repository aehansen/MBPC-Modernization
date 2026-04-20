import React, { useState, useEffect, useRef } from 'react';
import {
  useCargas,
  useCrearCarga,
  useAmarrarCarga,
  useFondearCarga,
  useCargarToneladas,
  useDescargarToneladas,
} from '../../hooks/useCargasApi';
import type { TipoCarga } from '../../types/cargas.types';

// Importamos los modales de edición y eliminación generados
import CargaEditModal from './CargaEditModal';
import CargaDeleteModal from './CargaDeleteModal';

// ─── Tipos Locales ────────────────────────────────────────────────────────────
interface AutocompleteBarcaza {
  idBuque: number;
  nombre: string;
  matricula: string;
  omi: string;
  tipo: string;
}

interface CargasModalProps {
  viajeId: string;
  viajeNombreBuque: string;
  onClose: () => void;
}

export default function CargasModal({ viajeId, viajeNombreBuque, onClose }: CargasModalProps) {
  // Extraemos refetch para poder recargar la grilla tras editar/eliminar
  const { data: cargas = [], isLoading, refetch } = useCargas(viajeId);
  const { mutate: crearCarga, isPending: isCreando } = useCrearCarga();
  const { mutate: amarrar } = useAmarrarCarga(viajeId);
  const { mutate: fondear } = useFondearCarga(viajeId);
  const { mutate: cargarTon } = useCargarToneladas(viajeId);
  const { mutate: descargarTon } = useDescargarToneladas(viajeId);

  const [showForm, setShowForm] = useState(false);
  const [tipo, setTipo] = useState<TipoCarga>('Barcaza');
  const [tonelaje, setTonelaje] = useState<number>(0);

  // ─── Estados para el Autocompletado de Barcaza ──────────────────────────────
  const [barcazaSearch, setBarcazaSearch] = useState('');
  const [barcazaId, setBarcazaId] = useState<number | null>(null);
  const [suggestions, setSuggestions] = useState<AutocompleteBarcaza[]>([]);
  const [showDropdown, setShowDropdown] = useState(false);
  const [loadingBusqueda, setLoadingBusqueda] = useState(false);
  const dropdownRef = useRef<HTMLDivElement>(null);

  // ─── Estados para los Modales ABM (Editar/Eliminar) ─────────────────────────
  const [cargaSeleccionada, setCargaSeleccionada] = useState<any>(null);
  const [modalAbierto, setModalAbierto] = useState<'editar' | 'eliminar' | null>(null);

  const abrirEditar = (carga: any) => {
    setCargaSeleccionada(carga);
    setModalAbierto('editar');
  };

  const abrirEliminar = (carga: any) => {
    setCargaSeleccionada(carga);
    setModalAbierto('eliminar');
  };

  const cerrarModalAbm = () => {
    setModalAbierto(null);
    setCargaSeleccionada(null);
  };

  const handleSuccessAbm = () => {
    cerrarModalAbm();
    if (refetch) refetch(); // Recargamos los datos desde Mongo/Oracle
  };

  // ─── FIX: Race Condition en handleClickOutside ────────────────────────────
  useEffect(() => {
    if (!showDropdown) return;

    const handleClickOutside = (event: MouseEvent) => {
      if (dropdownRef.current && !dropdownRef.current.contains(event.target as Node)) {
        setShowDropdown(false);
      }
    };
    document.addEventListener('mousedown', handleClickOutside);
    return () => document.removeEventListener('mousedown', handleClickOutside);
  }, [showDropdown]);

  // Efecto Debounce para buscar barcazas con token JWT
  useEffect(() => {
    const delayDebounceFn = setTimeout(async () => {
      if (barcazaSearch.trim().length >= 2) {
        setLoadingBusqueda(true);
        try {
          const token = localStorage.getItem("mbpc_token");

          const res = await fetch(
            `/api/buques/barcazas/autocomplete?etapaId=${viajeId}&query=${encodeURIComponent(barcazaSearch)}`,
            {
              headers: {
                Authorization: `Bearer ${token}`,
              },
            }
          );
          if (res.ok) {
            const data: AutocompleteBarcaza[] = await res.json();
            setSuggestions(data);
            setShowDropdown(true);
          }
        } catch (error) {
          console.error('Error buscando barcazas:', error);
        } finally {
          setLoadingBusqueda(false);
        }
      } else {
        setSuggestions([]);
        setShowDropdown(false);
      }
    }, 400);

    return () => clearTimeout(delayDebounceFn);
  }, [barcazaSearch, viajeId]);

  const handleCrear = (e: React.FormEvent) => {
    e.preventDefault();
    if (tipo === 'Barcaza' && !barcazaId) {
      alert('Por favor, busque y seleccione una barcaza/buque de la lista.');
      return;
    }

    // Si es Bodega, mandamos 0 (nuestra convención). Si es Barcaza, el ID que eligió.
    const payloadBarcazaId = tipo === 'Bodega' ? 0 : barcazaId;

    crearCarga(
      {
        nombreBuque: viajeNombreBuque,
        body: {
          barcazaId: payloadBarcazaId, // Enviamos el ID procesado
          tipo,
          tonelaje
        }
      },
      {
        onSuccess: () => {
          setBarcazaId(null);
          setBarcazaSearch('');
          setTonelaje(0);
          setShowDropdown(false);
          setShowForm(false);

          // Recargamos la grilla para ver la bodega/barcaza nueva
          if (refetch) refetch();
        },
      }
    );
  };

  const handleAccion = (accion: 'amarrar' | 'fondear' | 'cargar' | 'descargar', id: string) => {
    if (accion === 'amarrar') {
      const muelle = window.prompt('Ingrese el nuevo muelle:');
      if (muelle) amarrar({ id, nuevoMuelle: muelle });
    } else if (accion === 'fondear') {
      const zona = window.prompt('Ingrese la zona de fondeo:');
      if (zona) fondear({ id, zonaFondeo: zona });
    } else if (accion === 'cargar') {
      const val = window.prompt('Ingrese toneladas a cargar:');
      const ton = parseFloat(val || '');
      if (!isNaN(ton) && ton > 0) cargarTon({ id, toneladas: ton });
      else if (val) alert('Valor inválido');
    } else if (accion === 'descargar') {
      const val = window.prompt('Ingrese toneladas a descargar:');
      const ton = parseFloat(val || '');
      if (!isNaN(ton) && ton > 0) descargarTon({ id, toneladas: ton });
      else if (val) alert('Valor inválido');
    }
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black bg-opacity-50">
      <div className="bg-white rounded-lg shadow-xl w-full max-w-4xl max-h-[90vh] flex flex-col">
        {/* Header */}
        <div className="px-6 py-4 border-b border-gray-200 flex justify-between items-center bg-[#002454] text-white rounded-t-lg shrink-0">
          <h2 className="text-lg font-bold">Cargas del viaje: {viajeNombreBuque}</h2>
          <button onClick={onClose} className="text-white hover:text-gray-300 font-bold text-2xl leading-none">&times;</button>
        </div>

        {/* Content */}
        <div className="p-6 overflow-y-auto flex-1">
          <button
            onClick={() => setShowForm(!showForm)}
            className="mb-4 text-[#104a8e] font-semibold flex items-center hover:underline"
          >
            {showForm ? '▼ Ocultar Formulario' : '▶ Agregar Nueva Carga'}
          </button>

          {showForm && (
            <form onSubmit={handleCrear} className="mb-6 bg-gray-50 p-4 rounded border border-gray-200 flex gap-4 items-end">
              <div className="flex-1">
                <label className="block text-sm font-medium text-gray-700">Tipo</label>
                <select
                  value={tipo}
                  onChange={e => {
                    setTipo(e.target.value as TipoCarga);
                    // Limpiamos la búsqueda al cambiar de tipo
                    setBarcazaId(null);
                    setBarcazaSearch('');
                  }}
                  className="mt-1 block w-full rounded border-gray-300 shadow-sm p-2 border bg-white focus:outline-none focus:ring-1 focus:ring-[#104a8e] focus:border-[#104a8e]"
                >
                  <option value="Barcaza">Barcaza</option>
                  <option value="Bodega">Bodega</option>
                </select>
              </div>

              {/* Autocomplete Barcaza - SOLO SE MUESTRA SI ES BARCAZA */}
              {tipo === 'Barcaza' && (
                <div className="flex-1 relative" ref={dropdownRef}>
                  <label className="block text-sm font-medium text-gray-700">Buscar Barcaza</label>
                  <input
                    type="text"
                    required={tipo === 'Barcaza'}
                    autoComplete="off"
                    placeholder="Buscar por nombre, matrícula..."
                    value={barcazaSearch}
                    onChange={(e) => {
                      setBarcazaSearch(e.target.value);
                      setBarcazaId(null);
                    }}
                    onFocus={() => {
                      if (suggestions.length > 0) setShowDropdown(true);
                    }}
                    className={`mt-1 block w-full rounded shadow-sm p-2 border focus:outline-none focus:ring-1 focus:ring-[#104a8e] focus:border-[#104a8e] ${!barcazaId && barcazaSearch.length > 0 ? 'border-red-400' : 'border-gray-300'
                      }`}
                  />

                  {/* Dropdown de Resultados */}
                  {showDropdown && (
                    <div className="absolute z-50 w-full mt-1 bg-white border border-gray-200 rounded-md shadow-lg max-h-60 overflow-y-auto">
                      {loadingBusqueda ? (
                        <div className="px-4 py-3 text-sm text-gray-500">Buscando...</div>
                      ) : suggestions.length > 0 ? (
                        <ul className="py-1">
                          {suggestions.map((b) => (
                            <li
                              key={b.idBuque}
                              className="px-4 py-2 hover:bg-[#104a8e] hover:text-white cursor-pointer transition-colors"
                              onClick={() => {
                                setBarcazaId(b.idBuque);
                                setBarcazaSearch(b.nombre);
                                setShowDropdown(false);
                              }}
                            >
                              <div className="text-sm font-semibold">{b.nombre}</div>
                              <div className="text-xs opacity-80 mt-0.5">
                                OMI: {b.omi || '-'} | Mat: {b.matricula || '-'} | {b.tipo}
                              </div>
                            </li>
                          ))}
                        </ul>
                      ) : barcazaSearch.trim().length >= 2 ? (
                        <div className="px-4 py-3 text-sm text-gray-500">Sin resultados.</div>
                      ) : null}
                    </div>
                  )}
                </div>
              )}

              <div className="flex-1">
                <label className="block text-sm font-medium text-gray-700">Tonelaje Total</label>
                <input
                  type="number"
                  step="0.01"
                  min="0"
                  required
                  value={tonelaje}
                  onChange={e => setTonelaje(parseFloat(e.target.value))}
                  className="mt-1 block w-full rounded border-gray-300 shadow-sm p-2 border focus:outline-none focus:ring-1 focus:ring-[#104a8e] focus:border-[#104a8e]"
                />
              </div>

              <button type="submit" disabled={isCreando} className="bg-[#104a8e] text-white px-4 py-2 rounded font-medium hover:bg-[#002454] transition-colors disabled:opacity-50">
                {isCreando ? 'Guardando...' : 'Guardar'}
              </button>
            </form>
          )}

          {isLoading ? (
            <div className="text-center py-4 text-gray-500">Cargando datos...</div>
          ) : (
            <div className="overflow-x-auto border border-gray-200 rounded">
              <table className="min-w-full divide-y divide-gray-200">
                <thead className="bg-[#002454] text-white sticky top-0">
                  <tr>
                    <th className="px-4 py-3 text-left text-xs font-semibold uppercase">Descripción</th>
                    <th className="px-4 py-3 text-left text-xs font-semibold uppercase">Riesgo</th>
                    <th className="px-4 py-3 text-left text-xs font-semibold uppercase">Muelle / Zona</th>
                    <th className="px-4 py-3 text-right text-xs font-semibold uppercase">Tonelaje</th>
                    <th className="px-4 py-3 text-center text-xs font-semibold uppercase">Acciones</th>
                  </tr>
                </thead>
                <tbody className="bg-white divide-y divide-gray-200">
                  {cargas.length === 0 && (
                    <tr><td colSpan={5} className="px-4 py-8 text-center text-gray-500 font-medium">No hay cargas registradas para este viaje.</td></tr>
                  )}
                  {cargas.map(carga => (
                    <tr key={carga.id} className="hover:bg-gray-50">
                      <td className="px-4 py-3 text-sm text-gray-900 font-medium">{carga.descripcionLista}</td>
                      <td className="px-4 py-3 text-sm text-gray-900">
                        <span className={`px-2 py-1 text-xs rounded-full font-medium ${carga.nivelRiesgo === 'Alto' ? 'bg-red-100 text-red-800' : 'bg-gray-100 text-gray-800'}`}>
                          {carga.nivelRiesgo}
                        </span>
                      </td>
                      <td className="px-4 py-3 text-sm text-gray-900">{carga.muelleActual || '-'}</td>
                      <td className="px-4 py-3 text-sm text-right font-medium">{carga.tonelaje} t</td>
                      <td className="px-4 py-3 text-sm text-center">
                        <div className="flex justify-center items-center gap-3">
                          <button onClick={() => handleAccion('amarrar', carga.id)} className="text-blue-600 hover:text-blue-800 text-xs font-bold transition-colors">Amarrar</button>
                          <button onClick={() => handleAccion('fondear', carga.id)} className="text-yellow-600 hover:text-yellow-800 text-xs font-bold transition-colors">Fondear</button>
                          <button onClick={() => handleAccion('cargar', carga.id)} className="text-green-600 hover:text-green-800 text-xs font-bold transition-colors">Cargar</button>
                          <button onClick={() => handleAccion('descargar', carga.id)} className="text-red-600 hover:text-red-800 text-xs font-bold transition-colors">Descargar</button>

                          {/* Separador */}
                          <div className="w-px h-4 bg-gray-300 mx-1"></div>

                          {/* Nuevas Acciones ABM */}
                          <button onClick={() => abrirEditar(carga)} className="text-sky-600 hover:text-sky-800 text-xs font-bold transition-colors">Editar</button>
                          <button onClick={() => abrirEliminar(carga)} className="text-red-600 hover:text-red-900 text-xs font-bold transition-colors">Eliminar</button>
                        </div>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </div>

        {/* Footer */}
        <div className="px-6 py-4 bg-gray-50 border-t border-gray-200 flex justify-end rounded-b-lg shrink-0">
          <button onClick={onClose} className="px-4 py-2 bg-gray-200 text-gray-800 rounded hover:bg-gray-300 font-medium transition-colors">Cerrar</button>
        </div>
      </div>

      {/* ── Modales Inyectados ── */}
      {modalAbierto === 'editar' && cargaSeleccionada && (
        <CargaEditModal
          carga={cargaSeleccionada}
          onClose={cerrarModalAbm}
          onSuccess={handleSuccessAbm}
        />
      )}

      {modalAbierto === 'eliminar' && cargaSeleccionada && (
        <CargaDeleteModal
          carga={cargaSeleccionada}
          onClose={cerrarModalAbm}
          onSuccess={handleSuccessAbm}
        />
      )}
    </div>
  );
}