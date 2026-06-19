import React, { useState, useEffect } from 'react';
import { useQueryBuilderMetadata, useEjecutarConsulta, useExportarConsultaExcel } from '../../hooks/useQueryBuilder';
import { MetadataEntity, QueryFilter, QueryRequest, MetadataField } from '../../types/queryBuilder';
import { toast } from 'react-hot-toast';

export default function QueryBuilderPanel() {
  const { data: metadata = [], isLoading: cargandoMetadata, error: errorMetadata } = useQueryBuilderMetadata();
  const ejecutarMutation = useEjecutarConsulta();
  const exportarMutation = useExportarConsultaExcel();

  // Estados del Builder
  const [entidadSeleccionada, setEntidadSeleccionada] = useState<string>('');
  const [columnasSeleccionadas, setColumnasSeleccionadas] = useState<string[]>([]);
  const [filtros, setFiltros] = useState<QueryFilter[]>([]);

  // Listado de campos disponibles (entidad principal + entidades vinculadas por Join)
  const [camposDisponibles, setCamposDisponibles] = useState<MetadataField[]>([]);

  // Notificación de error en metadatos
  useEffect(() => {
    if (errorMetadata) {
      toast.error(`Error al cargar la metadata del visual query builder: ${errorMetadata.mensaje}`);
    }
  }, [errorMetadata]);

  // Al seleccionar la entidad principal, configuramos los campos y columnas por defecto
  useEffect(() => {
    if (!entidadSeleccionada) {
      setCamposDisponibles([]);
      setColumnasSeleccionadas([]);
      setFiltros([]);
      return;
    }

    const principal = metadata.find(e => e.name.toLowerCase() === entidadSeleccionada.toLowerCase());
    if (!principal) return;

    // Campos de la entidad principal
    const campos = [...principal.fields];

    // Buscar si hay entidades que se puedan unir (Joins)
    metadata.forEach(e => {
      if (e.name.toLowerCase() === entidadSeleccionada.toLowerCase()) return;

      // Si la entidad secundaria tiene un join que apunta a la principal
      const tieneJoin = e.joins?.some(j => j.targetEntity.toLowerCase() === entidadSeleccionada.toLowerCase());
      if (tieneJoin) {
        // Añadir campos de la entidad secundaria
        e.fields.forEach(f => {
          // Para evitar colisiones de nombres lógicos, validamos o los agregamos
          if (!campos.some(c => c.name.toLowerCase() === f.name.toLowerCase())) {
            campos.push(f);
          }
        });
      }
    });

    setCamposDisponibles(campos);
    
    // Seleccionar por defecto las primeras 4 columnas
    const colsDefecto = principal.fields.slice(0, 4).map(f => f.name);
    setColumnasSeleccionadas(colsDefecto);
    
    // Limpiar filtros al cambiar de entidad
    setFiltros([]);
  }, [entidadSeleccionada, metadata]);

  // Manejadores de Columnas
  const handleColumnaToggle = (colName: string) => {
    if (columnasSeleccionadas.includes(colName)) {
      setColumnasSeleccionadas(columnasSeleccionadas.filter(c => c !== colName));
    } else {
      setColumnasSeleccionadas([...columnasSeleccionadas, colName]);
    }
  };

  // Manejadores de Filtros
  const agregarFiltro = () => {
    if (camposDisponibles.length === 0) return;
    setFiltros([...filtros, { campo: camposDisponibles[0].name, operador: 'EQUALS', valor: '' }]);
  };

  const eliminarFiltro = (index: number) => {
    setFiltros(filtros.filter((_, i) => i !== index));
  };

  const actualizarFiltro = (index: number, key: keyof QueryFilter, value: string) => {
    const nuevosFiltros = [...filtros];
    nuevosFiltros[index] = { ...nuevosFiltros[index], [key]: value };
    setFiltros(nuevosFiltros);
  };

  // Obtener operadores sugeridos según el tipo de campo
  const obtenerOperadoresParaCampo = (campoName: string) => {
    const campo = camposDisponibles.find(c => c.name === campoName);
    if (!campo) return [{ value: 'EQUALS', label: 'Igual a' }];

    const type = campo.type.toUpperCase();
    if (type === 'STRING') {
      return [
        { value: 'EQUALS', label: 'Igual a' },
        { value: 'CONTAINS', label: 'Contiene' },
        { value: 'STARTS_WITH', label: 'Empieza con' }
      ];
    } else if (type === 'NUMERIC') {
      return [
        { value: 'EQUALS', label: 'Igual a' },
        { value: 'GREATER_THAN', label: 'Mayor que' },
        { value: 'LESS_THAN', label: 'Menor que' }
      ];
    } else if (type === 'DATETIME' || type === 'DATE') {
      return [
        { value: 'EQUALS', label: 'Igual a' },
        { value: 'GREATER_THAN', label: 'Posterior a' },
        { value: 'LESS_THAN', label: 'Anterior a' }
      ];
    }

    return [{ value: 'EQUALS', label: 'Igual a' }];
  };

  // Ejecución del Query
  const handleBuscar = () => {
    if (!entidadSeleccionada) {
      toast.error('Debe seleccionar una entidad para iniciar la consulta.');
      return;
    }
    if (columnasSeleccionadas.length === 0) {
      toast.error('Debe seleccionar al menos una columna para mostrar.');
      return;
    }

    const request: QueryRequest = {
      entidadPrincipal: entidadSeleccionada,
      columnas: columnasSeleccionadas,
      filtros: filtros.filter(f => f.valor.trim() !== '')
    };

    ejecutarMutation.mutate(request, {
      onSuccess: () => {
        toast.success('Consulta procesada con éxito.');
      },
      onError: (err) => {
        toast.error(`Error al compilar consulta: ${err.mensaje}`);
      }
    });
  };

  // Exportar Excel
  const handleExportarExcel = () => {
    if (!entidadSeleccionada) return;

    const request: QueryRequest = {
      entidadPrincipal: entidadSeleccionada,
      columnas: columnasSeleccionadas,
      filtros: filtros.filter(f => f.valor.trim() !== '')
    };

    exportarMutation.mutate(request, {
      onSuccess: () => {
        toast.success('Archivo Excel descargado con éxito.');
      },
      onError: (err) => {
        toast.error(`Fallo en exportación: ${err.mensaje}`);
      }
    });
  };

  return (
    <div className="space-y-6">
      {/* Panel de Configuración de la Consulta */}
      <div className="bg-white rounded-2xl border border-slate-200/80 shadow-sm p-6 space-y-6">
        
        {/* Fila 1: Selección de Entidad Principal */}
        <div className="flex flex-col max-w-md">
          <label className="text-xs font-bold uppercase tracking-wider text-slate-500 mb-2">
            Entidad Principal de Consulta
          </label>
          {cargandoMetadata ? (
            <div className="h-10 w-full bg-slate-100 rounded-lg animate-pulse" />
          ) : (
            <select
              value={entidadSeleccionada}
              onChange={(e) => setEntidadSeleccionada(e.target.value)}
              className="w-full px-4 py-2.5 text-sm bg-white border border-slate-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500 text-slate-900 shadow-sm transition-all font-semibold"
            >
              <option value="">-- Seleccionar Entidad Principal --</option>
              {metadata.map(e => (
                <option key={e.name} value={e.name}>{e.name}</option>
              ))}
            </select>
          )}
        </div>

        {entidadSeleccionada && (
          <>
            {/* Fila 2: Selector de Columnas a Visualizar */}
            <div className="space-y-2 border-t border-slate-100 pt-4">
              <label className="text-xs font-bold uppercase tracking-wider text-slate-500">
                Selección de Columnas (SELECT)
              </label>
              <p className="text-xs text-slate-400">Marque las columnas que desea incluir en el listado de resultados.</p>
              <div className="grid grid-cols-2 sm:grid-cols-3 md:grid-cols-4 lg:grid-cols-5 gap-3 mt-3">
                {camposDisponibles.map(f => {
                  const esDeJoin = !metadata
                    .find(e => e.name.toLowerCase() === entidadSeleccionada.toLowerCase())
                    ?.fields.some(x => x.name.toLowerCase() === f.name.toLowerCase());

                  return (
                    <button
                      key={f.name}
                      type="button"
                      onClick={() => handleColumnaToggle(f.name)}
                      className={`
                        flex items-center gap-2.5 p-3 rounded-xl border text-left text-xs font-medium transition-all duration-150 shadow-sm
                        ${columnasSeleccionadas.includes(f.name)
                          ? 'bg-blue-50 border-blue-500 text-blue-700 font-bold'
                          : 'bg-white hover:bg-slate-50 border-slate-200 text-slate-600'
                        }
                      `}
                    >
                      <input
                        type="checkbox"
                        checked={columnasSeleccionadas.includes(f.name)}
                        onChange={() => {}} // Manejado por el button click
                        className="rounded border-slate-300 text-blue-600 focus:ring-blue-500 pointer-events-none"
                      />
                      <span className="truncate">
                        {f.label}
                        {esDeJoin && (
                          <span className="block text-[9px] text-slate-400 font-normal">Relacionado</span>
                        )}
                      </span>
                    </button>
                  );
                })}
              </div>
            </div>

            {/* Fila 3: Filtros Dinámicos de Consulta */}
            <div className="space-y-4 border-t border-slate-100 pt-4">
              <div className="flex items-center justify-between">
                <div className="space-y-0.5">
                  <label className="text-xs font-bold uppercase tracking-wider text-slate-500">
                    Filtros de Búsqueda (WHERE)
                  </label>
                  <p className="text-xs text-slate-400">Configure los criterios para refinar su consulta.</p>
                </div>
                <button
                  type="button"
                  onClick={agregarFiltro}
                  className="inline-flex items-center gap-1.5 px-3 py-1.5 bg-blue-50 hover:bg-blue-100 text-blue-700 text-xs font-bold rounded-lg border border-blue-200 transition-colors shadow-sm"
                >
                  <svg className="w-3.5 h-3.5" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth="2.5">
                    <path strokeLinecap="round" strokeLinejoin="round" d="M12 4v16m8-8H4" />
                  </svg>
                  Agregar Filtro
                </button>
              </div>

              {filtros.length === 0 ? (
                <div className="border border-dashed border-slate-200 rounded-xl p-6 text-center">
                  <p className="text-xs text-slate-400">Sin filtros aplicados. La consulta retornará todos los registros.</p>
                </div>
              ) : (
                <div className="space-y-3">
                  {filtros.map((filtro, index) => {
                    const campoMeta = camposDisponibles.find(c => c.name === filtro.campo);
                    const inputType = campoMeta?.type?.toUpperCase() === 'DATETIME' ? 'date' : 'text';

                    return (
                      <div key={index} className="flex flex-col sm:flex-row items-stretch sm:items-center gap-3 bg-slate-50 border border-slate-200/60 p-3 rounded-xl">
                        
                        {/* Selector de Campo */}
                        <div className="flex-1 min-w-[150px]">
                          <select
                            value={filtro.campo}
                            onChange={(e) => {
                              const nuevoCampo = e.target.value;
                              // Al cambiar de campo, reseteamos operador a uno válido
                              const opsValidos = obtenerOperadoresParaCampo(nuevoCampo);
                              const nuevosFiltros = [...filtros];
                              nuevosFiltros[index] = {
                                campo: nuevoCampo,
                                operador: opsValidos[0].value,
                                valor: ''
                              };
                              setFiltros(nuevosFiltros);
                            }}
                            className="w-full px-3 py-2 text-xs bg-white border border-slate-300 rounded-lg text-slate-900 shadow-sm"
                          >
                            {camposDisponibles.map(c => (
                              <option key={c.name} value={c.name}>{c.label}</option>
                            ))}
                          </select>
                        </div>

                        {/* Selector de Operador */}
                        <div className="w-full sm:w-[150px]">
                          <select
                            value={filtro.operador}
                            onChange={(e) => actualizarFiltro(index, 'operador', e.target.value)}
                            className="w-full px-3 py-2 text-xs bg-white border border-slate-300 rounded-lg text-slate-900 shadow-sm font-medium"
                          >
                            {obtenerOperadoresParaCampo(filtro.campo).map(op => (
                              <option key={op.value} value={op.value}>{op.label}</option>
                            ))}
                          </select>
                        </div>

                        {/* Input de Valor */}
                        <div className="flex-1">
                          <input
                            type={inputType}
                            value={filtro.valor}
                            onChange={(e) => actualizarFiltro(index, 'valor', e.target.value)}
                            placeholder="Ingrese valor..."
                            className="w-full px-3 py-2 text-xs bg-white border border-slate-300 rounded-lg text-slate-900 placeholder-slate-400 shadow-sm"
                          />
                        </div>

                        {/* Botón de Eliminación */}
                        <button
                          type="button"
                          onClick={() => eliminarFiltro(index)}
                          aria-label="Eliminar Filtro"
                          className="p-2 text-red-500 hover:bg-red-50 rounded-lg border border-transparent hover:border-red-200 transition-all shadow-sm shrink-0 flex items-center justify-center"
                        >
                          <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth="2">
                            <path strokeLinecap="round" strokeLinejoin="round" d="M19 7l-.867 12.142A2 2 0 0116.138 21H7.862a2 2 0 01-1.995-1.858L5 7m5 4v6m4-6v6m1-10V4a1 1 0 00-1-1h-4a1 1 0 00-1 1v3M4 7h16" />
                          </svg>
                        </button>
                      </div>
                    );
                  })}
                </div>
              )}
            </div>

            {/* Fila 4: Acciones del Formulario */}
            <div className="border-t border-slate-100 pt-5 flex flex-wrap gap-3 justify-between items-center">
              <button
                type="button"
                onClick={handleBuscar}
                disabled={ejecutarMutation.isPending}
                className="bg-[#002454] hover:bg-[#0b3166] disabled:bg-slate-200 text-white text-xs font-bold py-2.5 px-6 rounded-lg border border-[#C8A84B]/40 hover:border-[#C8A84B] transition-all flex items-center gap-2 shadow-sm"
              >
                {ejecutarMutation.isPending ? (
                  <>
                    <span className="w-3.5 h-3.5 border-2 border-white border-t-transparent rounded-full animate-spin"></span>
                    Compilando y Consultando...
                  </>
                ) : (
                  <>
                    <svg className="w-4 h-4 text-[#C8A84B]" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth="2">
                      <path strokeLinecap="round" strokeLinejoin="round" d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z" />
                    </svg>
                    Ejecutar Consulta
                  </>
                )}
              </button>

              <button
                type="button"
                onClick={handleExportarExcel}
                disabled={exportarMutation.isPending || !ejecutarMutation.data}
                className="bg-green-50 hover:bg-green-100 disabled:bg-slate-50 disabled:text-slate-400 disabled:border-slate-100 text-green-700 text-xs font-bold py-2.5 px-5 rounded-lg border border-green-200 transition-all flex items-center gap-2 shadow-sm"
              >
                {exportarMutation.isPending ? (
                  <>
                    <span className="w-3.5 h-3.5 border-2 border-green-700 border-t-transparent rounded-full animate-spin"></span>
                    Generando Excel...
                  </>
                ) : (
                  <>
                    <svg className="w-4 h-4 text-green-600" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth="2">
                      <path strokeLinecap="round" strokeLinejoin="round" d="M9 17v-2m3 2v-4m3 4v-6m2 10H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z" />
                    </svg>
                    Exportar a Excel
                  </>
                )}
              </button>
            </div>
          </>
        )}
      </div>

      {/* Panel de Resultados */}
      {entidadSeleccionada && (
        <div className="bg-white rounded-2xl border border-slate-200/80 shadow-sm overflow-hidden">
          <div className="p-5 border-b border-slate-100 flex items-center justify-between">
            <h3 className="text-xs font-bold text-slate-800 uppercase tracking-wider">
              Resultados de la Consulta {ejecutarMutation.data && `(${ejecutarMutation.data.filas.length})`}
            </h3>
            {ejecutarMutation.isPending && (
              <span className="text-xs text-slate-400 animate-pulse">Cargando registros...</span>
            )}
          </div>

          {!ejecutarMutation.data && !ejecutarMutation.isPending && (
            <div className="p-12 text-center text-slate-500">
              <p className="text-xs text-slate-400">Defina columnas, agregue filtros y pulse "Ejecutar Consulta" para visualizar registros.</p>
            </div>
          )}

          {ejecutarMutation.data && ejecutarMutation.data.filas.length === 0 && (
            <div className="p-12 text-center text-slate-500">
              <h4 className="text-sm font-bold text-slate-800">No se encontraron resultados</h4>
              <p className="text-xs text-slate-400 mt-1">Modifique los filtros seleccionados e intente nuevamente.</p>
            </div>
          )}

          {ejecutarMutation.data && ejecutarMutation.data.filas.length > 0 && (
            <div className="overflow-x-auto">
              <table className="w-full text-left border-collapse text-xs text-slate-700">
                <thead>
                  <tr className="bg-slate-50 border-b border-slate-200 text-slate-600">
                    {ejecutarMutation.data.columnas.map(col => (
                      <th key={col} className="px-5 py-3.5 font-bold uppercase tracking-wider">{col}</th>
                    ))}
                  </tr>
                </thead>
                <tbody className="divide-y divide-slate-100">
                  {ejecutarMutation.data.filas.map((fila, idx) => (
                    <tr key={idx} className="hover:bg-slate-50/70 transition-colors">
                      {ejecutarMutation.data.columnas.map(col => {
                        const val = fila[col];
                        return (
                          <td key={col} className="px-5 py-3.5 whitespace-nowrap text-slate-800 font-medium">
                            {val === null || val === undefined ? (
                              <span className="text-slate-300">-</span>
                            ) : typeof val === 'number' ? (
                              val.toLocaleString()
                            ) : (
                              val.toString()
                            )}
                          </td>
                        );
                      })}
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </div>
      )}
    </div>
  );
}
