type SistemaExterno = 'DEB' | 'PBIP' | 'CIALA' | 'PROGEBU';

interface BuqueData {
  matricula?: string;
  nombre: string;
  omi?: string;
}

const BASE_URLS: Record<SistemaExterno, string> = {
  DEB:     'https://dsig.prefecturanaval.gob.ar/deb/consulta',
  PBIP:    'https://dsig.prefecturanaval.gob.ar/PBIP/consulta',
  CIALA:   'https://gcinfo.prefecturanaval.gob.ar/consultas/ciala',
  PROGEBU: 'https://dsig.prefecturanaval.gob.ar/progebu/buques',
};

export function buildExternalUrl(sistema: SistemaExterno, buqueData: BuqueData): string {
  const baseUrl   = BASE_URLS[sistema];
  const omi       = buqueData.omi ? encodeURIComponent(buqueData.omi) : '';
  const matricula = buqueData.matricula ? encodeURIComponent(buqueData.matricula) : 'null';

  if (sistema === 'PROGEBU') {
    return `${baseUrl}?matriculaDir=${matricula}`;
  }
  return `${baseUrl}?omi=${omi}`;
}
