/**
 * src/utils/coordinates.ts
 *
 * Utilidad de parsing robusto para coordenadas geográficas.
 * Soporta:
 *   - Grados Decimales puros:         "-34.6037, -58.3816"
 *   - DMS completo con símbolos:       "34° 35' 29\" S, 58° 21' 22\" W"
 *   - DMS sin símbolos (solo espacios): "34 35 29 S 58 21 22 W"
 *   - DM (sin segundos):               "34° 35.48' S, 58° 21.37' W"
 *   - Combinaciones N/S/E/W mayúsculas y minúsculas.
 *   - Separadores de componentes: coma, punto y coma, espacio múltiple.
 */

export interface CoordenadasDecimales {
  lat: number;
  lng: number;
}

// ── Regex auxiliares ──────────────────────────────────────────────────────────

/** Captura un número decimal con punto o coma como separador decimal. */
const RE_NUMERO = /[\d]+(?:[.,]\d+)?/;

/**
 * Patrón DMS: grados, minutos opcionales, segundos opcionales, hemisferio.
 * Ejemplo: "34° 35' 29.5\" S" o "34 35 29 S"
 */
const RE_DMS = new RegExp(
  `(${RE_NUMERO.source})` +               // grados
  `[°\\s]+` +                             // separador grados
  `(?:(${RE_NUMERO.source})['\u2032\\s]+` + // minutos (opcional)
  `(?:(${RE_NUMERO.source})["\u2033\\s]*)?)?` + // segundos (opcional)
  `([NSEWnsew])`,                          // hemisferio
  "g"
);

/**
 * Normaliza un string de entrada: convierte coma decimal a punto,
 * colapsa espacios múltiples y quita caracteres de control.
 */
function normalizar(input: string): string {
  return input
    .trim()
    .replace(/\r?\n/g, " ")
    .replace(/\s{2,}/g, " ");
}

/**
 * Convierte grados, minutos y segundos a grados decimales.
 * Aplica signo negativo para hemisferios S u O/W.
 */
function dmsADecimal(
  grados: number,
  minutos: number,
  segundos: number,
  hemisferio: string
): number {
  const decimal = grados + minutos / 60 + segundos / 3600;
  return /[SsOoWw]/.test(hemisferio) ? -decimal : decimal;
}

/** Reemplaza coma decimal por punto para parseFloat. */
function parseNum(s: string): number {
  return parseFloat(s.replace(",", "."));
}

// ── Parser DMS ────────────────────────────────────────────────────────────────

/**
 * Intenta parsear el input como DMS.
 * Retorna un par [lat, lng] o null si no encontró exactamente dos componentes.
 */
function parsearDMS(input: string): CoordenadasDecimales | null {
  const normalizado = normalizar(input);
  RE_DMS.lastIndex  = 0;

  const matches: Array<{ decimal: number; hemisferio: string }> = [];
  let match: RegExpExecArray | null;

  while ((match = RE_DMS.exec(normalizado)) !== null) {
    const grados   = parseNum(match[1] ?? "0");
    const minutos  = parseNum(match[2] ?? "0");
    const segundos = parseNum(match[3] ?? "0");
    const hemisferio = match[4]!;

    matches.push({
      decimal   : dmsADecimal(grados, minutos, segundos, hemisferio),
      hemisferio: hemisferio.toUpperCase(),
    });
  }

  if (matches.length < 2) return null;

  // Identificar cuál match es latitud (N/S) y cuál longitud (E/W/O)
  const latMatch = matches.find(m => /[NS]/.test(m.hemisferio));
  const lngMatch = matches.find(m => /[EWO]/.test(m.hemisferio));

  if (!latMatch || !lngMatch) return null;

  const lat = latMatch.decimal;
  const lng = lngMatch.decimal;

  return esRangoValido(lat, lng) ? { lat, lng } : null;
}

// ── Parser Decimal ────────────────────────────────────────────────────────────

/**
 * Regex para dos números decimales separados por coma, punto y coma o espacio.
 * Soporta signo negativo/positivo explícito.
 */
const RE_DECIMAL = /^([+-]?\d{1,3}(?:[.,]\d+)?)\s*[,;]\s*([+-]?\d{1,3}(?:[.,]\d+)?)$/;

function parsearDecimal(input: string): CoordenadasDecimales | null {
  const normalizado = normalizar(input);
  const m = RE_DECIMAL.exec(normalizado);
  if (!m) return null;

  const lat = parseNum(m[1]!);
  const lng = parseNum(m[2]!);

  return esRangoValido(lat, lng) ? { lat, lng } : null;
}

// ── Validación de rango ───────────────────────────────────────────────────────

function esRangoValido(lat: number, lng: number): boolean {
  return (
    isFinite(lat) && isFinite(lng) &&
    lat >= -90  && lat <= 90 &&
    lng >= -180 && lng <= 180
  );
}

// ── Función principal exportada ───────────────────────────────────────────────

/**
 * Parsea una cadena de texto a coordenadas decimales WGS-84.
 *
 * Estrategia:
 *   1. Intenta DMS (más específico).
 *   2. Si falla, intenta grados decimales puros.
 *   3. Retorna null si ningún formato aplica o si el rango es inválido.
 *
 * @example
 * parseCoordinates("34° 35' 29\" S, 58° 21' 22\" W")
 * // → { lat: -34.591388, lng: -58.356111 }
 *
 * parseCoordinates("-34.6037, -58.3816")
 * // → { lat: -34.6037, lng: -58.3816 }
 *
 * parseCoordinates("999, 999")
 * // → null (rango inválido)
 */
export function parseCoordinates(input: string): CoordenadasDecimales | null {
  if (!input || input.trim().length === 0) return null;

  return parsearDMS(input) ?? parsearDecimal(input);
}

/**
 * Formatea coordenadas decimales como DMS legible.
 * Útil para mostrar en tooltips o confirmaciones de UI.
 */
export function formatearDMS(lat: number, lng: number): string {
  const fmt = (val: number, esLat: boolean): string => {
    const abs  = Math.abs(val);
    const g    = Math.floor(abs);
    const mDec = (abs - g) * 60;
    const m    = Math.floor(mDec);
    const s    = ((mDec - m) * 60).toFixed(1);
    const dir  = esLat
      ? (val >= 0 ? "N" : "S")
      : (val >= 0 ? "E" : "W");
    return `${g}° ${m}' ${s}" ${dir}`;
  };

  return `${fmt(lat, true)}, ${fmt(lng, false)}`;
}
