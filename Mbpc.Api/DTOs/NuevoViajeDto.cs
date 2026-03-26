using System.ComponentModel.DataAnnotations;

namespace Mbpc.Api.DTOs
{
    /// <summary>
    /// Enum que mapea las opciones de la Declaración Jurada de Malvinas
    /// según el sistema legacy de MBPC. El valor de la letra corresponde
    /// al código de una sola letra que usa el SP subyacente.
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
    /// Expande los campos requeridos por el SP PKG_MBPC_VIAJES.SP_CREAR_VIAJE
    /// con los campos adicionales del formulario de la nueva UI.
    /// </summary>
    public class NuevoViajeDto
    {
        // ----------------------------------------------------------------
        // CAMPOS ORIGINALES (requeridos por el SP)
        // ----------------------------------------------------------------

        [Required(ErrorMessage = "El nombre del buque es requerido.")]
        [StringLength(100, MinimumLength = 2, ErrorMessage = "El nombre del buque debe tener entre 2 y 100 caracteres.")]
        public string NombreBuque { get; set; } = null!;

        [Required(ErrorMessage = "El puerto de origen es requerido.")]
        [StringLength(100, MinimumLength = 2, ErrorMessage = "El origen debe tener entre 2 y 100 caracteres.")]
        public string Origen { get; set; } = null!;

        [Required(ErrorMessage = "El puerto de destino es requerido.")]
        [StringLength(100, MinimumLength = 2, ErrorMessage = "El destino debe tener entre 2 y 100 caracteres.")]
        public string Destino { get; set; } = null!;

        // ----------------------------------------------------------------
        // CAMPOS NUEVOS DE LA VENTANA "NUEVO VIAJE"
        // ----------------------------------------------------------------

        /// <summary>
        /// Muelle de salida inicial del buque. Es opcional: el buque puede
        /// iniciar el viaje desde zona de fondeo sin muelle asignado.
        /// </summary>
        [StringLength(150, ErrorMessage = "El muelle de salida no puede superar los 150 caracteres.")]
        public string? MuelleSalida { get; set; }

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
        /// Debe ser posterior a FechaPartida.
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
        /// Mapea al enum DeclaracionMalvinasEnum. Valor requerido para todo viaje.
        /// </summary>
        [Required(ErrorMessage = "La declaración de Malvinas es requerida.")]
        [EnumDataType(typeof(DeclaracionMalvinasEnum), ErrorMessage = "El valor de Malvinas no corresponde a una opción válida del sistema.")]
        public DeclaracionMalvinasEnum DeclaracionMalvinas { get; set; }
    }
}
