// src/hooks/useReportes.ts

import { useQuery, useMutation } from '@tanstack/react-query';
import { getReportData, exportarReporte, getViajeComplementos, getReporteParams } from '../services/reporteService';
import { ReportParamDto, ViajeComplementosDto, FiltroDinamico } from '../types/reportes';

// Interfaz para ProblemDetails de .NET 8
export interface ApiErrorResponse {
  mensaje: string;
  title?: string;
  detail?: string;
  status?: number;
}

/**
 * Type-guard unificado para detectar respuestas de error de Axios
 */
export function isAxiosErrorWithResponse(
  error: unknown
): error is { response: { data: unknown; status: number } } {
  return (
    typeof error === 'object' &&
    error !== null &&
    'response' in error &&
    typeof (error as { response: unknown }).response === 'object' &&
    (error as { response: unknown }).response !== null
  );
}

/**
 * Formatea un error desconocido a un formato ApiErrorResponse consistente.
 */
export function parseApiError(error: unknown): ApiErrorResponse {
  if (isAxiosErrorWithResponse(error)) {
    const body = error.response.data as any;
    return {
      mensaje:
        body?.mensaje ??
        body?.title ??
        body?.detail ??
        'Error en la operación del servidor.',
      detail: body?.detail,
      status: error.response.status,
    };
  }
  return {
    mensaje: (error as Error).message || 'Error inesperado de comunicación.',
  };
}

/**
 * Hook para consultar los parámetros dinámicos de un reporte específico.
 */
export function useReporteParams(reporteId: string) {
  return useQuery<FiltroDinamico[], ApiErrorResponse>({
    queryKey: ['reporte', 'params', reporteId],
    queryFn: async () => {
      try {
        return await getReporteParams(reporteId);
      } catch (error) {
        throw parseApiError(error);
      }
    },
    enabled: !!reporteId,
  });
}

/**
 * Hook para consultar los datos de un reporte de forma condicionada.
 * Se dispara cuando 'enabled' es verdadero y el nombre del reporte está definido.
 */
export function useReportData(nombre: string, parametros: ReportParamDto[], enabled: boolean = false) {
  return useQuery<any[], ApiErrorResponse>({
    queryKey: ['reporte', 'data', nombre, parametros],
    queryFn: async () => {
      try {
        return await getReportData(nombre, parametros);
      } catch (error) {
        throw parseApiError(error);
      }
    },
    enabled: enabled && !!nombre,
  });
}

/**
 * Hook de mutación para exportar un reporte en formato PDF o Excel.
 * Administra el estado de carga (isPending) para proveer retroalimentación visual.
 */
export function useExportarReporte() {
  return useMutation<
    Blob,
    ApiErrorResponse,
    { nombre: string; parametros: ReportParamDto[]; formato: 'pdf' | 'excel' }
  >({
    mutationFn: async ({ nombre, parametros, formato }) => {
      try {
        return await exportarReporte(nombre, parametros, formato);
      } catch (error) {
        throw parseApiError(error);
      }
    },
  });
}

/**
 * Hook para traer los complementos consolidados de un viaje (bitácora y agencias).
 */
export function useViajeComplementos(viajeId: string, enabled: boolean = true) {
  return useQuery<ViajeComplementosDto, ApiErrorResponse>({
    queryKey: ['viaje', viajeId, 'complementos'],
    queryFn: async () => {
      try {
        return await getViajeComplementos(viajeId);
      } catch (error) {
        throw parseApiError(error);
      }
    },
    enabled: enabled && !!viajeId,
  });
}
