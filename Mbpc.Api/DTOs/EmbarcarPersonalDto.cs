// Mbpc.Api/DTOs/EmbarcarPersonalDto.cs
// Hito 9.0 — Personal Externo: Inspector / Práctico
using System.ComponentModel.DataAnnotations;

namespace Mbpc.Api.DTOs
{
    /// <summary>
    /// DTO de comando para embarcar personal externo (Inspector o Práctico) en un viaje activo.
    /// El backend verifica que el DNI no se encuentre activo en ningún otro viaje antes de insertar.
    /// </summary>
    public class EmbarcarPersonalDto
    {
        /// <summary>
        /// DNI / Documento de identidad del personal. Actúa como clave de unicidad.
        /// </summary>
        [Required(ErrorMessage = "El DNI es requerido.")]
        [StringLength(20, MinimumLength = 6, ErrorMessage = "El DNI debe tener entre 6 y 20 caracteres.")]
        public string Dni { get; set; } = string.Empty;

        /// <summary>
        /// Nombre y apellido completo del personal embarcado.
        /// </summary>
        [Required(ErrorMessage = "El nombre y apellido son requeridos.")]
        [StringLength(200, MinimumLength = 3, ErrorMessage = "El nombre debe tener entre 3 y 200 caracteres.")]
        public string NombreApellido { get; set; } = string.Empty;

        /// <summary>
        /// Tipo de personal. Valores válidos: "Inspector", "Practico".
        /// </summary>
        [Required(ErrorMessage = "El tipo de personal es requerido.")]
        [RegularExpression("^(Inspector|Practico)$",
            ErrorMessage = "TipoPersonal debe ser 'Inspector' o 'Practico'.")]
        public string TipoPersonal { get; set; } = string.Empty;

        /// <summary>
        /// Fecha y hora de embarque (UTC). Si no se provee, se usa el momento actual del servidor.
        /// </summary>
        public DateTime FechaEmbarque { get; set; } = DateTime.UtcNow;
    }
}
