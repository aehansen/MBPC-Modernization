# MBPC — Estado Actual del Proyecto
> Generado automáticamente como referencia para el Arquitecto de Software.
> Fecha: 2026-05-05

---

## 1. Backend — Endpoints Implementados

### 1.1 `ViajeController` — `/api/viajes`

| Método | Ruta | Estado | Descripción |
|---|---|---|---|
| GET | `/api/viajes` | ✅ Implementado | Lista paginada de viajes (filtro por nombre, paginación). Multitenant por CosteraId. |
| GET | `/api/viajes/{mmsi}` | ✅ Implementado | Viaje por MMSI |
| GET | `/api/viajes/puerto` | ✅ Implementado | Barcos en puerto (estado Amarrado/Fondeado). Usa caché Redis. |
| GET | `/api/viajes/historico` | ✅ Implementado | Histórico desde Oracle (con filtros: nombre, OMI, matrícula, fechas). |
| GET | `/api/viajes/mapa` | ✅ Implementado | Puntos del mapa AIS. Usa caché Redis. Filtro por MMSI o nombre. |
| POST | `/api/viajes` | ✅ Implementado | Crear nuevo viaje. Dual-write Oracle + MongoDB. CosteraId inyectado por JWT. |
| PUT | `/api/viajes/{id}/zarpar` | ✅ Implementado | Transición de estado: → Navegando. Con validación de máquina de estados. |
| PUT | `/api/viajes/{id}/amarrar` | ✅ Implementado | Transición de estado: → Amarrado. |
| PUT | `/api/viajes/{id}/fondear` | ✅ Implementado | Transición de estado: → Fondeado. |
| PUT | `/api/viajes/{id}/reanudar` | ✅ Implementado | Transición de estado: → Reanudado. |
| PUT | `/api/viajes/{id}/posicion` | ✅ Implementado | Actualiza posición AIS. Valida cinemática (Haversine, límite 60 kn). Escribe en tracklog. |

### 1.2 `CargaController` — `/api/carga`

| Método | Ruta | Estado | Descripción |
|---|---|---|---|
| GET | `/api/carga/viaje/{viajeId}` | ✅ Implementado | Lista de cargas de un viaje. Cache-aside con IMemoryCache. Fallback Oracle ↔ Mongo. |
| POST | `/api/carga/viaje/{viajeNombreBuque}` | ✅ Implementado | Agregar carga a un viaje. Dual-write Oracle + Mongo. |
| PUT | `/api/carga/{id}/amarrar` | ✅ Implementado | Amarrar barcaza a muelle (por query param `nuevoMuelle`). |
| PUT | `/api/carga/{id}/fondear` | ✅ Implementado | Fondear barcaza en zona. |
| PUT | `/api/carga/{id}/cargar` | ✅ Implementado | Registrar carga de toneladas. |
| PUT | `/api/carga/{id}/descargar` | ✅ Implementado | Registrar descarga de toneladas. |
| PUT | `/api/carga/{id}` | ✅ Implementado | Modificar datos de carga (BarcazaId, Tipo, Tonelaje). Requiere ViajeId en body para scoping. |
| DELETE | `/api/carga/viaje/{viajeId}/carga/{id}` | ✅ Implementado | Eliminar carga con scoping por viaje (fix bug Hito 5.8). |

### 1.3 `ConvoyController` — `/api/convoyes`

| Método | Ruta | Estado | Descripción |
|---|---|---|---|
| GET | `/api/convoyes/viaje/{viajeId}` | ✅ Implementado | Obtener composición del convoy (remolcador + barcazas). Sin caché (foto real). |
| POST | `/api/convoyes/{viajeId}/adjuntar` | ✅ Implementado | Adjuntar barcazas al convoy. Dual-write Oracle (con bypass DEV) + Mongo. |
| POST | `/api/convoyes/{viajeId}/separar` | ✅ Implementado | Separar barcazas del convoy. |
| PUT | `/api/convoyes/barcazas/{barcazaId}/amarrar` | ✅ Implementado | Amarrar barcaza dentro del contexto de convoy. |
| PUT | `/api/convoyes/barcazas/{barcazaId}/fondear` | ✅ Implementado | Fondear barcaza dentro del contexto de convoy. |

### 1.4 `BuqueController` — `/api/buques`

| Método | Ruta | Estado | Descripción |
|---|---|---|---|
| GET | `/api/buques/autocomplete` | ✅ Implementado | Autocomplete de buques por nombre/matrícula/OMI. Fuente: Oracle BUQUES_NEW. |
| GET | `/api/buques/barcazas/autocomplete` | ✅ Implementado | Autocomplete de barcazas disponibles para adjuntar a un convoy. |

### 1.5 `TipoCargaController` — `/api/tipocargas`

| Método | Ruta | Estado | Descripción |
|---|---|---|---|
| GET | `/api/tipocargas` | ✅ Implementado | Lista maestra de tipos de carga/mercadería. Fuente: Oracle TBL_TIPO_CARGA. |
| GET | `/api/tipocargas/{id}` | ✅ Implementado | Tipo de carga por ID de Oracle. |

### 1.6 `AuthController` — `/api/auth`

| Método | Ruta | Estado | Descripción |
|---|---|---|---|
| POST | `/api/auth/login` | ✅ Implementado | Login con usuario/contraseña. Retorna JWT. Embebe Claim CosteraId. |

### 1.7 `ChatController` — `/api/chat`

| Método | Ruta | Estado | Descripción |
|---|---|---|---|
| POST | `/api/chat` | 🔨 En Desarrollo | Chat con Gemini AI via Semantic Kernel. Registro de herramientas de negocio en progreso. |

---

## 2. Backend — Servicios Implementados

| Servicio | Interfaz | Estado | Notas |
|---|---|---|---|
| `ViajeManagerService` | `IViajeService` | ✅ Completo | Máquina de estados, AIS, Redis cache, hidratación Mongo←Oracle |
| `CargaManagerService` | `ICargaService` | ✅ Completo | IMemoryCache, dual-write, scoping por ViajeId implementado |
| `ConvoyManagerService` | `IConvoyManagerService` | ✅ Completo | Sin caché, Load-Mutate-Save, bypass DEV para Oracle |
| `BuqueManagerService` | `IBuqueService` | ✅ Completo | Autocomplete desde Oracle BUQUES_NEW |
| `TipoCargaManagerService` | `ITipoCargaService` | ✅ Completo | Fuente Oracle TBL_TIPO_CARGA |
| `ChatManagerService` | `IChatService` | 🔨 En Desarrollo | Integración Semantic Kernel + Gemini. Tools de negocio pendientes. |
| `Mbpc.McpServer` | — | 📋 Diseño | Proyecto MCP Server para integración con LLMs vía stdio. En planificación. |

---

## 3. Frontend — Módulos Implementados

### 3.1 Páginas

| Archivo | Estado | Descripción |
|---|---|---|
| `pages/Login.jsx` | ✅ Completo | Autenticación. Guarda JWT en localStorage. |
| `pages/ViajesPage.tsx` | ✅ Completo | Página principal. Contiene dashboard + mapa AIS. |

### 3.2 Componentes — Viajes

| Archivo | Estado | Descripción |
|---|---|---|
| `viajes/ViajesDashboard.tsx` | ✅ Completo | Tabla paginada de viajes. Filtro server-side con debounce 500ms. |
| `viajes/ModalNuevoViaje.tsx` | ✅ Completo | Formulario de creación de viaje. React Hook Form + autocomplete de buque. |
| `viajes/ModalActualizarPosicion.tsx` | ✅ Completo | Formulario de actualización de posición AIS. Validaciones cinemáticas. |
| `viajes/ModalHistorico.tsx` | ✅ Completo | Búsqueda histórica desde Oracle. |
| `BotonZarpar.tsx` | ✅ Completo | Botón de acción con validación de estado actual. |
| `BotonesAccionViaje.tsx` | ✅ Completo | Botones Amarrar / Fondear / Reanudar con lógica de estado. |

### 3.3 Componentes — Cargas

| Archivo | Estado | Descripción |
|---|---|---|
| `cargas/CargasModal.tsx` | ✅ Completo | Modal con tabla de cargas + formulario de nueva carga. Autocomplete de barcaza y mercadería. |
| `cargas/CargaEditModal.jsx` | ✅ Completo | Modal de edición de carga. Envía ViajeId para scoping. |
| `cargas/CargaDeleteModal.jsx` | ✅ Completo | Modal de confirmación de eliminación. Scoping por viajeId implementado. |
| `cargas/TipoCargaAutocomplete.jsx` | ✅ Completo | Autocomplete de mercadería/naturaleza desde Oracle. |
| `cargas/CargasTable.jsx` | ⚠️ Potencialmente Obsoleto | Tabla de cargas standalone. Puede solaparse con CargasModal. Revisar si se usa. |

### 3.4 Componentes — Convoy

| Archivo | Estado | Descripción |
|---|---|---|
| `convoy/PanelGestionConvoy.tsx` | ✅ Completo | Panel de gestión del convoy. Adjuntar/Separar/Fondear barcazas. Modales integrados. |
| `convoy/BarcazaAutocomplete.tsx` | ✅ Completo | Autocomplete de barcaza para el modal de adjuntar. |
| `ModalAmarrarBarcaza.tsx` | ⚠️ Verificar uso | Modal de amarrar barcaza. Verificar si es reemplazado por la lógica dentro del Panel. |

### 3.5 Componentes — Chat

| Componente | Estado | Descripción |
|---|---|---|
| `chat/` (directorio) | 🔨 En Desarrollo | Integración de chat con Gemini AI. |

### 3.6 Hooks

| Hook | Estado | Descripción |
|---|---|---|
| `useViajesApi.ts` | ✅ Completo | `useViajes` (paginación), `useNuevoViaje`, acciones de estado (zarpar, amarrar, etc.) |
| `useCargasApi.ts` | ✅ Completo | `useCargas`, `useCrearCarga`, `useAmarrarCarga`, `useFondearCarga`, `useCargarToneladas`, `useDescargarToneladas` |
| `useGestionConvoy.ts` | ✅ Completo | `useAdjuntarBarcazas`, `useSepararConvoy`, `useFondearBarcaza` |
| `useNuevoViaje.ts` | ✅ Completo | Mutación para POST /api/viajes |
| `useActualizarPosicion.ts` | ✅ Completo | Mutación para PUT /api/viajes/{id}/posicion |
| `useAccionesViaje.ts` | ✅ Completo | Wrapper de hooks de acciones de estado del buque |
| `useAmarrarBarcaza.ts` | ✅ Completo | Mutación amarrar dentro del contexto de convoy |
| `useBuscarBarcazas.ts` | ✅ Completo | Query de autocomplete de barcazas |
| `useZarpar.ts` | ✅ Completo | Mutación zarpar con invalidación de queries |
| `useViajes.ts` | ⚠️ Duplicado potencial | Verificar si es distinto de `useViajesApi.ts` o es un alias. |

---

## 4. Bugs Conocidos y Pendientes

| ID | Descripción | Estado | Ubicación |
|---|---|---|---|
| Bug "A Definir" | Las barcazas adjuntadas vía convoy (`ConvoyManagerService`) se insertan en `Etapas.First()` pero la lectura de cargas busca en `Etapas.Last()`. La barcaza recién adjuntada no aparece en `CargasModal`. | 🔴 Pendiente Fix | `CargaManagerService.AgregarCargaAsync` vs `ObtenerCargasDesdeMongoDb` |
| Bug "3000101" | El nombre de la barcaza se almacena como el ID numérico en lugar del nombre real cuando el campo `BarcazaNombre` del DTO no se mapea correctamente al `BarcazaMongo.Nombre`. | 🔴 Pendiente Fix | `CargaManagerService.AgregarCargaAsync` |
| Warning OracleException | Los bloques `catch (OracleException ex)` en métodos de escritura de `CargaManagerService` no usan la variable `ex` en el branch DEV (solo `exitoOracle = true`). Genera warning CS0168. | 🟡 Warning | `CargaManagerService` líneas 315, 367, 419, 471, 529 |
| Cascading State | Las transiciones de estado del buque (ej: Zarpar) no disparan actualización automática en `CargaManagerService` para las barcazas asociadas del convoy. La actualización en cascada está pendiente de implementar. | 🔴 Pendiente — Refactor Mayor | `ViajeManagerService.CambiarEstadoNavegacionAsync` |

---

## 5. Módulos Identificados Como Pendientes

| Módulo | Descripción |
|---|---|
| Chat AI completo | Integración funcional de Gemini con herramientas de negocio (ViajeManagerService, ConvoyManagerService como tools del Kernel). |
| MCP Server | Proyecto `Mbpc.McpServer` — exposición de servicios de negocio vía Model Context Protocol. |
| Cascading State Updates | Propagación automática de cambios de estado del buque principal a las barcazas del convoy en `CargaManagerService`. |
| Reconciliación Mongo ↔ Oracle | Job o mecanismo de reconciliación para casos donde Fase 1 (Oracle) fue exitosa pero Fase 2/3 (Mongo) falló. |
| Tests de integración | El proyecto `Mbpc.Api.Tests` existe pero el volumen de cobertura es incierto. Verificar. |

# Log Operativo y Estado del Proyecto

## Último Hito Completado (Hito 5.8 - Mayo 2026)
*   **Objetivo Logrado:** Scoping seguro en MongoDB para mutaciones, enmascaramiento visual en UI y limpieza de código CQRS.
*   **Componentes Modificados:** `CargaManagerService.cs`, `IViajeService.cs`, `CargaDto.cs`, `CargasTable.jsx`.
*   **Deuda Técnica resuelta:** Fix crítico de integridad en `.RemoveAll` de MongoDB; sincronización de contratos de interfaz; enmascaramiento de IDs en React.

## Hito Actual en Desarrollo (Hito 5.9)
*   **Objetivo:** Orquestación e Hidratación de Datos del Convoy (Catálogo vs Viaje).
*   **Problema a resolver:** `CargaManagerService` genera DTOs "dummy" ("A Definir" / 0 toneladas) para barcazas sin carga declarada en el convoy, mostrando IDs crudos (ej. 3000101) por falta de cruce de datos.
*   **Solución requerida:** Triangulación de información con el catálogo maestro de buques/barcazas (actualmente mockeado en `BuqueManagerService`) para hidratar los DTOs y mostrar nombres reales (ej. "UABL 101 (PY-101)") en la interfaz en lugar del ID numérico.
