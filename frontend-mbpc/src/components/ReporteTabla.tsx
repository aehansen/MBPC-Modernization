import React, { useState } from 'react';
import { useReporte, buildReportParams, type ReportParamDto } from '../hooks/useReporte';
import { reporteApi } from '../axiosClient';

interface ReporteTablaProps {
  nombreReporte: string;
  filtros: ReportParamDto[];
  titulo?: string;
  descripcion?: string;
}

export const ReporteTabla: React.FC<ReporteTablaProps> = ({
  nombreReporte,
  filtros,
  titulo = 'Visualizador de Reporte',
  descripcion = 'Consulta dinámica de datos y exportación directa.'
}) => {
  const { data, isLoading, isError, error, refetch } = useReporte(nombreReporte, filtros);
  const [isExporting, setIsExporting] = useState(false);
  const [exportError, setExportError] = useState<string | null>(null);

  const handleExportExcel = async () => {
    try {
      setIsExporting(true);
      setExportError(null);
      
      const queryParams = buildReportParams(filtros);
      const response = await reporteApi.exportar(nombreReporte, queryParams);
      
      let fileName = `Reporte_${nombreReporte}_${new Date().toISOString().replace(/[:.]/g, '-')}.xlsx`;
      
      const disposition = response.headers['content-disposition'];
      if (disposition && disposition.indexOf('attachment') !== -1) {
        const filenameRegex = /filename[^;=\n]*=((['"]).*?\2|[^;\n]*)/;
        const matches = filenameRegex.exec(disposition);
        if (matches !== null && matches[1]) {
          fileName = matches[1].replace(/['"]/g, '');
        }
      }

      const blob = new Blob([response.data], {
        type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
      });
      
      const url = window.URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      link.setAttribute('download', fileName);
      document.body.appendChild(link);
      link.click();
      
      // Limpieza
      link.parentNode?.removeChild(link);
      window.URL.revokeObjectURL(url);
    } catch (err) {
      console.error('Error al exportar reporte:', err);
      setExportError('No se pudo descargar el archivo Excel. Verifique que existan registros.');
    } finally {
      setIsExporting(false);
    }
  };

  if (isLoading) {
    return (
      <div className="flex flex-col items-center justify-center p-12 space-y-4 bg-slate-900/40 rounded-2xl border border-slate-800/80 backdrop-blur-md">
        <div className="w-12 h-12 border-4 border-t-cyan-500 border-r-transparent border-b-cyan-500 border-l-transparent rounded-full animate-spin"></div>
        <p className="text-slate-400 font-medium animate-pulse">Cargando reporte de datos...</p>
      </div>
    );
  }

  if (isError) {
    return (
      <div className="p-6 bg-red-950/20 border border-red-900/50 rounded-2xl text-red-200">
        <h4 className="font-bold text-lg mb-2">Error al recuperar el reporte</h4>
        <p className="text-sm opacity-90">{error?.message || 'Error inesperado del servidor.'}</p>
        <button
          onClick={() => refetch()}
          className="mt-4 px-4 py-2 bg-red-900/30 hover:bg-red-900/50 rounded-lg text-xs font-semibold uppercase tracking-wider transition duration-200"
        >
          Reintentar consulta
        </button>
      </div>
    );
  }

  // Obtener cabeceras dinámicas uniendo las llaves de todas las filas para evitar omitir columnas
  const headers = data
    ? Array.from(new Set(data.flatMap((row) => Object.keys(row))))
    : [];

  return (
    <div className="w-full bg-slate-900/60 rounded-2xl border border-slate-800/80 backdrop-blur-md overflow-hidden shadow-2xl transition-all duration-300">
      {/* Header del Reporte */}
      <div className="p-6 border-b border-slate-800/85 flex flex-col md:flex-row md:items-center md:justify-between gap-4">
        <div>
          <h2 className="text-xl font-bold text-slate-100 tracking-tight">{titulo}</h2>
          <p className="text-sm text-slate-400 mt-1">{descripcion}</p>
        </div>
        <div className="flex flex-col sm:flex-row gap-3">
          <button
            onClick={() => refetch()}
            className="px-4 py-2 bg-slate-800 hover:bg-slate-700 text-slate-200 font-semibold text-sm rounded-xl transition duration-200 flex items-center justify-center gap-2"
          >
            Actualizar
          </button>
          <button
            onClick={handleExportExcel}
            disabled={isExporting || !data || data.length === 0}
            className="px-5 py-2 bg-gradient-to-r from-emerald-500 to-teal-600 hover:from-emerald-600 hover:to-teal-700 disabled:from-slate-800 disabled:to-slate-800 text-white font-semibold text-sm rounded-xl shadow-lg shadow-emerald-900/20 disabled:shadow-none transition duration-200 flex items-center justify-center gap-2 disabled:cursor-not-allowed"
          >
            {isExporting ? (
              <>
                <div className="w-4 h-4 border-2 border-white border-t-transparent rounded-full animate-spin"></div>
                Exportando...
              </>
            ) : (
              'Exportar a Excel'
            )}
          </button>
        </div>
      </div>

      {exportError && (
        <div className="px-6 py-3 bg-red-950/20 border-b border-red-900/30 text-red-300 text-xs font-medium flex justify-between items-center">
          <span>{exportError}</span>
          <button onClick={() => setExportError(null)} className="text-red-400 hover:text-red-200">
            ✕
          </button>
        </div>
      )}

      {/* Tabla de Datos */}
      <div className="overflow-x-auto">
        {!data || data.length === 0 ? (
          <div className="p-12 text-center text-slate-500 font-medium">
            No se encontraron registros en el reporte.
          </div>
        ) : (
          <table className="w-full text-left border-collapse">
            <thead>
              <tr className="bg-slate-950/40 border-b border-slate-850">
                {headers.map((header) => (
                  <th
                    key={header}
                    className="px-6 py-4 text-xs font-bold uppercase tracking-wider text-cyan-400/90 whitespace-nowrap"
                  >
                    {header.replace(/_/g, ' ')}
                  </th>
                ))}
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-850 bg-slate-900/10">
              {data.map((row, idx) => (
                <tr
                  key={idx}
                  className="hover:bg-slate-800/30 transition duration-150 ease-in-out"
                >
                  {headers.map((header) => {
                    const value = row[header];
                    let displayValue = '-';
                    if (value !== null && value !== undefined) {
                      if (typeof value === 'boolean') {
                        displayValue = value ? 'Sí' : 'No';
                      } else {
                        displayValue = String(value);
                      }
                    }
                    return (
                      <td
                        key={header}
                        className="px-6 py-3.5 text-sm text-slate-300 font-medium whitespace-nowrap"
                      >
                        {displayValue}
                      </td>
                    );
                  })}
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>

      {/* Footer / Resumen */}
      {data && data.length > 0 && (
        <div className="px-6 py-4 bg-slate-950/20 border-t border-slate-800/80 text-xs text-slate-500 flex items-center justify-between">
          <span>Total de registros: {data.length}</span>
          <span>MBPC Report Engine v1.0</span>
        </div>
      )}
    </div>
  );
};
