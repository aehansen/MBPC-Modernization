using System;
using System.ComponentModel.DataAnnotations;

namespace Mbpc.Api.DTOs
{
    public class CrearInspeccionDto
    {
        [Required(ErrorMessage = "El ID del viaje es requerido.")]
        public string ViajeId { get; set; } = string.Empty;

        public int BuqueId { get; set; }

        [Required(ErrorMessage = "La fecha de inspección es requerida.")]
        public DateTime FechaInspeccion { get; set; }

        [Required(ErrorMessage = "El tipo de inspección es requerido.")]
        [StringLength(100, ErrorMessage = "El tipo de inspección no puede superar los 100 caracteres.")]
        public string TipoInspeccion { get; set; } = string.Empty;

        [Required(ErrorMessage = "El resultado es requerido.")]
        [StringLength(50, ErrorMessage = "El resultado no puede superar los 50 caracteres.")]
        public string Resultado { get; set; } = string.Empty;

        [StringLength(500, ErrorMessage = "Las observaciones no pueden superar los 500 caracteres.")]
        public string Observaciones { get; set; } = string.Empty;

        [Required(ErrorMessage = "Los datos del inspector son requeridos.")]
        [StringLength(200, ErrorMessage = "Los datos del inspector no pueden superar los 200 caracteres.")]
        public string InspectorDatos { get; set; } = string.Empty;

        [Required(ErrorMessage = "El lugar de la inspección es requerido.")]
        [StringLength(200, ErrorMessage = "El lugar de la inspección no puede superar los 200 caracteres.")]
        public string LugarInspeccion { get; set; } = string.Empty;

        public int CosteraId { get; set; }
    }
}
