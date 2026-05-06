// Mbpc.Api/DTOs/NuevoViajeDto.cs
using System.ComponentModel.DataAnnotations;

namespace Mbpc.Api.DTOs
{
    public enum DeclaracionMalvinasEnum
    {
        NoVieneDeMalvinas_L,
        VieneDeMalvinas_AutorizadoCPER_M,
        VieneDeMalvinas_NoAutorizado_Infraccion_Extranjero_W,
        VieneDeMalvinas_NoSolicitoAutorizacion_Amarra_Y,
        VieneDeMalvinas_SolicitoAutorizacion_Amarra_V,
        NoVaAMalvinas_Exceptuado_MilitarOGC_D,
        NoVaAMalvinas_Exceptuado_NoNavegacionMaritima_F,
        NoVaAMalvinas_B,
        NoVaAMalvinas_Exceptuado_GiroInteriorPuerto_G,
        NoVaAMalvinas_Exceptuado_NavegacionRadaRiaCostera_E,
        NoVaAMalvinas_Exceptuado_OtrosMotivos_X,
        NoVaAMalvinas_NoPresentoDeclaracion_N,
        NoVaAMalvinas_PresentoDeclaracion_J,
        NoVaAMalvinas_ReiniciaNavegacion_PresentoDeclaracion_K,
        VaAMalvinas_Exceptuado_MilitarOGC_Q,
        VaAMalvinas_AutorizadoCPER_A,
        VaAMalvinas_AutorizadoCPER_ReiniciaNavegacion_R,
        VaAMalvinas_NoAutorizadoCPER_Z,
        VaAMalvinas_NoAutorizadoCPER_Fondeo_P
    }

    public class NuevoViajeDto
    {
        public string CosteraId { get; set; } = string.Empty;

        [Required(ErrorMessage = "El ID del buque es requerido.")]
        [Range(1, long.MaxValue, ErrorMessage = "El BuqueId debe ser un entero positivo válido del padrón BUQUES_NEW.")]
        public long BuqueId { get; set; }

        public string? NombreBuque { get; set; }

        [Required(ErrorMessage = "El puerto de origen es requerido.")]
        [StringLength(100, MinimumLength = 2, ErrorMessage = "El origen debe tener entre 2 y 100 caracteres.")]
        public string Origen { get; set; } = null!;

        [Required(ErrorMessage = "El puerto de destino es requerido.")]
        [StringLength(100, MinimumLength = 2, ErrorMessage = "El destino debe tener entre 2 y 100 caracteres.")]
        public string Destino { get; set; } = null!;

        [StringLength(150, ErrorMessage = "El muelle de salida no puede superar los 150 caracteres.")]
        public string? MuelleSalida { get; set; }

        public string? AgenciaMaritima { get; set; }

        public string? MotivoViaje { get; set; }

        [Required(ErrorMessage = "El próximo punto de control es requerido.")]
        [StringLength(200, MinimumLength = 2, ErrorMessage = "El punto de control debe tener entre 2 y 200 caracteres.")]
        public string ProximoPuntoControl { get; set; } = null!;

        [Required(ErrorMessage = "La fecha de partida es requerida.")]
        public DateTime FechaPartida { get; set; }

        [Required(ErrorMessage = "La ETA (Tiempo Estimado de Arribo) es requerida.")]
        public DateTime ETA { get; set; }

        [StringLength(100, ErrorMessage = "La ZOE no puede superar los 100 caracteres.")]
        public string? ZOE { get; set; }

        /// <summary>
        /// Latitud inicial del buque al momento del registro.
        /// Valor numérico decimal para garantizar compatibilidad con MongoDB GeoJSON
        /// y cálculos de distancia con la fórmula de Haversine.
        /// </summary>
        [Range(-90.0, 90.0, ErrorMessage = "La latitud debe estar entre -90 y 90 grados.")]
        public decimal? Latitud { get; set; }

        /// <summary>
        /// Longitud inicial del buque al momento del registro.
        /// Valor numérico decimal para garantizar compatibilidad con MongoDB GeoJSON
        /// y cálculos de distancia con la fórmula de Haversine.
        /// </summary>
        [Range(-180.0, 180.0, ErrorMessage = "La longitud debe estar entre -180 y 180 grados.")]
        public decimal? Longitud { get; set; }

        [Range(0, 9999.9, ErrorMessage = "El km par debe ser un valor positivo menor a 9999.9.")]
        public decimal? RioCanalKmPar { get; set; }

        [Required(ErrorMessage = "La declaración de Malvinas es requerida.")]
        [EnumDataType(typeof(DeclaracionMalvinasEnum), ErrorMessage = "El valor de Malvinas no corresponde a una opción válida del sistema.")]
        public DeclaracionMalvinasEnum DeclaracionMalvinas { get; set; }
    }
}