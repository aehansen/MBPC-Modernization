# MBPC — Arquitectura del Sistema
> Generado automáticamente como referencia para el Arquitecto de Software.
> Fecha: 2026-05-05

---

# Arquitectura del Sistema MBPC

## Stack Tecnológico
*   **Backend:** .NET 8
*   **Frontend:** React (Stateless UI Rendering), TailwindCSS
*   **Bases de Datos:** MongoDB (Principal para mutaciones y lecturas específicas), Oracle (Fallback/Legacy)

## Reglas y Patrones Estrictos
1.  **Patrón CQRS:** Separación estricta de responsabilidades entre lectura y escritura.
2.  **Scoping de Mutaciones (MongoDB):** Toda mutación (especialmente los DELETE) sobre documentos anidados (ej. Barcazas/Bodegas) DEBE forzar un doble filtrado por el identificador del padre (`ViajeId` / `VesselName`) y el ID del hijo para evitar eliminación global de documentos.
3.  **Lógica de Negocio en DTOs:** La inferencia de tipos (ej. distinguir "Bodega" ID 0 de "Barcaza") se resuelve en la Capa de Servicios (`CargaManagerService`) inyectando propiedades calculadas fuertemente tipadas en los DTOs (`TipoUnidad`).
4.  **Frontend Stateless (Enmascaramiento Visual):** El backend no formatea strings para UI. React recibe los IDs originales intactos (para garantizar integridad en PUT/DELETE) y se encarga del enmascaramiento visual (ej. ID "0" -> "-") y la renderización condicional (Badges).
5.  **Clean Code:** Prohibido dejar variables huérfanas o advertencias de compilador (ej. `CS0168` en bloques try-catch).

## 1. Visión General

El sistema MBPC (Modernización de Buques y Posicionamiento de Convoy) es una migración
progresiva de un sistema legacy ASP.NET / Oracle hacia una arquitectura híbrida moderna.

**Stack tecnológico activo:**
- **Backend:** .NET 8 — ASP.NET Core Web API
- **Frontend:** React + Vite + TypeScript
- **Base de datos operativa:** MongoDB (colecciones por dominio)
- **Base de datos legacy (fuente de verdad regulatoria):** Oracle (stored procedures)
- **Estado del servidor (Frontend):** TanStack Query v5 (React Query)
- **Estado del formulario (Frontend):** React Hook Form
- **Cache distribuida (Backend):** Redis (`IDistributedCache`)
- **Cache en memoria (Backend):** `IMemoryCache` (solo en `CargaManagerService`)
- **Resiliencia:** Polly (`AsyncRetryPolicy` con backoff exponencial para Oracle y Redis)

---

## 2. Principios de Diseño

### 2.1 Separación de responsabilidades por capa

```
[Cliente React]
      ↓  HTTP/REST (JWT Bearer)
[ASP.NET Core Controllers]  ← Inyecta CosteraId desde Claim JWT; valida ModelState
      ↓  Interfaces (DI)
[Manager Services]           ← Lógica de negocio, orquestación, máquina de estados
      ↓  Dual Write
[MongoDB]  ←→  [Oracle]     ← MongoDB: estado operativo moderno. Oracle: fuente de verdad legacy
```

### 2.2 Patrón CQRS (Command-Query Responsibility Segregation)

El sistema implementa CQRS de manera implícita a nivel de servicio:

**READS (Queries):**
- Leen primero de **MongoDB** (baja latencia, documentos desnormalizados).
- Si el documento no existe en Mongo → "hidratación": busca en Oracle y crea el documento.
- Resultado cacheado en Redis (`ViajeManagerService`) o IMemoryCache (`CargaManagerService`).

**WRITES (Commands):**
- Primero escriben en **Oracle** (stored procedure como fuente de verdad regulatoria).
- Si Oracle OK → **replican en MongoDB** (Load-Mutate-Save pattern sobre el documento).
- Invalidan la caché correspondiente **después** de la escritura exitosa en ambas bases.
- En ambiente `IsDevelopment()`: se omite Oracle con bypass explícito para evitar dependencia de VPN/red.

**Regla crítica:** La invalidación de caché ocurre DESPUÉS de que ambas escrituras son exitosas. Si Mongo falla pero Oracle fue exitoso, el sistema loguea el error pero no revierte Oracle (eventual consistency aceptada por diseño).

### 2.3 Multitenant Geográfico (CosteraId)

- **Todas** las entidades en MongoDB tienen un campo `CosteraId` (int, nullable).
- `CosteraId == 0` → Super Admin / registro global.
- `CosteraId > 0` → Registro restringido a esa jurisdicción costera.
- El Controller **nunca** acepta el CosteraId del cliente. Lo extrae del Claim `"CosteraId"` del JWT y lo inyecta en el DTO antes de llamar al servicio.
- Cada Manager Service resuelve el CosteraId internamente vía `ICosteraUserContext`.

### 2.4 Máquina de Estados del Buque (ViajeManagerService)

Las transiciones de estado son estrictamente validadas antes de cualquier escritura:

```
Amarrado ──────► Navegando
Navegando ──────► Amarrado
Navegando ──────► Fondeado
Fondeado ──────► Reanudado   ← PASO OBLIGATORIO (no se puede zarpar desde Fondeado)
Reanudado ──────► Navegando
Reanudado ──────► Amarrado
Reanudado ──────► Fondeado
```

- Las transiciones ilegales retornan HTTP 422 (Unprocessable Entity) con mensaje de dominio descriptivo.
- El método privado `CambiarEstadoConValidacionAsync` aplica la validación antes de delegar a `CambiarEstadoNavegacionAsync`.

### 2.5 Estrategia de Caché

| Servicio | Tipo de Caché | TTL | Clave | Invalidación |
|---|---|---|---|---|
| `ViajeManagerService` | Redis (IDistributedCache) | 2 min | `barcos:en_puerto:{costeraId}` / `viajes:mapa:{costeraId}` | Tras `IniciarViajeAsync` |
| `CargaManagerService` | IMemoryCache | 5 min abs / 2 min sliding | `cargas_viaje_{parametro}` | Tras cada mutación (AmarrarBarcaza, AgregarCarga, etc.) |
| `ConvoyManagerService` | Sin caché | — | — | Lee directo de MongoDB ("foto real") |

---

## 3. Flujo de Datos: Caso de Uso "Crear Viaje"

```
1. Usuario completa ModalNuevoViaje (React Hook Form)
2. onSubmit() mapea NuevoViajeFormValues → NuevoViajeRequest
   - Convierte datetime-local strings a ISO 8601 UTC
   - Convierte buqueId a Number, rioCanalKmPar a Number
3. useNuevoViaje (TanStack Mutation) → POST /api/viajes
4. ViajesController.IniciarViaje():
   a. Valida ModelState
   b. Extrae CosteraId del JWT → inyecta en NuevoViajeDto
5. ViajeManagerService.IniciarViajeAsync():
   a. FASE 1: Llama SP Oracle PKG_MBPC_VIAJES.SP_CREAR_VIAJE → obtiene TravelId
   b. FASE 2: Inserta ViajePosicionMongo en colección "last_mbpc"
   c. FASE 3: Inserta ViajeDetalleMongo en colección "details_mbpc" (con Etapa 1 vacía)
   d. FASE 4: Invalida cachés Redis (barcos en puerto + mapa)
6. Frontend recibe OK → invalidateQueries(['viajes']) → Dashboard se refresca
```

---

## 4. Flujo de Datos: Caso de Uso "Adjuntar Barcaza al Convoy"

```
1. Usuario abre PanelGestionConvoy → BarcazaAutocomplete (busca por nombre/matrícula)
2. ModalAdjuntar captura barcazaId + ubicacion
3. useAdjuntarBarcazas (TanStack Mutation) → POST /api/convoyes/{viajeId}/adjuntar
4. ConvoyController → ConvoyManagerService.AdjuntarBarcazasAsync():
   a. DEV BYPASS: Omite Oracle en desarrollo
   b. PROD: Llama SP Oracle mbpc.adjuntar_barcazas
   c. Busca ViajeDetalleMongo en MongoDB por viajeId
   d. Agrega nueva BarcazaMongo a la última Etapa del documento (Load-Mutate-Save)
5. Invalidación: queryClient.invalidateQueries({ queryKey: convoyKeys.all })
```

---

## 5. Gestión de Estado en el Frontend

### 5.1 Estado del Servidor (TanStack Query v5)

El frontend NO usa Zustand ni Redux. Todo estado asíncrono se gestiona con React Query:

| Hook | Query Key | Fuente | Invalidación |
|---|---|---|---|
| `useViajes(page, size, filtro)` | `['viajes', page, size, filtro]` | GET /api/viajes | Tras crear viaje |
| `useCargas(viajeId)` | `cargasKeys.byViaje(viajeId)` | GET /api/carga/viaje/:id | Tras cualquier mutación de carga |
| `useObtenerConvoy(viajeId)` | `['convoy', viajeId]` | GET /api/convoyes/viaje/:id | Tras adjuntar/separar/fondear |

**Asimetría conocida:** Las mutaciones de cargas invalidan a nivel granular (por `viajeId`), mientras que las mutaciones de convoy invalidan de forma global (`convoyKeys.all`).

### 5.2 Estado del Formulario (React Hook Form)

- `ModalNuevoViaje.tsx` usa `useForm<NuevoViajeFormValues>()`.
- Las fechas se manejan como `string` (compatible con `datetime-local`) y se convierten a ISO UTC en el `onSubmit`.
- El campo `buqueId` se resuelve mediante un autocomplete externo que llama a `/api/buques/autocomplete`.

### 5.3 Estado Local (useState)

Usado exclusivamente para:
- Control de visibilidad de modales (`isOpen`, `modalAbierto`)
- Control de steps o wizards de formulario (`showForm`)
- Estado de dropdowns de autocompletado (`showDropdown`, `suggestions`)
- IDs de items seleccionados en la UI (`viajeSeleccionadoId`, `cargaSeleccionada`)

---

## 6. Autenticación y Autorización

- **JWT Bearer Token** almacenado en `localStorage` bajo la clave `mbpc_token`.
- El `axiosClient.js` centralizado inyecta el header `Authorization: Bearer <token>` en todas las llamadas.
- Todos los endpoints del backend tienen `[Authorize]` a nivel de Controller.
- El Claim `"CosteraId"` es la llave de multitenant; si falta, el Controller retorna HTTP 403 Forbid.

---

## 7. Proyecto MCP Server (En Desarrollo)

Existe un proyecto `Mbpc.McpServer` en la solución que implementa el protocolo MCP (Model Context Protocol) para exposición de herramientas de negocio a LLMs (Gemini/Semantic Kernel). Actualmente en fase de diseño arquitectónico.

---

## 8. Reglas de Codificación a Respetar

1. **Nunca aceptar CosteraId del cliente.** Siempre inyectarlo desde el JWT en el Controller.
2. **Invalidar la caché SIEMPRE** después de mutaciones exitosas en `CargaManagerService`.
3. **`ConvoyManagerService` no usa caché** por diseño (necesita "foto real" del convoy).
4. **En producción, Oracle es la fuente de verdad regulatoria.** Mongo es la proyección operativa.
5. **El bypass de Oracle** (`_env.IsDevelopment()`) debe mantenerse en todos los métodos de escritura para evitar bloqueos de red en desarrollo.
6. **`BarcazaMongo.Nombre`** debe almacenarse siempre como el nombre real del buque, nunca el ID numérico (bug conocido como "bug 3000101").
7. **Las propiedades de respaldo BSON** (`EtapasLegacy`/`EtapasModern`, etc.) en `ViajeDetalleMongo` deben conservarse para tolerar documentos legacy pre-CQRS.
