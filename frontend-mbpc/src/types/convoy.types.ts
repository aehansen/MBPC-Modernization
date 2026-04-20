// src/types/convoy.types.ts
//
// Interfaces TypeScript que espejan exactamente los DTOs de C# en
// Mbpc.Api/DTOs/Convoy/ConvoyDto.cs.
// Cero `any`. Tipado fuerte estricto.

// ---------------------------------------------------------------------------
// Enum → string union (vocabulario de dominio centralizado)
// ---------------------------------------------------------------------------

/**
 * Mapea el enum `EstadoBarcaza` de C# a un string union de TypeScript.
 * Los valores son idénticos a los del servidor para evitar transformaciones.
 */
export type EstadoBarcaza =
  | 'EnTransito'
  | 'Amarrada'
  | 'Fondeada'
  | 'EnCarga'
  | 'EnDescarga'
  | 'FueraDeServicio';

// ---------------------------------------------------------------------------
// Records / Entidades
// ---------------------------------------------------------------------------

/**
 * Espeja `RemolcadorConvoyDto` (record de C#).
 * FechaSalida puede ser null si el remolcador aún no partió.
 */
export interface RemolcadorConvoyDto {
  id: string;
  nombre: string;
  estado: string;
  fechaSalida: string | null; // ISO 8601 — DateTimeOffset? en C#
}

/**
 * Espeja `BarcazaConvoyDto` (record de C#).
 * Los campos nullable del servidor llegan como string | null.
 */
export interface BarcazaConvoyDto {
  id: string;
  nombre: string;
  bandera: string;
  matricula: string | null;
  tipoCarga: string;
  tonelaje: number;
  unidad: string;
  muelleActual: string | null;
  estado: EstadoBarcaza;
}

/**
 * Espeja `ConvoyDto` (class de C#).
 * `tonelajeTotal` y `barcazasActivas` son propiedades calculadas en el servidor
 * y llegan serializadas en el JSON de respuesta.
 */
export interface ConvoyDto {
  viajeId: string;
  nombreBuque: string;
  remolcador: RemolcadorConvoyDto | null;
  barcazas: BarcazaConvoyDto[];
  tonelajeTotal: number;
  barcazasActivas: number;
}

// ---------------------------------------------------------------------------
// Payloads de entrada (Request bodies)
// ---------------------------------------------------------------------------

/**
 * Espeja `AmarrarBarcazaRequest`.
 * PUT /api/convoyes/barcazas/{barcazaId}/amarrar
 */
export interface AmarrarBarcazaRequest {
  nuevoMuelle: string;
}

/**
 * Espeja `FondearBarcazaRequest`.
 * PUT /api/convoyes/barcazas/{barcazaId}/fondear
 */
export interface FondearBarcazaRequest {
  zonaFondeo: string;
}
