import { useQuery } from '@tanstack/react-query';
import { reporteApi } from '../axiosClient';

export interface ReportParamDto {
  name: string;
  value?: string | null;
}

export const reportesKeys = {
  all: ['reportes'] as const,
  detail: (nombre: string, filtros: ReportParamDto[]) => [...reportesKeys.all, nombre, filtros] as const,
};

/**
 * Convierte una lista de parámetros de reporte al formato esperado por el binding
 * de ASP.NET [FromQuery] para la clase ReporteRequestDto.
 */
export function buildReportParams(filtros: ReportParamDto[]): Record<string, string> {
  const params: Record<string, string> = {};
  filtros.forEach((f, idx) => {
    params[`Parametros[${idx}].Name`] = f.name;
    if (f.value !== undefined && f.value !== null) {
      params[`Parametros[${idx}].Value`] = f.value;
    }
  });
  return params;
}

async function fetchReporteData(
  nombre: string,
  filtros: ReportParamDto[]
): Promise<Record<string, string | number | boolean | null>[]> {
  const queryParams = buildReportParams(filtros);
  const { data } = await reporteApi.getData(nombre, queryParams);
  return data;
}

export function useReporte(nombre: string, filtros: ReportParamDto[]) {
  return useQuery<Record<string, string | number | boolean | null>[], Error>({
    queryKey: reportesKeys.detail(nombre, filtros),
    queryFn: () => fetchReporteData(nombre, filtros),
    enabled: !!nombre,
  });
}
