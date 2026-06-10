export const UNIDADES_MEDIDA = [
  { value: 'TN', label: 'Toneladas (Tn)' },
  { value: 'M3', label: 'Metros Cúbicos (m³)' },
  { value: 'BBL', label: 'Barriles (BBL)' },
  { value: 'KG', label: 'Kilogramos (kg)' }
] as const;

export type UnidadMedida = typeof UNIDADES_MEDIDA[number]['value'];
