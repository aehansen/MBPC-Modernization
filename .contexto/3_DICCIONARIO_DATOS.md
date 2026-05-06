# MBPC — Diccionario de Datos y Entidades de Dominio
> Generado automáticamente como referencia para el Arquitecto de Software.
> Fecha: 2026-05-05

---

## 1. Entidades de Dominio (Modelos)

### 1.1 `EstadoEtapa` (Enum)
**Namespace:** `Mbpc.Api.Models`
**Archivo:** `Mbpc.Api/Models/EstadoEtapa.cs`

Representa el estado operativo de un buque en una etapa de su viaje.

| Valor | Descripción |
|---|---|
| `Amarrado` | Buque amarrado en muelle. Estado inicial al crear un viaje. |
| `Navegando` | Buque en tránsito (zarpó). |
| `Fondeado` | Buque detenido en zona de fondeo (ancla). |
| `Reanudado` | Buque reanudó movimiento después de fondear. Paso intermedio obligatorio antes de zarpar. |

### 1.2 `Carga` (Modelo de Dominio — Legado)
**Namespace:** `Mbpc.Api.Models`
**Archivo:** `Mbpc.Api/Models/Carga.cs`

Modelo de dominio básico. Actualmente desplazado por `BarcazaMongo` en el flujo operativo.

| Campo | Tipo | Descripción |
|---|---|---|
| `Id` | int | Identificador de la carga |
| `ViajeId` | int | Relación con el viaje |
| `TipoMercaderia` | string | Tipo de mercadería transportada |
| `Toneladas` | double | Peso en toneladas métricas |
| `CantidadUnidades` | int | Cantidad de unidades |
| `EsPeligrosa` | bool | Indica mercancía IMO peligrosa |

---

## 2. Colecciones MongoDB

### 2.1 `ViajePosicionMongo` — Colección: `last_mbpc`
**Archivo:** `Mbpc.Api/Models/Mongo/ViajePosicionMongo.cs`

Documento de posición activa del buque. Es el registro "caliente" que actualiza el AIS.
**Nota:** No contiene listado de barcazas/convoy. Esa información vive en `ViajeDetalleMongo`.

| Campo BSON | Propiedad C# | Tipo | Descripción |
|---|---|---|---|
| `_id` | `Id` | string (ObjectId) | Identificador MongoDB |
| `TravelId` | `TravelId` | long | ID del viaje en Oracle |
| `VesselName` | `VesselName` | string | Nombre del buque (en mayúsculas) |
| `MMSI` | `Mmsi` | string? | Número MMSI del transponder AIS |
| `IMO` | `Imo` | int? | Número IMO internacional |
| `CallSign` | `CallSign` | string? | Indicativo de llamada |
| `Latitude` | `Latitude` | double | Latitud actual |
| `Longitude` | `Longitude` | double | Longitud actual |
| `NavegationStatusDesc` | `NavegationStatusDesc` | string | Estado de navegación como string (ej: "Amarrado") |
| `SpeedOverGroud` *(typo intencional)* | `SpeedOverGround` | double | Velocidad sobre el fondo |
| `CourseOverGround` | `CourseOverGround` | double | Rumbo sobre el fondo |
| `msgTime` | `MsgTime` | DateTime | Timestamp del último mensaje AIS |
| `Origin` | `Origin` | string? | Puerto de origen |
| `Destination` | `Destination` | string? | Puerto de destino |
| `location` | `Location` | LocationMongo? | Coordenadas GeoJSON (Point) |
| `CosteraId` | `CosteraId` | int? | ID de costera para multitenant. 0 = global. |

**Clases anidadas:**
- `LocationMongo { geo: GeoMongo }`
- `GeoMongo { type: string, coordinates: double[] }` — Formato GeoJSON: `[longitud, latitud]`

---

### 2.2 `ViajeDetalleMongo` — Colección: `details_mbpc`
**Archivo:** `Mbpc.Api/Models/Mongo/ViajeDetalleMongo.cs`

Documento operativo del viaje. Contiene la estructura de etapas, barcazas del convoy, remolcador, etc.
Implementa **propiedades de respaldo** para tolerar documentos legacy (campos en UPPERCASE, camelCase y PascalCase).

| Campo BSON | Propiedad C# | Tipo | Descripción |
|---|---|---|---|
| `_id` | `Id` | string (ObjectId) | Identificador MongoDB |
| `IdViaje` | `IdViaje` | long | ID del viaje en Oracle (cross-reference con TravelId) |
| `VesselName` | `VesselName` | string? | Nombre del buque |
| `Origin` | `Origin` | string? | Puerto de origen |
| `Destination` | `Destination` | string? | Puerto de destino |
| `ETAPAS` / `etapas` | `EtapasLegacy` / `EtapasModern` | List\<EtapaMongo\>? | Etapas del viaje (legacy UPPERCASE / modern camelCase) |
| `BARCAZAS` / `barcazas` | `BarcazasRootLegacy` / `BarcazasRootModern` | List\<BarcazaMongo\>? | Barcazas a nivel raíz (pre-CQRS) |
| `CosteraId` | `CosteraId` | int? | Multitenant |

**Propiedad virtual calculada:**
- `Etapas` → Retorna `EtapasModern ?? EtapasLegacy`. Al asignar, siempre escribe en `EtapasModern`.
- `BarcazasLegacy` → Retorna `BarcazasRootModern ?? BarcazasRootLegacy`.

---

### 2.3 `EtapaMongo` (embebida en `ViajeDetalleMongo`)
**Archivo:** `Mbpc.Api/Models/Mongo/ViajeDetalleMongo.cs`

Representa una etapa operativa dentro del viaje (ej: tramo con determinado convoy).

| Campo BSON | Propiedad C# | Tipo | Descripción |
|---|---|---|---|
| `ETAPA_ID` / `etapaId` / `EtapaId` | `EtapaId` (calculado) | long | ID de etapa |
| `FECHA_INICIO` / `fechaInicio` / `FechaInicio` | `FechaInicio` (calculado) | DateTime? | Fecha de inicio de la etapa |
| `REMOLCADOR` / `remolcador` | `Remolcador` (calculado) | RemolcadorMongo? | Remolcador de la etapa |
| `BARCAZAS` / `barcazas` | `Barcazas` (calculado) | List\<BarcazaMongo\>? | Barcazas de la etapa |

---

### 2.4 `BarcazaMongo` (embebida en `EtapaMongo`)
**Archivo:** `Mbpc.Api/Models/Mongo/ViajeDetalleMongo.cs`

Representa una barcaza dentro de una etapa del convoy.
**Regla crítica:** `Nombre` debe ser el nombre real de la embarcación, NUNCA el ID numérico.

| Campo BSON | Propiedad C# | Tipo | Descripción |
|---|---|---|---|
| `ID_VIAJE` / `idViaje` | `IdViaje` (calculado) | long | ID del viaje al que pertenece |
| `BARCAZA` / `nombre` | `Nombre` (calculado) | string | Nombre real de la barcaza |
| `BANDERA` / `bandera` | `Bandera` (calculado) | string | Bandera (país de registro) |
| `MATRICULA` / `matricula` | `Matricula` (calculado) | string? | Matrícula oficial |
| `CARGA` / `carga` | `Carga` (calculado) | string | Descripción de la carga transportada |
| `CANTIDAD` / `cantidad` | `Cantidad` (calculado) | double | Cantidad en toneladas |
| `UNIDAD` / `unidad` | `Unidad` (calculado) | string | Unidad de medida (ej: "TN") |
| `MUELLE_ACTUAL` / `muelleActual` | `MuelleActual` (calculado) | string? | Muelle donde está amarrada |
| `MERCADERIA_ID` / `mercaderiaId` | `MercaderiaId` (calculado) | int? | ID de Oracle de la mercadería (TBL_TIPO_CARGA) |

---

### 2.5 `RemolcadorMongo` (embebida en `EtapaMongo`)
**Archivo:** `Mbpc.Api/Models/Mongo/ViajeDetalleMongo.cs`

| Campo BSON | Propiedad C# | Tipo | Descripción |
|---|---|---|---|
| `NOMBRE` / `nombre` | `Nombre` (calculado) | string? | Nombre del remolcador |
| `MATRICULA` / `matricula` | `Matricula` (calculado) | string? | Matrícula del remolcador |

---

### 2.6 `ViajeTracklogMongo` — Colección: `tracklog_mbpc`
**Archivo:** `Mbpc.Api/Models/Mongo/ViajeTracklogMongo.cs`

Registro **inmutable** de cada actualización de posición AIS. Nunca se modifica; solo se inserta.
Permite reconstruir la trayectoria completa del buque.

| Campo BSON | Tipo | Descripción |
|---|---|---|
| `PosicionId` | string (ObjectId) | Referencia al documento padre en `last_mbpc` |
| `TravelId` | long | ID en Oracle |
| `VesselName` | string | Nombre del buque |
| `MMSI` | string? | MMSI del transponder |
| `Latitude` / `Longitude` | double | Posición geográfica en este momento |
| `SpeedOverGroud` | double | Velocidad reportada por el transponder |
| `CalculatedSpeedKnots` | double | Velocidad calculada por Haversine entre este punto y el anterior |
| `DistanceNM` | double | Distancia en millas náuticas desde la posición anterior |
| `NavegationStatusDesc` | string | Estado de navegación en el momento del reporte |
| `msgTime` | DateTime | Timestamp del transponder AIS |
| `insertedAt` | DateTime | Timestamp de inserción en servidor (UTC) |
| `CosteraId` | int? | Multitenant |
| `location` | LocationMongo? | GeoJSON Point |

---

### 2.7 `TipoCargaMongo` — Colección: (maestro interno)
**Archivo:** `Mbpc.Api/Models/Mongo/TipoCargaMongo.cs`

Maestro de tipos de carga/mercadería. Fuente: Oracle `TBL_TIPO_CARGA`.

| Campo | Tipo | Descripción |
|---|---|---|
| `Id` | int | ID de Oracle |
| `Nombre` | string | Nombre de la mercadería |
| `EsPeligrosa` | bool | Indica carga peligrosa (IMO) |

---

## 3. DTOs de Transferencia

### 3.1 DTOs de Viaje

| DTO | Dirección | Descripción |
|---|---|---|
| `ViajeDto` | Backend → Frontend | Resumen de viaje para lista/tarjeta del dashboard. Incluye Barcazas, Remolcador, Etapas, Practicos, Inspectores. |
| `NuevoViajeDto` | Frontend → Backend | Payload de creación de viaje. CosteraId se inyecta server-side desde JWT. |
| `ViajeHistoricoDto` | Backend → Frontend | Datos del histórico Oracle. Contiene: Id, Buque, OMI, Matrícula, Origen, Destino, FechaPartida, ETA, Estado, CosteraId. |
| `MapaViajeDto` | Backend → Frontend | Punto del mapa AIS. Contiene: Id, NombreBuque, MMSI, IMO, Latitud, Longitud, Velocidad, Rumbo, EstadoNav, UltimaActualizacion, TieneDetalleOperativo, CantidadBarcazas, Remolcador. |
| `BarcoPuertoDto` | Backend → Frontend | Barco en puerto. Contiene: Id, Buque, Origen, Destino, Eta, Estado, Mmsi. |
| `ActualizarPosicionDto` | Frontend → Backend | Payload de actualización AIS. Contiene: Latitud, Longitud, FechaReporte (DateTime). |
| `PosicionActualizadaResultDto` | Backend → Frontend | Resultado de actualización de posición. Contiene: VesselName, Latitud, Longitud, VelocidadCalculadaKn, DistanciaRecorridaNM, TracklogId. |
| `FiltroHistoricoDto` | Frontend → Backend | Filtros de búsqueda histórica: Nombre, Omi, Matricula, Origen, Destino, Desde, Hasta. |

### 3.2 DTOs de Carga

| DTO | Dirección | Descripción |
|---|---|---|
| `CargaDto` | Backend → Frontend | Carga en la grilla. Contiene: Id, ViajeId, DescripcionLista, NivelRiesgo, MuelleActual, Tonelaje, TipoUnidad. |
| `NuevaCargaDto` | Frontend → Backend | Nueva carga. Contiene: BarcazaId (long), BarcazaNombre (string?, para desnormalizar nombre real en Mongo), Tipo (Barcaza/Bodega), Tonelaje, MercaderiaId (int, FK Oracle). |
| `ModificarCargaDto` | Frontend → Backend | Modificación de carga. Requiere ViajeId obligatorio (scoping). Contiene: ViajeId, BarcazaId, Tipo, Tonelaje. |

### 3.3 DTOs de Convoy

| DTO | Dirección | Descripción |
|---|---|---|
| `ConvoyDto` | Backend → Frontend | Convoy completo. Contiene: ViajeId, NombreBuque, Remolcador (RemolcadorConvoyDto), Barcazas (IReadOnlyList\<BarcazaConvoyDto\>), TonelajeTotal (calculado), BarcazasActivas (calculado). |
| `BarcazaConvoyDto` | Backend → Frontend | Barcaza dentro del convoy. Contiene: Id, Nombre, Bandera, Matricula, TipoCarga, Tonelaje, Unidad, MuelleActual, Estado (EstadoBarcaza enum). |
| `RemolcadorConvoyDto` | Backend → Frontend | Remolcador del convoy. Contiene: Id, Nombre, Estado, FechaSalida. |
| `AdjuntarBarcazasRequest` | Frontend → Backend | Lista de BarcazasIds (string[]) + Ubicacion (punto de maniobra). |
| `SepararConvoyRequest` | Frontend → Backend | Lista de BarcazasIds (string[]) + Ubicacion (destino final). |
| `AmarrarBarcazaRequest` | Frontend → Backend | NuevoMuelle (string). |
| `FondearBarcazaRequest` | Frontend → Backend | ZonaFondeo (string). |
| `EstadoBarcaza` (Enum) | — | Valores: EnTransito, Amarrada, Fondeada, EnCarga, EnDescarga, FueraDeServicio. |

### 3.4 DTOs de Buque

| DTO | Dirección | Descripción |
|---|---|---|
| `BuqueAutocompleteDto` | Backend → Frontend | Sugerencia del autocomplete de buques. Contiene: IdBuque, Nombre, Omi, Sdist, Matricula, Bandera, Tipo, Estado, Costera. |

### 3.5 DTOs de Tipo de Carga

| DTO | Dirección | Descripción |
|---|---|---|
| `TipoCargaDto` | Backend → Frontend | Tipo de mercadería. Contiene: Id (int, Oracle), Nombre, EsPeligrosa (bool). |

### 3.6 DTOs de Reporte

| DTO | Dirección | Descripción |
|---|---|---|
| `ReportesDto` | — | DTO de estructura de reportes. Ver archivo `ReportesDto.cs` para detalle. |

### 3.7 DTOs de Chat

| DTO | Dirección | Descripción |
|---|---|---|
| `ChatDto` | Frontend → Backend | Mensaje de chat para Gemini AI. |

---

## 4. DTOs del Frontend (TypeScript)

### 4.1 `viajes.types.ts`

| Tipo/Interface | Descripción |
|---|---|
| `ViajeDto` | Viaje en dashboard: id, buque, ruta, fechaInicioFormateada, estadoActual, costeraId |
| `ViajeHistoricoDto` | Viaje histórico Oracle: id, buque, omi, matricula, origen, destino, fechaPartida, eta, estado, costeraId |
| `NuevoViajeRequest` | Payload POST /api/viajes. costeraId es readonly y omitido por el cliente. |
| `NuevoViajeFormValues` | Tipo interno de react-hook-form. Fechas como string (datetime-local). |
| `NuevoViajeResponse` | Respuesta de creación: viajeId (number), mensaje |
| `DeclaracionMalvinasEnum` | Enum con 20 valores de declaración Malvinas. Cada valor incluye letra identificadora (ej: `_L`, `_M`, `_B`). |
| `DECLARACION_MALVINAS_LABELS` | Record de etiquetas legibles para el select del formulario. |
| `ActualizarPosicionRequest` | latitud, longitud, fechaReporte (ISO string) |
| `ActualizarPosicionResponse` | velocidadCalculadaKn, distanciaRecorridaNM |

### 4.2 `cargas.types.ts`

| Tipo/Interface | Descripción |
|---|---|
| `CargaDto` | id, viajeId, descripcionLista, nivelRiesgo, muelleActual, tonelaje, tipoUnidad |
| `TipoCarga` | Union type: `'Barcaza' \| 'Bodega'` |

### 4.3 `convoy.types.ts`

| Tipo/Interface | Descripción |
|---|---|
| `ConvoyDto` | viajeId, nombreBuque, remolcador, barcazas[] |
| `BarcazaConvoyDto` | id, nombre, bandera, matricula, tipoCarga, tonelaje, unidad, muelleActual, estado |
| `EstadoBarcaza` | Union type: `'EnTransito' \| 'Amarrada' \| 'Fondeada' \| 'EnCarga' \| 'EnDescarga' \| 'FueraDeServicio'` |

### 4.4 `amarrarBarcaza.types.ts`

| Tipo/Interface | Descripción |
|---|---|
| (Ver archivo) | Tipos relacionados a la operación de amarrar dentro del contexto de convoy. |

---

## 5. Tablas Oracle Identificadas

| Tabla / SP / Package | Descripción |
|---|---|
| `PKG_MBPC_VIAJES.SP_CREAR_VIAJE` | Crea un nuevo viaje. Parámetros de output: p_RESULTADO (int), p_ID_VIAJE_GENERADO (long). |
| `PKG_MBPC_VIAJES.SP_HISTORICO` | Retorna histórico de viajes. |
| `PKG_MBPC_CARGAS.SP_AMARRAR` | Amarra una barcaza. |
| `PKG_MBPC_CARGAS.SP_FONDEAR` | Fondea una barcaza. |
| `PKG_MBPC_CARGAS.SP_CARGAR` | Registra carga de toneladas. |
| `PKG_MBPC_CARGAS.SP_DESCARGAR` | Registra descarga. |
| `PKG_MBPC_CARGAS.SP_AGREGAR_CARGA` | Agrega nueva barcaza/bodega al manifiesto. |
| `PKG_MBPC_CARGAS.SP_MODIFICAR_CARGA` | Modifica datos de una carga. |
| `PKG_MBPC_CARGAS.SP_ELIMINAR_CARGA` | Elimina carga del manifiesto. |
| `mbpc.adjuntar_barcazas` | SP para adjuntar barcazas a un convoy. |
| `mbpc.separar_convoy` | SP para separar barcazas del convoy. |
| `mbpc.traer_cargas` | SP para obtener cargas de una etapa (por EtapaId). |
| `BUQUES_NEW` | Padrón de buques y barcazas. Usado por autocomplete. |
| `TBL_TIPO_CARGA` | Maestro de tipos de carga/mercadería. Incluye flag peligrosidad IMO. |
| `TBL_AGRUPACION_CARGA` | Agrupación de tipos de carga (relacionada con TBL_TIPO_CARGA). |
| `TBL_ETAPA` | Etapas de viaje en Oracle. Usado para resolver EtapaId en el fallback Oracle de cargas. |

---

## 6. Glosario de Términos de Dominio

| Término | Descripción |
|---|---|
| **Viaje** | Movimiento de un buque desde un origen a un destino. Registrado en Oracle (TravelId) y replicado en MongoDB. |
| **Barcaza** | Embarcación sin propulsión propia, remolcada por el buque tractor. Transporta la carga. |
| **Bodega** | Carga almacenada en la bodega del propio buque tractor (no en barcaza). Se identifica con BarcazaId = 0. |
| **Remolcador/Tractor** | Buque principal que remolca el convoy de barcazas. |
| **Convoy** | Conjunto formado por el remolcador y sus barcazas asociadas en una etapa del viaje. |
| **Etapa** | Segmento del viaje con una composición específica de barcazas (el convoy puede cambiar entre etapas). |
| **Costera** | Jurisdicción geográfica/administrativa del sistema MBPC. Define el scope de datos de cada usuario. |
| **AIS** | Automatic Identification System. Sistema de transponders de posicionamiento marítimo. |
| **MMSI** | Maritime Mobile Service Identity. Identificador único del transponder AIS. |
| **IMO** | International Maritime Organization. Número de identificación internacional del buque. |
| **TravelId** | ID del viaje en el sistema Oracle legacy. Es el puente entre MongoDB y Oracle. |
| **Declaración Malvinas** | Declaración obligatoria regulatoria sobre si el buque va a / viene de las Islas Malvinas. Tiene 20 códigos posibles (letras A-Z). |
| **Km Par** | Kilómetro par del río/canal. Sistema de georreferenciación fluvial usado en la hidrovía Argentina. |
| **ZOE** | Zona de Operación Especial. Campo opcional del viaje. |
| **CPER** | Autoridad regulatoria que emite autorizaciones para navegar a/desde Malvinas. |
| **Hidratación** | Proceso por el cual un documento faltante en MongoDB se reconstruye desde Oracle para evitar pérdida de datos. |
| **Scoping** | Restricción de operaciones CRUD a un viaje específico (usando ViajeId) para evitar modificaciones accidentales en otros viajes. |
