// src/services/reporteService.ts

import apiClient from './apiClient';
import { ReportParamDto, ViajeComplementosDto, FiltroDinamico } from '../types/reportes';

/**
 * Convierte una lista de parámetros de reporte al formato esperado por el model binder de ASP.NET Core
 * para listas complejas en la Query String (ej: Parametros[0].Name y Parametros[0].Value).
 */
const formatearParametrosQuery = (parametros: ReportParamDto[]): Record<string, string> => {
  const query: Record<string, string> = {};
  parametros.forEach((param, index) => {
    query[`Parametros[${index}].Name`] = param.name;
    if (param.value !== undefined && param.value !== null) {
      query[`Parametros[${index}].Value`] = param.value;
    }
  });
  return query;
};

/**
 * Obtiene la configuración de parámetros dinámicos de un reporte específico.
 */
export const getReporteParams = async (reporteId: string): Promise<FiltroDinamico[]> => {
  const response = await apiClient.get<FiltroDinamico[]>(`/api/reportes/${reporteId}/params`);
  return response.data;
};

/**
 * Obtiene los datos del reporte especificado.
 */
export const getReportData = async (nombre: string, parametros: ReportParamDto[]): Promise<any[]> => {
  const queryParams = formatearParametrosQuery(parametros);
  const response = await apiClient.get<any[]>(`/api/reportes/${nombre}/data`, {
    params: queryParams,
  });
  return response.data;
};

/**
 * Descarga el reporte en el formato indicado, configurando responseType: 'blob'.
 * Maneja el objeto Blob para iniciar la descarga del archivo en el navegador con el tipo MIME correcto.
 */
export const exportarReporte = async (
  nombre: string,
  parametros: ReportParamDto[],
  formato: 'pdf' | 'excel'
): Promise<Blob> => {
  const queryParams = formatearParametrosQuery(parametros);
  
  const response = await apiClient.get(`/api/reportes/${nombre}/exportar`, {
    params: {
      ...queryParams,
      formato,
    },
    responseType: 'blob',
  });

  const blob = new Blob([response.data], {
    type: formato === 'excel' 
      ? 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet' 
      : 'application/pdf',
  });

  const url = window.URL.createObjectURL(blob);
  const link = document.createElement('a');
  link.href = url;
  
  const timestamp = new Date().toISOString().replace(/[:.]/g, '-');
  const extension = formato === 'excel' ? 'xlsx' : 'pdf';
  link.setAttribute('download', `Reporte_${nombre}_${timestamp}.${extension}`);
  
  document.body.appendChild(link);
  link.click();
  
  link.parentNode?.removeChild(link);
  window.URL.revokeObjectURL(url);

  return blob;
};

/**
 * Obtiene los complementos de un viaje específico (Bitácora, Agencias y Datos PBIP).
 */
export const getViajeComplementos = async (viajeId: string): Promise<ViajeComplementosDto> => {
  const response = await apiClient.get<ViajeComplementosDto>(`/api/viajes/${viajeId}/complementos`);
  return response.data;
};
