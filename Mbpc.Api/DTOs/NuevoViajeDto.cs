using System.ComponentModel.DataAnnotations;

namespace Mbpc.Api.DTOs
{
    /// <summary>
    /// Enum que mapea las opciones de la Declaración Jurada de Malvinas
    /// según el sistema legacy de MBPC.
    ///
    /// CONVENCIÓN DE NOMBRES (obligatoria — no romper):
    ///   El nombre de cada valor termina siempre con "_[LETRA]" donde LETRA
    ///   es el código de una sola letra que usa el SP subyacente.
    ///   El helper MapDeclaracionMalvinas() en ViajeManagerService extrae
    ///   ese último segmento con Split('_').Last() para enviarlo al SP.
    ///   Si se agrega un nuevo valor, DEBE seguir esta convención.
    /// </summary>
    public enum DeclaracionMalvinasEnum
    {
        /// <summary>(L) No viene de Malvinas</summary>
        NoVieneDeMalvinas_L,

        /// <summary>(M) Viene de Malvinas: Autorizado por la CPER</summary>
        VieneDeMalvinas_AutorizadoCPER_M,

        /// <summary>(W) Viene de Malvinas: No autorizado - Se labró infracción - Va al extranjero</summary>
        VieneDeMalvinas_NoAutorizado_Infraccion_Extranjero_W,

        /// <summary>(Y) Viene de Malvinas: No solicitó autorización (Amarra en el país)</summary>
        VieneDeMalvinas_NoSolicitoAutorizacion_Amarra_Y,

        /// <summary>(V) Viene de Malvinas: Solicitó autorización (Amarra en el país)</summary>
        VieneDeMalvinas_SolicitoAutorizacion_Amarra_V,

        /// <summary>(D) No va a Malvinas: Exceptuado, Militar o GC - Cualquier bandera</summary>
        NoVaAMalvinas_Exceptuado_MilitarOGC_D,

        /// <summary>(F) No va a Malvinas: Exceptuado, no realiza navegación marítima</summary>
        NoVaAMalvinas_Exceptuado_NoNavegacionMaritima_F,

        /// <summary>(B) No va a Malvinas</summary>
        NoVaAMalvinas_B,

        /// <summary>(G) No va a Malvinas: Exceptuado, giro interior puerto - misma jurisdicción</summary>
        NoVaAMalvinas_Exceptuado_GiroInteriorPuerto_G,

        /// <summary>(E) No va a Malvinas: Exceptuado, navegación Rada-Ría o Costera</summary>
        NoVaAMalvinas_Exceptuado_NavegacionRadaRiaCostera_E,

        /// <summary>(X) No va a Malvinas: Exceptuado, por otros motivos</summary>
        NoVaAMalvinas_Exceptuado_OtrosMotivos_X,

        /// <summary>(N) No va a Malvinas: No presentó Declaración Jurada</summary>
        NoVaAMalvinas_NoPresentoDeclaracion_N,

        /// <summary>(J) No va a Malvinas: Presentó Declaración Jurada</summary>
        NoVaAMalvinas_PresentoDeclaracion_J,

        /// <summary>(K) No va a Malvinas: Reinicia navegación - Presentó Declaración Jurada</summary>
        NoVaAMalvinas_ReiniciaNavegacion_PresentoDeclaracion_K,

        /// <summary>(Q) Va a Malvinas: Exceptuado, Militar o GC - Cualquier bandera</summary>
        VaAMalvinas_Exceptuado_MilitarOGC_Q,

        /// <summary>(A) Va a Malvinas: Autorizado por la CPER</summary>
        VaAMalvinas_AutorizadoCPER_A,

        /// <summary>(R) Va a Malvinas: Autorizado por la CPER - Reinicia navegación</summary>
        VaAMalvinas_AutorizadoCPER_ReiniciaNavegacion_R,

        /// <summary>(Z) Va a Malvinas: No autorizado por la CPER</summary>
        VaAMalvinas_NoAutorizadoCPER_Z,

        /// <summary>(P) Va a Malvinas: No autorizado por la CPER - Se ordenó fondeo</summary>
        VaAMalvinas_NoAutorizadoCPER_Fondeo_P
    }

    /// <summary>
    /// DTO para el inicio de un nuevo viaje.
    ///
    /// FLUJO DE VIDA DE ESTE OBJETO:
    ///   1. El frontend serializa el formulario de "Nuevo Viaje" a este DTO.
    ///   2. [ApiController] + ModelState validan las DataAnnotations automáticamente.
    ///   3. El Controller inyecta CosteraId desde el Claim del JWT (es el único punto
    ///      donde esto ocurre; el Service NO debe leer el Claim para este DTO).
    ///   4. El Service lo consume para escribir en Oracle (SP) y en ambas colecciones Mongo.
    ///
    /// CAMPOS NO EXPUESTOS AL CLIENTE:
    ///   CosteraId: se inyecta server-side desde el JWT; nunca debe venir del body del cliente.
    ///              Si el cliente lo envía en el body, el Controller lo sobreescribe con el
    ///              valor del Claim, garantizando que un operador nunca pueda crear un viaje
    ///              en una costera que no le pertenece.
    ///
    /// MIGRACIÓN MDM (Cimientos):
    ///   NombreBuque (string libre) fue reemplazado por BuqueId (long).
    ///   El padrón de buques vive en BUQUES_NEW; el frontend debe resolver el ID
    ///   previamente a través de IBuqueService.BuscarBuquesDisponiblesAsync().
    ///
    /// HITO 5.9:
    ///   Se reincorpora NombreBuque como campo complementario (opcional).
    ///   El backend lo persiste en MongoDB junto al detalle del viaje.
    ///   El valor lo resuelve el frontend desde el estado del Autocomplete (buqueSearchTerm).
    /// </summary>
    public class NuevoViajeDto
    {
        // ── MULTITENANT GEOGRÁFICO (inyectado por el Controller, no por el cliente) ──
        //
        // Se inicializa como string.Empty porque el Controller lo sobreescribe SIEMPRE
        // antes de pasar el DTO al Service. El Service falla explícitamente si recibe
        // un valor no parseable a int (ver IniciarViajeAsync).
        public string CosteraId { get; set; } = string.Empty;

        // ── CAMPOS CORE (requeridos por el SP PKG_MBPC_VIAJES.SP_CREAR_VIAJE) ──

        /// <summary>
        /// ID del buque en el padrón BUQUES_NEW del sistema legacy.
        /// Reemplaza el campo de texto libre NombreBuque.
        /// El frontend resuelve este valor mediante el autocomplete de IBuqueService.
        /// </summary>
        [Required(ErrorMessage = "El ID del buque es requerido.")]
        [Range(1, long.MaxValue, ErrorMessage = "El BuqueId debe ser un entero positivo válido del padrón BUQUES_NEW.")]
        public long BuqueId { get; set; }

        /// <summary>
        /// Nombre real del buque tal como fue seleccionado en el Autocomplete del frontend.
        /// Opcional: se persiste en MongoDB (ViajeDetalleMongo) para facilitar la lectura
        /// humana del documento sin necesidad de resolver el BuqueId contra BUQUES_NEW.
        /// El SP de Oracle no utiliza este campo; es exclusivo de la capa Mongo.
        /// </summary>
        public string? NombreBuque { get; set; }

        [Required(ErrorMessage = "El puerto de origen es requerido.")]
        [StringLength(100, MinimumLength = 2, ErrorMessage = "El origen debe tener entre 2 y 100 caracteres.")]
        public string Origen { get; set; } = null!;

        [Required(ErrorMessage = "El puerto de destino es requerido.")]
        [StringLength(100, MinimumLength = 2, ErrorMessage = "El destino debe tener entre 2 y 100 caracteres.")]
        public string Destino { get; set; } = null!;

        // ── CAMPOS ENRIQUECIDOS DEL FORMULARIO "NUEVO VIAJE" ─────────────────

        /// <summary>
        /// Muelle de salida inicial del buque. Opcional: el buque puede
        /// iniciar el viaje desde zona de fondeo sin muelle asignado.
        /// </summary>
        [StringLength(150, ErrorMessage = "El muelle de salida no puede superar los 150 caracteres.")]
        public string? MuelleSalida { get; set; }

        /// <summary>
        /// Agencia Marítima responsable del buque.
        /// Opcional.
        /// </summary>
        public string? AgenciaMaritima { get; set; }

        /// <summary>
        /// Motivo del viaje (ej: Carga Comercial, Reparaciones).
        /// Opcional.
        /// </summary>
        public string? MotivoViaje { get; set; }

        /// <summary>
        /// Próximo punto de control o estación de inspección en la ruta.
        /// Corresponde a los puntos de control del sistema legacy (ej: AMARR ELDO, etc.).
        /// </summary>
        [Required(ErrorMessage = "El próximo punto de control es requerido.")]
        [StringLength(200, MinimumLength = 2, ErrorMessage = "El punto de control debe tener entre 2 y 200 caracteres.")]
        public string ProximoPuntoControl { get; set; } = null!;

        /// <summary>
        /// Fecha y hora de partida del buque desde el origen.
        /// </summary>
        [Required(ErrorMessage = "La fecha de partida es requerida.")]
        public DateTime FechaPartida { get; set; }

        /// <summary>
        /// Estimated Time of Arrival: fecha y hora estimada de llegada al destino.
        /// Debe ser posterior a FechaPartida. La validación temporal se realiza en el
        /// Service (ValidarCoherenciaFechas) para mantener el Controller libre de lógica.
        /// </summary>
        [Required(ErrorMessage = "La ETA (Tiempo Estimado de Arribo) es requerida.")]
        public DateTime ETA { get; set; }

        /// <summary>
        /// Zona de Operación Especial (ZOE) asignada al viaje, si aplica.
        /// Referencia a zonas geográficas con restricciones del sistema legacy.
        /// </summary>
        [StringLength(100, ErrorMessage = "La ZOE no puede superar los 100 caracteres.")]
        public string? ZOE { get; set; }

        /// <summary>
        /// Posición geográfica inicial del buque al momento del registro del viaje.
        /// Formato libre del sistema legacy (ej: "34°36'S 058°22'W" o descripción de zona).
        /// Se almacena como texto en el detalle operativo (ViajeDetalleMongo).
        /// Los valores numéricos Latitude/Longitude en ViajePosicionMongo se inicializan en 0
        /// y son actualizados por el feed AIS externo.
        /// </summary>
        [StringLength(200, ErrorMessage = "La posición no puede superar los 200 caracteres.")]
        public string? Posicion { get; set; }

        /// <summary>
        /// Kilómetro par del Río o Canal correspondiente a la posición del buque.
        /// Usado para georreferenciación en vías fluviales (ej: Río Paraná, Canal Mitre).
        /// </summary>
        [Range(0, 9999.9, ErrorMessage = "El km par debe ser un valor positivo menor a 9999.9.")]
        public decimal? RioCanalKmPar { get; set; }

        /// <summary>
        /// Código de la Declaración Jurada de Malvinas según el sistema legacy.
        /// El valor se envía al SP como una letra de código extraída del nombre del enum
        /// mediante el helper MapDeclaracionMalvinas() en ViajeManagerService.
        /// </summary>
        [Required(ErrorMessage = "La declaración de Malvinas es requerida.")]
        [EnumDataType(typeof(DeclaracionMalvinasEnum), ErrorMessage = "El valor de Malvinas no corresponde a una opción válida del sistema.")]
        public DeclaracionMalvinasEnum DeclaracionMalvinas { get; set; }
    }
}
