using System.ComponentModel.DataAnnotations;

namespace Mbpc.Api.DTOs
{
    /// <summary>
    /// DTO para la rectificación histórica de cargas.
    /// </summary>
    public record RectificarCargaDto
    {
        /// <summary>
        /// Tonelaje corregido de la carga.
        /// </summary>
        [Required(ErrorMessage = "El tonelaje corregido es requerido.")]
        [Range(0.0, double.MaxValue, ErrorMessage = "El tonelaje corregido debe ser un valor positivo.")]
        public double Tonelaje { get; init; }

        /// <summary>
        /// Motivo por el cual se realiza la rectificación histórica.
        /// </summary>
        [Required(ErrorMessage = "El motivo de la rectificación es requerido.")]
        [MinLength(3, ErrorMessage = "El motivo debe tener al menos 3 caracteres.")]
        public string Motivo { get; init; } = string.Empty;
    }
}
