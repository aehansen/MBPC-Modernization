// Mbpc.Api/DTOs/DesembarcarPersonalDto.cs
// Hito 9.0 — Personal Externo: Inspector / Práctico
using System.ComponentModel.DataAnnotations;

namespace Mbpc.Api.DTOs
{
    /// <summary>
    /// DTO de comando para registrar el desembarque de personal externo (Inspector o Práctico).
    /// El DNI viaja en la ruta de la URL. Este DTO solo lleva el tipo y la fecha de desembarque.
    /// </summary>
    public class DesembarcarPersonalDto
    {
        /// <summary>
        /// Tipo de personal. Valores válidos: "Inspector", "Practico".
        /// Necesario para identificar en cuál array del documento aplicar el update posicional.
        /// </summary>
        [Required(ErrorMessage = "El tipo de personal es requerido.")]
        [RegularExpression("^(Inspector|Practico)$",
            ErrorMessage = "TipoPersonal debe ser 'Inspector' o 'Practico'.")]
        public string TipoPersonal { get; set; } = string.Empty;

        /// <summary>
        /// Fecha y hora de desembarque (UTC). Si no se provee, se usa el momento actual del servidor.
        /// No puede ser futura (validada en el Service).
        /// </summary>
        public DateTime FechaDesembarque { get; set; } = DateTime.UtcNow;
    }
}
