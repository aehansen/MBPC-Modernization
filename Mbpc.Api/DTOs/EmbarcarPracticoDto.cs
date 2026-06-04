using System.ComponentModel.DataAnnotations;

namespace Mbpc.Api.DTOs
{
    public class EmbarcarPracticoDto
    {
        [Required(ErrorMessage = "El DNI es requerido.")]
        [StringLength(20, MinimumLength = 6, ErrorMessage = "El DNI debe tener entre 6 y 20 caracteres.")]
        public string Dni { get; set; } = string.Empty;

        [Required(ErrorMessage = "El nombre y apellido son requeridos.")]
        [StringLength(200, MinimumLength = 3, ErrorMessage = "El nombre debe tener entre 3 y 200 caracteres.")]
        public string NombreApellido { get; set; } = string.Empty;

        public DateTime FechaEmbarque { get; set; } = DateTime.UtcNow;
    }
}
