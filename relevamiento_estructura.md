# 📋 Relevamiento de Estructura — MBPC (Solo Lectura)
> Fecha: 2026-05-11 | Basado en código fuente actual, sin modificaciones.

---

## 1. Estructura del Frontend — `frontend-mbpc/src/`

```
src/
├── axiosClient.js
├── index.css
├── main.tsx
├── ControlesEstadoBuque.jsx
├── MapaAIS.jsx
│
├── components/
│   ├── BotonesAccionViaje.tsx
│   ├── BotonZarpar.tsx
│   ├── ModalAmarrarBarcaza.tsx
│   ├── Navbar.jsx / Navbar2.jsx
│   │
│   ├── viajes/                         ← MÓDULO PRINCIPAL A REFACTORIZAR
│   │   ├── ModalNuevoViaje.tsx         ← contiene 2x datetime-local (fechaPartida, eta)
│   │   ├── ModalActualizarPosicion.tsx ← contiene 2x datetime-local (fechaPartida, eta)
│   │   ├── ViajesDashboard.tsx
│   │   └── ModalHistorico.tsx
│   │
│   ├── cargas/
│   │   ├── CargasModal.tsx
│   │   ├── CargasTable.jsx
│   │   ├── CargaEditModal.jsx
│   │   ├── CargaDeleteModal.jsx
│   │   └── TipoCargaAutocomplete.jsx
│   │
│   ├── convoy/
│   │   ├── PanelGestionConvoy.tsx
│   │   └── BarcazaAutocomplete.tsx
│   │
│   ├── chat/
│   │   └── ChatFloatingWindow.tsx
│   └── layout/
│       └── MainLayout.tsx
│
├── hooks/
│   ├── useNuevoViaje.ts
│   ├── useViajes.ts / useViajesApi.ts
│   ├── useAccionesViaje.ts
│   ├── useActualizarPosicion.ts
│   ├── useAmarrarBarcaza.ts
│   ├── useBuscarBarcazas.ts
│   ├── useCargasApi.ts
│   ├── useGestionConvoy.ts
│   ├── useZarpar.ts
│   └── ...
│
├── types/
│   ├── viajes.types.ts                 ← NuevoViajeFormValues, NuevoViajeRequest
│   ├── cargas.types.ts
│   ├── convoy.types.ts
│   └── amarrarBarcaza.types.ts
│
├── pages/
│   ├── ViajesPage.tsx
│   └── Login.jsx
│
└── services/
    ├── apiClient.js
    └── viajes.service.ts
```

### 📍 Inputs `datetime-local` relevados

| Archivo | ID del input | Campo RHF | Atributo `min` | Validación RHF |
|---|---|---|---|---|
| `ModalNuevoViaje.tsx` L582 | `fechaPartida` | `fechaPartida` | ❌ removido (Hito 8.0) | `validate`: no puede ser pasado |
| `ModalNuevoViaje.tsx` L603 | `eta` | `eta` | ❌ removido (Hito 8.0) | `validate`: debe ser > fechaPartida |
| `ModalActualizarPosicion.tsx` L571 | `fechaPartida` | `fechaPartida` | ✅ `min={nowMin}` | Solo `required` |
| `ModalActualizarPosicion.tsx` L588 | `eta` | `eta` | ✅ `min={fechaPartidaValue \|\| nowMin}` | `validate`: debe ser > fechaPartida |

> [!WARNING]
> `ModalActualizarPosicion.tsx` **aún usa el atributo `min` nativo** en ambos inputs, que es el bug a corregir (desplegable de horas bloqueado). `ModalNuevoViaje.tsx` ya lo tiene solucionado (Hito 8.0).

---

## 2. Backend — `ViajesController.cs`

**Clase:** `ViajesController : ControllerBase`
**Ruta base:** `api/viajes` | **Auth:** `[Authorize]` en toda la clase

### 2.1 Firmas de Endpoints

```csharp
// ── LECTURA ──────────────────────────────────────────────────────────────────

[HttpGet]
Task<ActionResult<List<ViajeDto>>> GetViajes(
    [FromQuery] string? nombre  = null,
    [FromQuery] int pagina      = 1,
    [FromQuery] int tamanio     = 50)

[HttpGet("{mmsi}")]
Task<ActionResult<ViajePosicionMongo>> GetViajeByMmsi(string mmsi)

[HttpGet("puerto")]
Task<ActionResult<List<BarcoPuertoDto>>> GetBarcosEnPuerto()

[HttpGet("historico")]
Task<ActionResult<List<ViajeHistoricoDto>>> GetHistorico(
    [FromQuery] string?   nombre    = null,
    [FromQuery] string?   omi       = null,
    [FromQuery] string?   matricula = null,
    [FromQuery] string?   origen    = null,
    [FromQuery] string?   destino   = null,
    [FromQuery] DateTime? desde     = null,
    [FromQuery] DateTime? hasta     = null)

[HttpGet("mapa")]
Task<ActionResult<List<MapaViajeDto>>> GetMapaViajes(
    [FromQuery] string? mmsi        = null,
    [FromQuery] string? nombreBuque = null)

// ── ESCRITURA ─────────────────────────────────────────────────────────────────

[HttpPost]
Task<ActionResult> IniciarViaje([FromBody] NuevoViajeDto nuevoViaje)

[HttpPut("{id}/zarpar")]
Task<ActionResult> ZarparViaje(string id)

[HttpPut("{id}/amarrar")]
Task<ActionResult> AmarrarViaje(string id)

[HttpPut("{id}/finalizar")]         // ← ENDPOINT CLAVE
Task<ActionResult> FinalizarViaje(string id)

[HttpPut("{id}/fondear")]
Task<ActionResult> FondearViaje(string id)

[HttpPut("{id}/reanudar")]
Task<ActionResult> ReanudarViaje(string id)

[HttpPut("{id}/posicion")]
Task<ActionResult> ActualizarPosicion(string id, [FromBody] ActualizarPosicionDto dto)
```

> [!NOTE]
> **No existe** ningún endpoint para "personal externo" (Inspectores/Prácticos) en `ViajesController.cs`. El CRUD de `Inspectores` y `Practicos` **aún no está implementado** a nivel de Controller ni Service.

---

## 3. Backend — `IViajeService.cs` (Interface)

```csharp
// ── LECTURAS ──────────────────────────────────────────────────────────────────
Task<List<ViajePosicionMongo>> GetViajesAsync(string? nombre = null, int pagina = 1, int tamanio = 50);
Task<List<ViajeDto>> ObtenerViajesDtoAsync(string? nombre, int pagina, int tamanio);
Task<ViajePosicionMongo?> GetViajeByMmsiAsync(string mmsi);
Task<(ViajeDetalleMongo? Detalle, long TravelId)> GetViajeDetalleByIdAsync(string id, CancellationToken ct = default);
Task<List<BarcoPuertoDto>> GetBarcosEnPuertoAsync();
Task<List<ViajeHistoricoDto>> GetHistoricoAsync(FiltroHistoricoDto filtro);
Task<List<MapaViajeDto>> GetMapaViajesAsync(string? mmsi = null, string? nombreBuque = null);

// ── ESCRITURA ─────────────────────────────────────────────────────────────────
Task<bool> IniciarViajeAsync(NuevoViajeDto nuevoViaje);

// ── MÁQUINA DE ESTADOS ────────────────────────────────────────────────────────
Task<bool> ZarparAsync(string id);
Task<bool> AmarrarViajeAsync(string id);
Task<bool> FinalizarViajeAsync(string id);   // ← bloqueo paranoico
Task<bool> FondearViajeAsync(string id);
Task<bool> ReanudarViajeAsync(string id);

// ── POSICIONAMIENTO AIS ───────────────────────────────────────────────────────
Task<PosicionActualizadaResultDto?> ActualizarPosicionAsync(string id, ActualizarPosicionDto dto);
```

---

## 4. Backend — `ViajeManagerService.cs` — Firmas

### 4.1 Métodos Públicos (implementan `IViajeService`)

```csharp
// Lecturas
public async Task<List<ViajePosicionMongo>> GetViajesAsync(string? nombre = null, int pagina = 1, int tamanio = 50)
public async Task<List<ViajeDto>> ObtenerViajesDtoAsync(string? nombre, int pagina, int tamanio)
public async Task<ViajePosicionMongo?> GetViajeByMmsiAsync(string mmsi)
public async Task<(ViajeDetalleMongo? Detalle, long TravelId)> GetViajeDetalleByIdAsync(string id, CancellationToken ct = default)
public async Task<List<BarcoPuertoDto>> GetBarcosEnPuertoAsync()
public async Task<List<MapaViajeDto>> GetMapaViajesAsync(string? mmsi = null, string? nombreBuque = null)
public async Task<List<ViajeHistoricoDto>> GetHistoricoAsync(FiltroHistoricoDto filtro)

// Escritura
public async Task<bool> IniciarViajeAsync(NuevoViajeDto nuevoViaje)
public async Task<PosicionActualizadaResultDto?> ActualizarPosicionAsync(string id, ActualizarPosicionDto dto)

// Máquina de estados
public async Task<bool> ZarparAsync(string id)
public async Task<bool> AmarrarViajeAsync(string id)
public async Task<bool> FinalizarViajeAsync(string id)     // ← ver 4.2
public async Task<bool> FondearViajeAsync(string id)
public async Task<bool> ReanudarViajeAsync(string id)
```

### 4.2 `FinalizarViajeAsync` — Lógica de Bloqueo (sin cuerpo)

```csharp
public async Task<bool> FinalizarViajeAsync(string id)
// Precondiciones verificadas antes de delegar a CambiarEstadoNavegacionAsync:
//   1. GetViajeDetalleByIdAsync(id) → detalle != null (lanza InvalidOperationException si null)
//   2. NavegationStatusDesc == "Finalizado" → lanza InvalidOperationException (idempotencia)
//   3. tieneBarcazasRaiz    = detalle.Barcazas?.Count > 0
//   4. tieneBarcazasEtapa   = detalle.Etapas?.Any(e => e.Barcazas?.Count > 0)
//   5. inspectoresABordo    = detalle.Inspectores?.Any(i => i.FechaDesembarque is null)
//   6. practicosABordo      = detalle.Practicos?.Any(p => p.FechaDesembarque is null)
//   Si cualquiera de 3-6 es true → lanza InvalidOperationException con motivos concatenados
//   Caso OK → llama CambiarEstadoNavegacionAsync(id, "Finalizado")
```

### 4.3 Métodos Privados

```csharp
private async Task<bool> CambiarEstadoConValidacionAsync(string id, EstadoEtapa estadoDestino)
private async Task<bool> CambiarEstadoNavegacionAsync(string id, string nuevoEstado)
private static FilterDefinition<ViajePosicionMongo> BuildFiltroViaje(string id)
private static FilterDefinition<ViajePosicionMongo> BuildFiltroCostera(int costeraId)
private static FilterDefinition<ViajeDetalleMongo>  BuildFiltroCosteraDetalle(int costeraId)
private static double CalcularHaversineKm(double lat1, double lng1, double lat2, double lng2)
private static double ToRadians(double grados)
private static string MapDeclaracionMalvinas(DeclaracionMalvinasEnum declaracion)
private static List<ViajeHistoricoDto> GetHistoricoMock(FiltroHistoricoDto filtro, int costeraId)
```

### 4.4 Constantes y Campos Estáticos

```csharp
private const double RADIO_TIERRA_KM             = 6371.0;
private const double KM_POR_MILLA_NAUTICA        = 1.852;
private const double MAX_VELOCIDAD_KNOTS         = 60.0;
private const double MIN_SEGUNDOS_ENTRE_REPORTES = 1.0;

private static string CacheKeyBarcosEnPuerto(int costeraId) => $"barcos:en_puerto:{costeraId}";
private static string CacheKeyMapaViajes(int costeraId)     => $"viajes:mapa:{costeraId}";
private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(2);
```

---

## 5. Modelo BSON — `ViajeDetalleMongo.cs`

### 5.1 `ViajeDetalleMongo` (raíz del documento)

| Propiedad C# | BsonElement | Tipo .NET | Notas |
|---|---|---|---|
| `Id` | `_id` | `string` | ObjectId |
| `IdViaje` | `"IdViaje"` | `long?` | TravelId relacional Oracle |
| `VesselName` | `"VesselName"` | `string?` | |
| `Origin` | `"Origin"` | `string?` | |
| `Destination` | `"Destination"` | `string?` | |
| `Etapas` | `"ETAPAS"` | `List<EtapaMongo>?` | Formato moderno |
| `Barcazas` | `"barcazas"` | `List<BarcazaMongo>?` | Fallback legacy (minúsculas) |
| `RemolcadorLegacy` | `"remolcador"` | `RemolcadorMongo?` | Fallback legacy |
| `CosteraIdRaw` | `"CosteraId"` | `BsonValue?` | Tolerante a int/long/string |
| `CosteraId` | `[BsonIgnore]` | `int?` | Getter de negocio |
| `Pbip` | `"pbip"` | `PbipMongo?` | |
| `Inspectores` | `"inspectores"` | `List<InspectorMongo>?` | ← Personal externo |
| `Practicos` | `"practicos"` | `List<PracticoMongo>?` | ← Personal externo |

### 5.2 `InspectorMongo`

```csharp
[BsonElement("documento")]      string  Documento        { get; set; }
[BsonElement("nombreApellido")] string  NombreApellido   { get; set; }
[BsonElement("fechaEmbarque")]  DateTime  FechaEmbarque  { get; set; }
[BsonElement("fechaDesembarque")] DateTime? FechaDesembarque { get; set; }  // null = aún a bordo
```

### 5.3 `PracticoMongo`

```csharp
[BsonElement("documento")]      string  Documento        { get; set; }
[BsonElement("nombreApellido")] string  NombreApellido   { get; set; }
[BsonElement("fechaEmbarque")]  DateTime  FechaEmbarque  { get; set; }
[BsonElement("fechaDesembarque")] DateTime? FechaDesembarque { get; set; }  // null = aún a bordo
```

> [!NOTE]
> `InspectorMongo` y `PracticoMongo` tienen **estructura idéntica**. Son dos colecciones separadas por tipo de rol, no hay discriminador de tipo.

### 5.4 `EtapaMongo`

```csharp
[BsonElement("ETAPA_ID")]     long?              EtapaId     { get; set; }
[BsonElement("FECHA_INICIO")] DateTime?          FechaInicio { get; set; }
[BsonElement("FECHA_FIN")]    DateTime?          FechaFin    { get; set; }
[BsonElement("REMOLCADOR")]   RemolcadorMongo?   Remolcador  { get; set; }
[BsonElement("BARCAZAS")]     List<BarcazaMongo>? Barcazas   { get; set; }
```

### 5.5 `BarcazaMongo`

```csharp
[BsonElement("ID_VIAJE")]      long?   IdViaje      { get; set; }
[BsonElement("BARCAZA")]       string? Nombre       { get; set; }
[BsonElement("BANDERA")]       string? Bandera      { get; set; }
[BsonElement("MATRICULA")]     string? Matricula    { get; set; }
[BsonElement("CARGA")]         string? Carga        { get; set; }
[BsonElement("CANTIDAD")]      double? Cantidad     { get; set; }
[BsonElement("UNIDAD")]        string? Unidad       { get; set; }
[BsonElement("MUELLE_ACTUAL")] string? MuelleActual { get; set; }
[BsonElement("MERCADERIA_ID")] int?    MercaderiaId { get; set; }

// Propiedades de negocio [BsonIgnore] (nunca persistidas):
string  UnidadDescripcion  // switch: "TN"→"Toneladas", "M3"→"Metros Cúbicos", etc.
bool    EsCargaLiquida     // true si Carga contiene "petroleo/combustible/gas"
string  Resumen            // formato: "[MATRICULA] NOMBRE — CANTIDAD UNIDAD"
```

### 5.6 `RemolcadorMongo`

```csharp
[BsonElement("NOMBRE")]    string? Nombre    { get; set; }
[BsonElement("MATRICULA")] string? Matricula { get; set; }
```

### 5.7 `PbipMongo`

```csharp
[BsonElement("nroInmarsat")]     string NroInmarsat     { get; set; }
[BsonElement("arqueoBruto")]     double ArqueoBruto     { get; set; }
[BsonElement("nivelProteccion")] int    NivelProteccion  { get; set; }
```

---

## 6. DTOs de Cargas

### 6.1 `CargaDto` (respuesta de lectura)

```csharp
string  Id               { get; set; }   // ObjectId
string  ViajeId          { get; set; }
string  DescripcionLista { get; set; }
string  NivelRiesgo      { get; set; }
string? MuelleActual     { get; set; }
double  Tonelaje         { get; set; }
string  TipoUnidad       { get; set; }   // calculado en Service, nunca persiste
int?    MercaderiaId     { get; set; }
string? MercaderiaNombre { get; set; }
```

### 6.2 `NuevaCargaDto` (comando de alta)

```csharp
[Required] [Range(0, long.MaxValue)]
long    BarcazaId    { get; set; }   // ID del padrón Oracle

string? BarcazaNombre { get; set; }  // nombre desnormalizado para MongoDB

[Required]
string  Tipo         { get; set; }   // "Barcaza" | "Bodega"

[Required] [Range(0.01, double.MaxValue)]
double  Tonelaje     { get; set; }

[Required] [Range(1, int.MaxValue)]
int     MercaderiaId { get; set; }
```

### 6.3 `ModificarCargaDto` (comando de edición)

```csharp
[Required]
string  ViajeId      { get; set; }   // scoping de seguridad

[Required] [Range(0, long.MaxValue)]
long    BarcazaId    { get; set; }

[Required]
string  Tipo         { get; set; }

[Required] [Range(0.01, double.MaxValue)]
double  Tonelaje     { get; set; }

[Required] [Range(1, int.MaxValue)]
int     MercaderiaId { get; set; }
```

---

## 7. Hallazgos Clave para la Planificación

> [!IMPORTANT]
> **Gap crítico**: Los campos `Inspectores` y `Practicos` **existen en el modelo BSON** `ViajeDetalleMongo` y son evaluados en `FinalizarViajeAsync` como precondición de bloqueo, pero **no existe ningún endpoint HTTP** para gestionarlos (crear, embarcar, desembarcar). El CRUD de personal externo está pendiente de implementar.

> [!WARNING]
> **Bug datetime-local pendiente**: `ModalActualizarPosicion.tsx` (L571, L588) aún usa `min={nowMin}` y `min={fechaPartidaValue || nowMin}` nativos, lo que bloquea el desplegable de horas del browser. La corrección aplicada en `ModalNuevoViaje.tsx` (Hito 8.0) debe replicarse aquí: eliminar `min`, y mover la validación temporal a `react-hook-form validate`.

> [!NOTE]
> **Dualidad de documentos legacy**: `ViajeDetalleMongo` tiene dos rutas de lectura para barcazas: `Etapas[].BARCAZAS` (formato moderno, mayúsculas) y `barcazas` raíz (legacy, minúsculas). Cualquier refactorización debe mantener ambos `BsonElement` para no romper documentos existentes.
