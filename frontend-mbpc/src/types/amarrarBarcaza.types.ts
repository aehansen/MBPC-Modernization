// src/types/amarrarBarcaza.types.ts
// ──────────────────────────────────────────────────────────────────────────────
// DTOs para la funcionalidad "Amarrar Barcaza".
// Endpoint: PUT /api/carga/{id}/amarrar?nuevoMuelle={muelle}
// ──────────────────────────────────────────────────────────────────────────────

/**
 * Parámetros que recibe la mutación para amarrar una barcaza.
 * El `id` se interpola en la ruta y `nuevoMuelle` va como query param.
 */
export interface AmarrarBarcazaRequest {
  /** Identificador único de la carga/barcaza a amarrar. */
  id: string;
  /** Código o nombre del muelle de destino. */
  nuevoMuelle: string;
}

/**
 * Respuesta exitosa del backend (HTTP 200).
 */
export interface AmarrarBarcazaResponse {
  /** Mensaje confirmatorio devuelto por la API. */
  mensaje: string;
}

/**
 * Estructura del error devuelto por el backend (HTTP 400 / 404).
 * El campo `detail` es el estándar ProblemDetails de .NET.
 */
export interface AmarrarBarcazaError {
  /** Mensaje de error legible para el usuario. */
  mensaje: string;
  /** Detalle técnico adicional (opcional, ProblemDetails). */
  detail?: string;
  /** Código HTTP recibido. */
  status?: number;
}
