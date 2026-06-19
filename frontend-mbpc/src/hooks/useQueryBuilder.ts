import { useQuery, useMutation, UseQueryResult, UseMutationResult } from '@tanstack/react-query';
import { getQueryBuilderMetadata, ejecutarConsultaDinamica, exportarConsultaExcel } from '../services/queryBuilderService';
import { MetadataEntity, QueryRequest, QueryResult } from '../types/queryBuilder';
import { ApiErrorResponse, parseApiError } from './useReportes';

/**
 * Hook para obtener los metadatos de entidades y campos del Query Builder.
 */
export function useQueryBuilderMetadata(): UseQueryResult<MetadataEntity[], ApiErrorResponse> {
  return useQuery<MetadataEntity[], ApiErrorResponse>({
    queryKey: ['querybuilder', 'metadata'],
    queryFn: async () => {
      try {
        return await getQueryBuilderMetadata();
      } catch (error) {
        throw parseApiError(error);
      }
    },
    staleTime: Infinity, // Metadata estática que no cambia en la sesión
  });
}

/**
 * Hook de mutación para ejecutar una consulta personalizada.
 */
export function useEjecutarConsulta(): UseMutationResult<QueryResult, ApiErrorResponse, QueryRequest> {
  return useMutation<QueryResult, ApiErrorResponse, QueryRequest>({
    mutationFn: async (request: QueryRequest) => {
      try {
        return await ejecutarConsultaDinamica(request);
      } catch (error) {
        throw parseApiError(error);
      }
    },
  });
}

/**
 * Hook de mutación para exportar a Excel la consulta.
 */
export function useExportarConsultaExcel(): UseMutationResult<Blob, ApiErrorResponse, QueryRequest> {
  return useMutation<Blob, ApiErrorResponse, QueryRequest>({
    mutationFn: async (request: QueryRequest) => {
      try {
        return await exportarConsultaExcel(request);
      } catch (error) {
        throw parseApiError(error);
      }
    },
  });
}
