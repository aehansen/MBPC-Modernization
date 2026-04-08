import React, { useState } from 'react';
import {
  useCargas,
  useCrearCarga,
  useAmarrarCarga,
  useFondearCarga,
  useCargarToneladas,
  useDescargarToneladas,
} from '../../hooks/useCargasApi';
import type { TipoCarga } from '../../types/cargas.types';

interface CargasModalProps {
  viajeId: string;
  viajeNombreBuque: string;
  onClose: () => void;
}

export default function CargasModal({ viajeId, viajeNombreBuque, onClose }: CargasModalProps) {
  const { data: cargas = [], isLoading } = useCargas(viajeId);
  const { mutate: crearCarga, isPending: isCreando } = useCrearCarga();
  const { mutate: amarrar } = useAmarrarCarga(viajeId);
  const { mutate: fondear } = useFondearCarga(viajeId);
  const { mutate: cargarTon } = useCargarToneladas(viajeId);
  const { mutate: descargarTon } = useDescargarToneladas(viajeId);

  const [showForm, setShowForm] = useState(false);
  const [nombre, setNombre] = useState('');
  const [tipo, setTipo] = useState<TipoCarga>('Barcaza');
  const [tonelaje, setTonelaje] = useState<number>(0);

  const handleCrear = (e: React.FormEvent) => {
    e.preventDefault();
    crearCarga(
      { nombreBuque: viajeNombreBuque, body: { nombre, tipo, tonelaje } },
      {
        onSuccess: () => {
          setNombre('');
          setTonelaje(0);
          setShowForm(false);
        }
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
        <div className="px-6 py-4 border-b border-gray-200 flex justify-between items-center bg-[#002454] text-white rounded-t-lg">
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
                <label className="block text-sm font-medium text-gray-700">Nombre / Identificador</label>
                <input type="text" required value={nombre} onChange={e => setNombre(e.target.value)} className="mt-1 block w-full rounded border-gray-300 shadow-sm p-2 border" />
              </div>
              <div className="flex-1">
                <label className="block text-sm font-medium text-gray-700">Tipo</label>
                <select value={tipo} onChange={e => setTipo(e.target.value as TipoCarga)} className="mt-1 block w-full rounded border-gray-300 shadow-sm p-2 border bg-white">
                  <option value="Barcaza">Barcaza</option>
                  <option value="Bodega">Bodega</option>
                </select>
              </div>
              <div className="flex-1">
                <label className="block text-sm font-medium text-gray-700">Tonelaje Total</label>
                <input type="number" step="0.01" min="0" required value={tonelaje} onChange={e => setTonelaje(parseFloat(e.target.value))} className="mt-1 block w-full rounded border-gray-300 shadow-sm p-2 border" />
              </div>
              <button type="submit" disabled={isCreando} className="bg-[#104a8e] text-white px-4 py-2 rounded font-medium hover:bg-[#002454] transition-colors">
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
                        <div className="flex justify-center gap-3">
                          <button onClick={() => handleAccion('amarrar', carga.id)} className="text-blue-600 hover:text-blue-800 text-xs font-bold transition-colors">Amarrar</button>
                          <button onClick={() => handleAccion('fondear', carga.id)} className="text-yellow-600 hover:text-yellow-800 text-xs font-bold transition-colors">Fondear</button>
                          <button onClick={() => handleAccion('cargar', carga.id)} className="text-green-600 hover:text-green-800 text-xs font-bold transition-colors">Cargar</button>
                          <button onClick={() => handleAccion('descargar', carga.id)} className="text-red-600 hover:text-red-800 text-xs font-bold transition-colors">Descargar</button>
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
        <div className="px-6 py-4 bg-gray-50 border-t border-gray-200 flex justify-end rounded-b-lg">
          <button onClick={onClose} className="px-4 py-2 bg-gray-200 text-gray-800 rounded hover:bg-gray-300 font-medium transition-colors">Cerrar</button>
        </div>
      </div>
    </div>
  );
}