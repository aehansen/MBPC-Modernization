import apiClient from './apiClient';
import { MetadataEntity, QueryRequest, QueryResult } from '../types/queryBuilder';

/**
 * Obtiene la configuración de entidades y campos del Query Builder.
 */
export const getQueryBuilderMetadata = async (): Promise<MetadataEntity[]> => {
  const response = await apiClient.get<MetadataEntity[]>('/api/querybuilder/metadata');
  return response.data;
};

/**
 * Ejecuta una consulta dinámica y devuelve el listado de resultados.
 */
export const ejecutarConsultaDinamica = async (request: QueryRequest): Promise<QueryResult> => {
  const response = await apiClient.post<QueryResult>('/api/querybuilder/ejecutar', request);
  return response.data;
};

/**
 * Exporta el resultado de la consulta dinámica a un archivo Excel.
 */
export const exportarConsultaExcel = async (request: QueryRequest): Promise<Blob> => {
  const response = await apiClient.post('/api/querybuilder/exportar', request, {
    responseType: 'blob',
  });

  const blob = new Blob([response.data], {
    type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
  });

  const url = window.URL.createObjectURL(blob);
  const link = document.createElement('a');
  link.href = url;
  
  const timestamp = new Date().toISOString().replace(/[:.]/g, '-');
  link.setAttribute('download', `ConsultaDinamica_${request.entidadPrincipal}_${timestamp}.xlsx`);
  
  document.body.appendChild(link);
  link.click();
  
  link.parentNode?.removeChild(link);
  window.URL.revokeObjectURL(url);

  return blob;
};
