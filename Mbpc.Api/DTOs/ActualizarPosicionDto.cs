using System;
using System.ComponentModel.DataAnnotations;

namespace Mbpc.Api.DTOs
{
    /// <summary>
    /// DTO para actualizar la posición geográfica de un buque en tránsito.
    /// Todos los campos son obligatorios; la validación cinemática (velocidad > 60 kn)
    /// se ejecuta en el servicio comparando contra la posición anterior en MongoDB.
    /// </summary>
    public class ActualizarPosicionDto
    {
        /// <summary>Latitud decimal WGS-84. Rango válido: [-90, 90].</summary>
        [Required(ErrorMessage = "La latitud es obligatoria.")]
        [Range(-90.0, 90.0, ErrorMessage = "La latitud debe estar entre -90 y 90.")]
        public double Latitud { get; set; }

        /// <summary>Longitud decimal WGS-84. Rango válido: [-180, 180].</summary>
        [Required(ErrorMessage = "La longitud es obligatoria.")]
        [Range(-180.0, 180.0, ErrorMessage = "La longitud debe estar entre -180 y 180.")]
        public double Longitud { get; set; }

        /// <summary>
        /// Fecha/hora UTC del reporte AIS. No puede ser futura (tolerancia de 5 min).
        /// La validación temporal se aplica en el servicio para evitar registros con
        /// timestamps incorrectos provenientes de transponders defectuosos.
        /// </summary>
        [Required(ErrorMessage = "La fecha de reporte es obligatoria.")]
        public DateTime FechaReporte { get; set; }
    }
}