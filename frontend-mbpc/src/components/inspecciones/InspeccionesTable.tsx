import React, { useState } from 'react';
import { useInspecciones, useEliminarInspeccion } from '../../hooks/useInspecciones';
import type { InspeccionDto } from '../../types/inspecciones.types';
import InspeccionModal from './InspeccionModal';

interface InspeccionesTableProps {
  viajeId?: string;
  readOnly?: boolean;
}

export default function InspeccionesTable({ viajeId, readOnly = false }: InspeccionesTableProps) {
  const { data: inspecciones = [], isLoading, error } = useInspecciones(viajeId);
  const { mutate: eliminarInspeccion } = useEliminarInspeccion(viajeId);

  const [selectedInspeccion, setSelectedInspeccion] = useState<InspeccionDto | null>(null);
  const [isModalOpen, setIsModalOpen] = useState(false);

  const handleEdit = (inspeccion: InspeccionDto) => {
    setSelectedInspeccion(inspeccion);
    setIsModalOpen(true);
  };

  const handleCreate = () => {
    setSelectedInspeccion(null);
    setIsModalOpen(true);
  };

  const handleDelete = (id: string, vId: string) => {
    if (window.confirm('¿Está seguro de que desea eliminar esta inspección?')) {
      eliminarInspeccion({ id, viajeId: vId });
    }
  };

  if (isLoading) {
    return (
      <div className="flex items-center justify-center p-6 text-slate-400">
        <span className="text-sm">Cargando inspecciones...</span>
      </div>
    );
  }

  if (error) {
    return (
      <div className="p-4 bg-red-900/30 border border-red-500/50 rounded-md text-red-200">
        <p className="text-sm">Ocurrió un error al cargar las inspecciones.</p>
      </div>
    );
  }

  return (
    <div className="bg-white rounded-2xl border border-slate-200/80 shadow-sm overflow-hidden">
      <div className="px-6 py-5 border-b border-slate-200/80 bg-slate-50/50 flex justify-between items-center">
        <div>
          <h2 className="text-lg font-bold text-slate-900">Inspecciones</h2>
          <p className="text-xs text-slate-500 mt-1">Registro histórico y estado de inspecciones</p>
        </div>
        {!readOnly && (
          <button
            onClick={handleCreate}
            className="px-4 py-2 bg-[#002454] hover:bg-[#0b3166] text-white rounded-lg text-sm font-semibold transition-all duration-200 border border-[#C8A84B]/40 hover:border-[#C8A84B] shadow-sm"
          >
            Nueva Inspección
          </button>
        )}
      </div>

      <div className="overflow-x-auto">
        <table className="min-w-full divide-y divide-slate-100 text-left">
          <thead className="bg-slate-50 text-slate-600 font-bold text-xs uppercase tracking-wider border-b border-slate-200">
            <tr>
              <th className="px-6 py-4 text-left font-semibold">Fecha</th>
              <th className="px-6 py-4 text-left font-semibold">Tipo</th>
              <th className="px-6 py-4 text-left font-semibold">Datos del Inspector</th>
              <th className="px-6 py-4 text-left font-semibold">Lugar</th>
              <th className="px-6 py-4 text-left font-semibold">Resultado</th>
              <th className="px-6 py-4 text-left font-semibold">Observaciones</th>
              {!readOnly && (
                <th className="px-6 py-4 text-center font-semibold">Acciones</th>
              )}
            </tr>
          </thead>
          <tbody className="bg-white divide-y divide-slate-100 text-sm text-slate-700">
            {inspecciones.length === 0 ? (
              <tr>
                <td colSpan={readOnly ? 6 : 7} className="px-6 py-8 text-center text-slate-500 text-sm italic">
                  No hay inspecciones registradas.
                </td>
              </tr>
            ) : (
              inspecciones.map((inspeccion) => (
                <tr key={inspeccion.id} className="hover:bg-slate-50/70 transition-colors duration-150">
                  <td className="px-6 py-4 whitespace-nowrap text-slate-500">
                    {new Date(inspeccion.fechaInspeccion).toLocaleDateString('es-AR')}
                  </td>
                  <td className="px-6 py-4 whitespace-nowrap font-semibold text-slate-900">
                    {inspeccion.tipoInspeccion}
                  </td>
                  <td className="px-6 py-4 whitespace-pre-wrap max-w-xs text-slate-600">
                    {inspeccion.inspectorDatos}
                  </td>
                  <td className="px-6 py-4 whitespace-nowrap text-slate-600">
                    {inspeccion.lugarInspeccion}
                  </td>
                  <td className="px-6 py-4 whitespace-nowrap">
                    <span
                      className={`inline-flex items-center gap-1.5 px-2.5 py-0.5 rounded-full text-xs font-semibold ${
                        inspeccion.resultado === 'Sin Deficiencias'
                          ? 'bg-green-50 text-green-700 border border-green-200'
                          : 'bg-red-50 text-red-700 border border-red-200'
                      }`}
                    >
                      <span
                        className={`w-1.5 h-1.5 rounded-full ${
                          inspeccion.resultado === 'Sin Deficiencias' ? 'bg-green-500' : 'bg-red-500'
                        }`}
                      />
                      {inspeccion.resultado}
                    </span>
                  </td>
                  <td className="px-6 py-4 max-w-xs truncate text-slate-500" title={inspeccion.observaciones}>
                    {inspeccion.observaciones || <span className="text-slate-400 italic">Sin observaciones</span>}
                  </td>
                  {!readOnly && (
                    <td className="px-6 py-4 whitespace-nowrap text-center">
                      <div className="flex justify-center items-center gap-3">
                        <button
                          onClick={() => handleEdit(inspeccion)}
                          className="text-blue-600 hover:text-blue-800 font-semibold transition-colors duration-150"
                        >
                          Editar
                        </button>
                        <span className="text-slate-300">|</span>
                        <button
                          onClick={() => handleDelete(inspeccion.id, inspeccion.viajeId)}
                          className="text-red-600 hover:text-red-800 font-semibold transition-colors duration-150"
                        >
                          Eliminar
                        </button>
                      </div>
                    </td>
                  )}
                </tr>
              ))
            )}
          </tbody>
        </table>
      </div>

      {isModalOpen && (
        <InspeccionModal
          isOpen={isModalOpen}
          inspeccion={selectedInspeccion}
          viajeId={viajeId}
          onClose={() => {
            setIsModalOpen(false);
            setSelectedInspeccion(null);
          }}
        />
      )}
    </div>
  );
}
