using System.ComponentModel.DataAnnotations;

namespace Mbpc.Api.DTOs
{
    /// <summary>
    /// DTO para la reubicación manual de un buque en el mapa por parte de un operador.
    /// Se omiten las validaciones de velocidad cinemática puesto que es una reubicación manual.
    /// </summary>
    public class ReubicarBuqueDto
    {
        /// <summary>Latitud decimal WGS-84. Rango válido: [-90, 90].</summary>
        [Required(ErrorMessage = "La latitud es obligatoria.")]
        [Range(-90.0, 90.0, ErrorMessage = "La latitud debe estar entre -90 y 90.")]
        public double Latitud { get; set; }

        /// <summary>Longitud decimal WGS-84. Rango válido: [-180, 180].</summary>
        [Required(ErrorMessage = "La longitud es obligatoria.")]
        [Range(-180.0, 180.0, ErrorMessage = "La longitud debe estar entre -180 y 180.")]
        public double Longitud { get; set; }
    }
}
