// src/pages/PlataformaReportesPage.tsx

import React, { useState, useEffect } from 'react';
import { useForm } from 'react-hook-form';
import { Link } from 'react-router-dom';
import { toast } from 'react-hot-toast';
import {
  useReportData,
  useExportarReporte,
  useViajeComplementos,
  useReporteParams,
} from '../hooks/useReportes';
import {
  ReportParamDto,
  BarcoPuertoDto,
  ViajeHistoricoDto,
} from '../types/reportes';

// Configuración de Grupos y Reportes (Mapeados desde el estilo legacy del viejo MBPC)
const GRUPOS_REPORTES = [
  {
    id: 'operativos',
    label: 'Reportes Operativos',
    reportes: [
      { id: 'buques_puerto', label: 'Buques en Puerto' },
      { id: 'historico_viajes', label: 'Histórico de Viajes' }
    ]
  },
  {
    id: 'auditoria',
    label: 'Auditoría y Bitácoras',
    reportes: [
      { id: 'auditoria_general', label: 'Bitácora General' }
    ]
  }
];

export default function PlataformaReportesPage() {
  const [activeTab, setActiveTab] = useState<'reportes' | 'auditoria'>('reportes');
  
  // Selectores dinámicos del sistema legacy (Grupo y Reporte)
  const [grupoSeleccionado, setGrupoSeleccionado] = useState('operativos');
  const [reporteSeleccionado, setReporteSeleccionado] = useState('');
  
  // Estado para la búsqueda del reporte
  const [queryParams, setQueryParams] = useState<ReportParamDto[]>([]);
  const [fetchEnabled, setFetchEnabled] = useState(false);
  
  // Estado para el Viaje ID de Auditoría
  const [viajeIdAuditoria, setViajeIdAuditoria] = useState('');
  const [buscarAuditoriaId, setBuscarAuditoriaId] = useState('');

  // Cargar los parámetros/filtros dinámicos del reporte seleccionado
  const { 
    data: filtrosDinamicos = [], 
    isLoading: cargandoFiltros,
    error: errorFiltros 
  } = useReporteParams(reporteSeleccionado);

  // react-hook-form dinámico para mapear campos autogenerados
  const { register, handleSubmit, reset } = useForm<Record<string, string>>();

  // Consultas de datos de reportes y complementos
  const { 
    data: dataReporte, 
    isLoading: cargandoReporte, 
    error: errorReporte 
  } = useReportData(reporteSeleccionado, queryParams, fetchEnabled);

  const { 
    mutate: exportar, 
    isPending: exportando 
  } = useExportarReporte();

  const { 
    data: dataAuditoria, 
    isLoading: cargandoAuditoria, 
    error: errorAuditoria 
  } = useViajeComplementos(buscarAuditoriaId, !!buscarAuditoriaId);

  // Limpiar estados cuando cambia el grupo
  const handleGrupoChange = (e: React.ChangeEvent<HTMLSelectElement>) => {
    setGrupoSeleccionado(e.target.value);
    setReporteSeleccionado('');
    setFetchEnabled(false);
    setQueryParams([]);
    reset();
  };

  // Limpiar estados cuando cambia el reporte
  const handleReporteChange = (e: React.ChangeEvent<HTMLSelectElement>) => {
    const repId = e.target.value;
    setReporteSeleccionado(repId);
    setFetchEnabled(false);
    setQueryParams([]);
    reset();
    if (repId) {
      toast.success(`Cargando parámetros para: ${repId.replace('_', ' ').toUpperCase()}`);
    }
  };

  // Notificación de errores
  useEffect(() => {
    if (errorReporte) {
      toast.error(`Error al cargar reporte: ${errorReporte.mensaje}`);
    }
  }, [errorReporte]);

  useEffect(() => {
    if (errorAuditoria) {
      toast.error(`Error al cargar auditoría: ${errorAuditoria.mensaje}`);
    }
  }, [errorAuditoria]);

  useEffect(() => {
    if (errorFiltros) {
      toast.error(`Error al recuperar metadatos de filtros: ${errorFiltros.mensaje}`);
    }
  }, [errorFiltros]);

  // Manejo de envío del formulario dinámico
  const onSubmitFiltros = (data: Record<string, string>) => {
    const params: ReportParamDto[] = [];
    
    // Mapea dinámicamente cualquier campo completado al DTO del backend
    Object.keys(data).forEach((key) => {
      const val = data[key];
      if (val !== undefined && val !== null && val.trim() !== '') {
        params.push({ name: key, value: val.trim() });
      }
    });

    setQueryParams(params);
    setFetchEnabled(true);
  };

  // Exportación del reporte
  const handleExportar = (formato: 'pdf' | 'excel') => {
    if (formato === 'pdf') {
      toast.error('La exportación a PDF en el servidor está deshabilitada temporalmente en este entorno. Por favor, exporte a Excel.');
      return;
    }

    exportar(
      {
        nombre: reporteSeleccionado,
        parametros: queryParams,
        formato,
      },
      {
        onSuccess: () => {
          toast.success(`Reporte ${formato.toUpperCase()} descargado con éxito.`);
        },
        onError: (err) => {
          toast.error(`Fallo en exportación: ${err.mensaje}`);
        },
      }
    );
  };

  // Búsqueda en auditoría
  const handleBuscarAuditoria = (e: React.FormEvent) => {
    e.preventDefault();
    if (viajeIdAuditoria.trim()) {
      setBuscarAuditoriaId(viajeIdAuditoria.trim());
    } else {
      toast.error('Ingrese un Viaje ID válido para buscar.');
    }
  };

  // Simulación rápida de viaje para auditoría
  const handleSimularViaje = (id: string) => {
    setViajeIdAuditoria(id);
    setBuscarAuditoriaId(id);
    toast.success(`Cargando auditoría para viaje simulado: ${id}`);
  };

  // Reportes disponibles en base al grupo seleccionado
  const reportesDisponibles = GRUPOS_REPORTES.find(g => g.id === grupoSeleccionado)?.reportes || [];

  return (
    <div className="flex-grow p-4 sm:p-6 lg:p-8 max-w-screen-2xl mx-auto w-full">
      {/* Encabezado */}
      <div className="mb-8 border-b border-slate-200 pb-5 flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4">
        <div>
          <h2 className="text-3xl font-extrabold text-slate-900 tracking-tight">
            Plataforma de Reportes y Auditoría
          </h2>
          <p className="text-slate-500 mt-2 text-sm max-w-2xl">
            Modernización del sistema de control y trazabilidad de buques con generación dinámica de filtros.
          </p>
        </div>
        <Link
          to="/dashboard"
          className="inline-flex items-center justify-center gap-2 px-4 py-2 bg-[#002454] hover:bg-[#0b3166] text-white text-sm font-semibold rounded-lg border border-[#C8A84B]/40 hover:border-[#C8A84B] transition-all duration-200 shadow-sm shrink-0 h-fit"
        >
          <svg className="w-4 h-4 text-[#C8A84B]" fill="none" stroke="currentColor" strokeWidth="2.5" viewBox="0 0 24 24">
            <path strokeLinecap="round" strokeLinejoin="round" d="M10 19l-7-7m0 0l7-7m-7 7h18" />
          </svg>
          Volver al Dashboard
        </Link>
      </div>

      {/* Selector de Pestañas */}
      <div className="flex bg-slate-100 p-1 rounded-xl w-fit mb-6">
        <button
          onClick={() => setActiveTab('reportes')}
          className={`flex items-center gap-2 px-5 py-2.5 rounded-lg text-sm font-semibold transition-all duration-200 ${
            activeTab === 'reportes'
              ? 'bg-white text-blue-700 shadow-sm border border-slate-200/50'
              : 'text-slate-600 hover:text-slate-900'
          }`}
        >
          <svg className="w-4 h-4" fill="none" stroke="currentColor" strokeWidth="2" viewBox="0 0 24 24">
            <path strokeLinecap="round" strokeLinejoin="round" d="M9 17v-2m3 2v-4m3 4v-6m2 10H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z" />
          </svg>
          Reportes Operativos
        </button>
        <button
          onClick={() => setActiveTab('auditoria')}
          className={`flex items-center gap-2 px-5 py-2.5 rounded-lg text-sm font-semibold transition-all duration-200 ${
            activeTab === 'auditoria'
              ? 'bg-white text-blue-700 shadow-sm border border-slate-200/50'
              : 'text-slate-600 hover:text-slate-900'
          }`}
        >
          <svg className="w-4 h-4" fill="none" stroke="currentColor" strokeWidth="2" viewBox="0 0 24 24">
            <path strokeLinecap="round" strokeLinejoin="round" d="M12 8v4l3 3m6-3a9 9 0 11-18 0 9 9 0 0118 0z" />
          </svg>
          Auditoría y Bitácora (Tracklog)
        </button>
      </div>

      {/* Contenido Principal */}
      <div className="space-y-6">
        
        {/* PESTAÑA 1: REPORTES OPERATIVOS */}
        {activeTab === 'reportes' && (
          <div className="space-y-6">
            
            {/* Panel Superior: Selectores de Grupo y Reporte (Igual que Diventi) */}
            <div className="bg-white rounded-2xl border border-slate-200/80 shadow-sm p-6">
              <div className="grid grid-cols-1 md:grid-cols-2 gap-6 max-w-4xl">
                <div className="flex flex-col">
                  <label className="text-xs font-semibold uppercase tracking-wider text-slate-500 mb-2">
                    Grupo de Reportes
                  </label>
                  <select
                    value={grupoSeleccionado}
                    onChange={handleGrupoChange}
                    className="w-full px-4 py-2.5 text-sm bg-white border border-slate-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500 text-slate-900 shadow-sm transition-all"
                  >
                    {GRUPOS_REPORTES.map(g => (
                      <option key={g.id} value={g.id}>{g.label}</option>
                    ))}
                  </select>
                </div>

                <div className="flex flex-col">
                  <label className="text-xs font-semibold uppercase tracking-wider text-slate-500 mb-2">
                    Reporte
                  </label>
                  <select
                    value={reporteSeleccionado}
                    onChange={handleReporteChange}
                    className="w-full px-4 py-2.5 text-sm bg-white border border-slate-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500 text-slate-900 shadow-sm transition-all"
                  >
                    <option value="">-- Seleccione un Reporte --</option>
                    {reportesDisponibles.map(r => (
                      <option key={r.id} value={r.id}>{r.label}</option>
                    ))}
                  </select>
                </div>
              </div>
            </div>

            {/* Panel de Filtros Dinámicos (Solo se renderiza si hay un reporte seleccionado) */}
            {reporteSeleccionado && (
              <form onSubmit={handleSubmit(onSubmitFiltros)} className="bg-white rounded-2xl border border-slate-200/80 shadow-sm p-6">
                <div className="flex items-center justify-between border-b border-slate-100 pb-3 mb-4">
                  <h3 className="text-sm font-bold text-slate-900 flex items-center gap-2">
                    <svg className="w-4 h-4 text-blue-600" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                      <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 6V4m0 2a2 2 0 100 4m0-4a2 2 0 110 4m-6 8a2 2 0 100-4m0 4a2 2 0 110-4m0 4v2m0-6V4m6 6v10m6-2a2 2 0 100-4m0 4a2 2 0 110-4m0 4v2m0-6V4" />
                    </svg>
                    Parámetros del Reporte
                  </h3>
                  {cargandoFiltros && (
                    <span className="text-xs text-slate-400 animate-pulse">Obteniendo metadatos...</span>
                  )}
                </div>
                
                {/* Inputs autogenerados en base a FiltroDinamico[] */}
                {filtrosDinamicos.length === 0 && !cargandoFiltros ? (
                  <p className="text-xs text-slate-400 py-2">Este reporte no requiere parámetros adicionales.</p>
                ) : (
                  <div className="grid grid-cols-1 md:grid-cols-3 lg:grid-cols-4 gap-4">
                    {filtrosDinamicos.map((filtro) => (
                      <div key={filtro.name} className="flex flex-col">
                        <label className="text-xs text-slate-600 mb-1.5 font-medium">
                          {filtro.label}
                        </label>
                        <input
                          type={filtro.type}
                          {...register(filtro.name)}
                          placeholder={`Ingrese ${filtro.label}...`}
                          className="w-full px-4 py-2 text-sm bg-white border border-slate-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500 text-slate-900 placeholder-slate-400 shadow-sm transition-all"
                        />
                      </div>
                    ))}
                  </div>
                )}

                {/* Botonera de Acciones del Formulario */}
                <div className="mt-6 flex flex-wrap gap-3 justify-between items-center border-t border-slate-100 pt-5">
                  <div className="flex gap-2">
                    <button
                      type="submit"
                      className="bg-[#002454] hover:bg-[#0b3166] text-white text-xs font-bold py-2.5 px-5 rounded-lg border border-[#C8A84B]/40 hover:border-[#C8A84B] transition-all flex items-center gap-2 shadow-sm"
                    >
                      <svg className="w-4 h-4 text-[#C8A84B]" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                        <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z" />
                      </svg>
                      Buscar Reporte
                    </button>
                    <button
                      type="button"
                      onClick={() => { reset(); setFetchEnabled(false); setQueryParams([]); }}
                      className="bg-slate-100 hover:bg-slate-200 text-slate-700 text-xs font-semibold py-2.5 px-4 rounded-lg border border-slate-200 transition-colors"
                    >
                      Limpiar
                    </button>
                  </div>

                  <div className="flex gap-2">
                    <button
                      type="button"
                      onClick={() => handleExportar('pdf')}
                      disabled={exportando || !fetchEnabled}
                      className="bg-red-50 hover:bg-red-100 disabled:bg-slate-50 disabled:text-slate-400 disabled:border-slate-100 text-red-700 text-xs font-bold py-2.5 px-4 rounded-lg transition-all border border-red-200 flex items-center gap-2 shadow-sm"
                    >
                      {exportando ? (
                        <span className="w-4 h-4 border-2 border-red-700 border-t-transparent rounded-full animate-spin"></span>
                      ) : (
                        <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                          <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 10v6m0 0l-3-3m3 3l3-3m2 8H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z" />
                        </svg>
                      )}
                      Exportar PDF
                    </button>
                    <button
                      type="button"
                      onClick={() => handleExportar('excel')}
                      disabled={exportando || !fetchEnabled}
                      className="bg-green-50 hover:bg-green-100 disabled:bg-slate-50 disabled:text-slate-400 disabled:border-slate-100 text-green-700 text-xs font-bold py-2.5 px-4 rounded-lg transition-all border border-green-200 flex items-center gap-2 shadow-sm"
                    >
                      {exportando ? (
                        <span className="w-4 h-4 border-2 border-green-700 border-t-transparent rounded-full animate-spin"></span>
                      ) : (
                        <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                          <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 17v-2m3 2v-4m3 4v-6m2 10H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z" />
                        </svg>
                      )}
                      Exportar Excel
                    </button>
                  </div>
                </div>
              </form>
            )}

            {/* Resultados */}
            {reporteSeleccionado && (
              <div className="bg-white rounded-2xl border border-slate-200/80 shadow-sm overflow-hidden">
                <div className="p-6 border-b border-slate-200 flex items-center justify-between">
                  <h3 className="text-sm font-bold text-slate-800 uppercase tracking-wider">
                    Listado de Resultados {fetchEnabled && `(${dataReporte?.length || 0})`}
                  </h3>
                  {cargandoReporte && (
                    <span className="text-xs text-slate-500 flex items-center gap-2">
                      <span className="w-3.5 h-3.5 border-2 border-blue-600 border-t-transparent rounded-full animate-spin"></span>
                      Cargando registros...
                    </span>
                  )}
                </div>

                {!fetchEnabled ? (
                  <div className="p-12 text-center text-slate-500">
                    <p className="text-sm text-slate-400">Configure los parámetros y pulse "Buscar Reporte" para visualizar datos.</p>
                  </div>
                ) : errorReporte ? (
                  <div className="p-6 bg-red-50 text-red-700 text-sm border-t border-red-100">
                    {errorReporte.mensaje}
                  </div>
                ) : dataReporte?.length === 0 ? (
                  <div className="p-12 text-center text-slate-500">
                    <h4 className="text-base font-bold text-slate-800">No se encontraron resultados</h4>
                    <p className="text-sm text-slate-400 mt-1">Intente ajustar los filtros de búsqueda.</p>
                  </div>
                ) : (
                  <div className="overflow-x-auto">
                    <table className="w-full text-left border-collapse text-sm text-slate-700">
                      <thead>
                        <tr className="bg-slate-50 text-slate-600 font-bold text-xs uppercase tracking-wider border-b border-slate-200">
                          {reporteSeleccionado === 'buques_puerto' ? (
                            <>
                              <th className="py-4 px-6 w-24">ID</th>
                              <th className="py-4 px-6">Buque</th>
                              <th className="py-4 px-6 w-36">MMSI</th>
                              <th className="py-4 px-6">Origen</th>
                              <th className="py-4 px-6">Destino</th>
                              <th className="py-4 px-6 w-44">ETA</th>
                              <th className="py-4 px-6 w-32 text-right">Estado</th>
                            </>
                          ) : (
                            <>
                              <th className="py-4 px-6 w-24">ID</th>
                              <th className="py-4 px-6">Buque</th>
                              <th className="py-4 px-6 w-32">OMI</th>
                              <th className="py-4 px-6 w-36">Matrícula</th>
                              <th className="py-4 px-6">Origen</th>
                              <th className="py-4 px-6">Destino</th>
                              <th className="py-4 px-6">Fecha Partida</th>
                              <th className="py-4 px-6 w-44">ETA</th>
                              <th className="py-4 px-6 w-24">Costera</th>
                              <th className="py-4 px-6 w-32 text-right">Estado</th>
                            </>
                          )}
                        </tr>
                      </thead>
                      <tbody className="divide-y divide-slate-100">
                        {reporteSeleccionado === 'buques_puerto'
                          ? (Array.isArray(dataReporte) ? dataReporte as BarcoPuertoDto[] : []).map((row) => (
                              <tr key={row.id || Math.random().toString()} className="hover:bg-slate-55/70 transition-colors duration-150">
                                <td className="py-3.5 px-6 font-mono text-xs text-slate-500 font-bold">{row.id}</td>
                                <td className="py-3.5 px-6 font-semibold text-slate-900">{row.buque}</td>
                                <td className="py-3.5 px-6 font-mono text-xs text-slate-600">{row.mmsi}</td>
                                <td className="py-3.5 px-6 text-slate-600">{row.origen}</td>
                                <td className="py-3.5 px-6 text-slate-600">{row.destino}</td>
                                <td className="py-3.5 px-6 text-slate-600">{row.eta}</td>
                                <td className="py-3.5 px-6 text-right">
                                  <span className={`inline-flex items-center px-2 py-0.5 rounded-full text-xs font-semibold ${
                                    row.estado === 'Amarrado' 
                                      ? 'bg-blue-50 text-blue-700 border border-blue-200' 
                                      : 'bg-yellow-50 text-yellow-700 border border-yellow-200'
                                  }`}>
                                    {row.estado}
                                  </span>
                                </td>
                              </tr>
                            ))
                          : (Array.isArray(dataReporte) ? dataReporte as ViajeHistoricoDto[] : []).map((row) => (
                              <tr key={row.id || Math.random().toString()} className="hover:bg-slate-55/70 transition-colors duration-150">
                                <td className="py-3.5 px-6 font-mono text-xs text-slate-500 font-bold">{row.id}</td>
                                <td className="py-3.5 px-6 font-semibold text-slate-900">{row.buque}</td>
                                <td className="py-3.5 px-6 text-slate-600 font-mono text-xs">{row.omi}</td>
                                <td className="py-3.5 px-6 text-slate-600 font-mono text-xs">{row.matricula}</td>
                                <td className="py-3.5 px-6 text-slate-600">{row.origen}</td>
                                <td className="py-3.5 px-6 text-slate-600">{row.destino}</td>
                                <td className="py-3.5 px-6 text-slate-600">{row.fechaPartida}</td>
                                <td className="py-3.5 px-6 text-slate-600">{row.eta}</td>
                                <td className="py-3.5 px-6 font-mono text-xs text-slate-500">{row.costeraId}</td>
                                <td className="py-3.5 px-6 text-right">
                                  <span className="inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-semibold bg-slate-100 text-slate-700 border border-slate-200">
                                    {row.estado}
                                  </span>
                                </td>
                              </tr>
                            ))}
                      </tbody>
                    </table>
                  </div>
                )}
              </div>
            )}
          </div>
        )}

        {/* PESTAÑA 2: AUDITORÍA Y BITÁCORA (TRACKLOG) */}
        {activeTab === 'auditoria' && (
          <div className="space-y-6">
            <div className="bg-white rounded-2xl border border-slate-200/80 shadow-sm p-6">
              <h3 className="text-sm font-bold text-slate-900 mb-4 uppercase tracking-wider">
                Auditoría Consolidada de Viaje
              </h3>
              
              <form onSubmit={handleBuscarAuditoria} className="flex flex-col sm:flex-row gap-3 max-w-xl">
                <input
                  type="text"
                  value={viajeIdAuditoria}
                  onChange={(e) => setViajeIdAuditoria(e.target.value)}
                  placeholder="Ingrese el Viaje ID de MongoDB"
                  className="w-full px-4 py-2 text-sm bg-white border border-slate-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500 text-slate-900 placeholder-slate-400 shadow-sm flex-grow"
                />
                <button
                  type="submit"
                  className="bg-[#002454] hover:bg-[#0b3166] text-white text-xs font-bold py-2 px-6 rounded-lg border border-[#C8A84B]/40 hover:border-[#C8A84B] transition-all shadow-sm"
                >
                  Cargar Auditoría
                </button>
              </form>

              {/* Atajos de simulación de viajes */}
              <div className="mt-3 flex items-center gap-2 flex-wrap">
                <span className="text-xs text-slate-500 font-medium">Búsquedas rápidas:</span>
                <button
                  onClick={() => handleSimularViaje('vj-801')}
                  className="bg-slate-50 border border-slate-200 hover:border-blue-500 text-slate-700 text-[11px] px-3 py-1.5 rounded-lg transition-colors font-semibold shadow-sm"
                >
                  Viaje VJ-801 (Carga Gral)
                </button>
                <button
                  onClick={() => handleSimularViaje('vj-3032')}
                  className="bg-slate-50 border border-slate-200 hover:border-blue-500 text-slate-700 text-[11px] px-3 py-1.5 rounded-lg transition-colors font-semibold shadow-sm"
                >
                  Viaje VJ-3032 (Convoy UABL)
                </button>
              </div>
            </div>

            {/* Si no hay búsqueda */}
            {!buscarAuditoriaId && (
              <div className="p-12 text-center text-slate-500 bg-white rounded-2xl border border-slate-200/80 shadow-sm">
                <p className="text-slate-400 text-sm">
                  Por favor ingrese un Viaje ID o seleccione una búsqueda rápida para auditar la bitácora y telemetría AIS.
                </p>
              </div>
            )}

            {/* Cargando Auditoría */}
            {buscarAuditoriaId && cargandoAuditoria && (
              <div className="p-12 text-center text-slate-550 bg-white rounded-2xl border border-slate-200/80 shadow-sm">
                <span className="inline-block w-8 h-8 border-4 border-blue-600 border-t-transparent rounded-full animate-spin mb-4"></span>
                <p className="text-slate-400 text-sm">Recuperando bitácora y registros consolidados de auditoría...</p>
              </div>
            )}

            {/* Error en Auditoría */}
            {buscarAuditoriaId && errorAuditoria && (
              <div className="p-4 bg-red-50 text-red-750 text-sm rounded-lg border border-red-100">
                No se pudo cargar la auditoría: {errorAuditoria.mensaje}
              </div>
            )}

            {/* Datos Consolidados */}
            {buscarAuditoriaId && !cargandoAuditoria && dataAuditoria && (
              <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
                
                {/* Columna Izquierda/Centro: Bitácora de Auditoría (Timeline) */}
                <div className="lg:col-span-2 space-y-6">
                  <div className="bg-white rounded-2xl border border-slate-200/80 shadow-sm p-6">
                    <div className="flex items-center justify-between border-b border-slate-100 pb-4 mb-6">
                      <h4 className="text-sm font-bold text-slate-900 uppercase tracking-wider flex items-center gap-2">
                        <span className="w-2 h-2 bg-blue-600 rounded-full animate-ping"></span>
                        Línea de Tiempo Operativa (Viaje: {dataAuditoria.viajeId})
                      </h4>
                      <span className="text-[11px] text-slate-500 font-mono font-semibold">
                        Notas Registradas: {dataAuditoria.notasBitacora?.length || 0}
                      </span>
                    </div>

                    {dataAuditoria.notasBitacora?.length === 0 ? (
                      <p className="text-slate-500 text-sm py-4">No hay notas registradas para este viaje.</p>
                    ) : (
                      <div className="relative border-l border-slate-200 ml-4 pl-6 space-y-6">
                        {dataAuditoria.notasBitacora.map((nota) => (
                          <div key={nota.id} className="relative">
                            <span className="absolute -left-[31px] top-1.5 w-3 h-3 rounded-full bg-white border-2 border-blue-600 flex items-center justify-center">
                              <span className="w-1 h-1 bg-blue-600 rounded-full"></span>
                            </span>

                            <div className="bg-slate-50/60 p-4 rounded-xl border border-slate-200/80 hover:bg-slate-50 hover:border-slate-350 transition-all">
                              <div className="flex flex-wrap items-center justify-between gap-2 mb-2">
                                <span className={`px-2.5 py-0.5 rounded-full text-[10px] font-bold ${
                                  nota.categoria === 'SEGURIDAD' 
                                    ? 'bg-red-50 text-red-700 border border-red-200'
                                    : nota.categoria === 'TRANSICION'
                                    ? 'bg-blue-50 text-blue-700 border border-blue-200'
                                    : 'bg-slate-100 text-slate-700 border border-slate-200'
                                }`}>
                                  {nota.categoria}
                                </span>
                                <span className="text-[11px] text-slate-500 font-mono font-medium">
                                  {new Date(nota.fechaHora).toLocaleString()}
                                </span>
                              </div>
                              <p className="text-sm text-slate-700 leading-relaxed">{nota.texto}</p>
                              <div className="mt-3 text-[10px] text-slate-500 flex items-center gap-1.5 border-t border-slate-200 pt-2 font-medium">
                                <svg className="w-3.5 h-3.5 text-slate-400" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M16 7a4 4 0 11-8 0 4 4 0 018 0zM12 14a7 7 0 00-7 7h14a7 7 0 00-7-7z" />
                                </svg>
                                Operador: <span className="font-semibold text-slate-700">{nota.usuario}</span>
                              </div>
                            </div>
                          </div>
                        ))}
                      </div>
                    )}
                  </div>
                </div>

                {/* Columna Derecha: Protección (PBIP), Agencias y Telemetría Tracklog */}
                <div className="space-y-6">
                  <div className="bg-white rounded-2xl border border-slate-200/80 shadow-sm p-6 space-y-6">
                    <div>
                      <h4 className="text-xs font-bold text-slate-900 uppercase tracking-wider mb-3 pb-2 border-b border-slate-100">
                        Protección Marítima (PBIP)
                      </h4>
                      {dataAuditoria.datosPbip ? (
                        <div className="space-y-2.5 text-xs text-slate-700">
                          <div className="flex justify-between items-center">
                            <span className="text-slate-500 font-medium">Nivel Protección:</span>
                            <span className="font-bold text-yellow-700 bg-yellow-50 px-2 py-0.5 rounded border border-yellow-250">
                              Nivel {dataAuditoria.datosPbip.nivelProteccion}
                            </span>
                          </div>
                          <div className="flex justify-between">
                            <span className="text-slate-500 font-medium">Arqueo Bruto:</span>
                            <span className="text-slate-900 font-mono font-semibold">
                              {dataAuditoria.datosPbip.arqueoBruto ? `${dataAuditoria.datosPbip.arqueoBruto.toLocaleString()} TRB` : 'No declarado'}
                            </span>
                          </div>
                          <div className="flex justify-between">
                            <span className="text-slate-500 font-medium">Nro Inmarsat:</span>
                            <span className="text-slate-900 font-mono font-semibold">{dataAuditoria.datosPbip.nroInmarsat || '-'}</span>
                          </div>
                          <div className="flex flex-col mt-1">
                            <span className="text-slate-500 font-medium mb-1">Contacto OCPM:</span>
                            <span className="text-slate-800 font-mono bg-slate-50 p-2 rounded border border-slate-200 text-[11px] leading-relaxed">
                              {dataAuditoria.datosPbip.contactoOcpm || 'Sin contacto'}
                            </span>
                          </div>
                        </div>
                      ) : (
                        <p className="text-xs text-slate-400">Sin datos PBIP declarados.</p>
                      )}
                    </div>

                    <div>
                      <h4 className="text-xs font-bold text-slate-900 uppercase tracking-wider mb-3 pb-2 border-b border-slate-100">
                        Agencias Intervinientes
                      </h4>
                      {dataAuditoria.agencias?.length === 0 ? (
                        <p className="text-xs text-slate-400">No hay agencias asignadas a este viaje.</p>
                      ) : (
                        <div className="space-y-3">
                          {dataAuditoria.agencias.map((agencia, i) => (
                            <div key={i} className="p-3 bg-slate-50 rounded-lg border border-slate-200 text-xs">
                              <span className="block font-bold text-blue-700 uppercase text-[9px] tracking-wider mb-1">
                                {agencia.rol}
                              </span>
                              <span className="block text-slate-900 font-bold">{agencia.nombre}</span>
                              <span className="block text-slate-500 text-[10px] mt-1 font-mono">{agencia.contacto}</span>
                            </div>
                          ))}
                        </div>
                      )}
                    </div>
                  </div>

                  {/* Telemetría AIS Inmutable (Tracklog) */}
                  <div className="bg-white rounded-2xl border border-slate-200/80 shadow-sm p-6">
                    <h4 className="text-xs font-bold text-slate-900 uppercase tracking-wider mb-3 pb-2 border-b border-slate-100 flex items-center justify-between">
                      <span>Tracklog Telemetría AIS</span>
                      <span className="px-2 py-0.5 rounded-full text-[9px] font-bold bg-green-50 text-green-700 border border-green-200">
                        INMUTABLE
                      </span>
                    </h4>

                    <div className="bg-slate-50 border border-slate-200 rounded-xl p-4 font-mono text-xs space-y-3 text-slate-700">
                      <div className="flex items-center justify-between border-b border-slate-200 pb-2">
                        <span className="text-blue-700 font-bold"># GPS ST-01</span>
                        <span className="text-green-600 flex items-center gap-1 font-bold text-[10px]">
                          <span className="w-1.5 h-1.5 bg-green-600 rounded-full animate-ping"></span>
                          TRANSMITIENDO
                        </span>
                      </div>
                      
                      <div className="space-y-2">
                        <div className="flex justify-between">
                          <span className="text-slate-500">Latitud:</span>
                          <span className="text-slate-900 font-semibold">34° 36' 12'' S</span>
                        </div>
                        <div className="flex justify-between">
                          <span className="text-slate-500">Longitud:</span>
                          <span className="text-slate-900 font-semibold">58° 22' 08'' W</span>
                        </div>
                        <div className="flex justify-between">
                          <span className="text-slate-500">Velocidad:</span>
                          <span className="text-yellow-700 font-bold">12.4 nudos (kn)</span>
                        </div>
                        <div className="flex justify-between">
                          <span className="text-slate-500">Rumbo:</span>
                          <span className="text-slate-900 font-semibold">145° (SE)</span>
                        </div>
                      </div>

                      {/* Feed inmutable */}
                      <div className="mt-3 pt-3 border-t border-slate-200 space-y-1.5 text-[10px] text-slate-400 max-h-[120px] overflow-y-auto">
                        <div className="flex justify-between text-slate-600">
                          <span>12:44:30 - pos OK</span>
                          <span>-34.6033, -58.3688</span>
                        </div>
                        <div className="flex justify-between">
                          <span>12:39:15 - pos OK</span>
                          <span>-34.6042, -58.3695</span>
                        </div>
                        <div className="flex justify-between">
                          <span>12:34:02 - pos OK</span>
                          <span>-34.6051, -58.3702</span>
                        </div>
                        <div className="flex justify-between">
                          <span>12:28:50 - pos OK</span>
                          <span>-34.6060, -58.3710</span>
                        </div>
                      </div>
                    </div>
                  </div>

                </div>

              </div>
            )}
          </div>
        )}
      </div>
    </div>
  );
}
