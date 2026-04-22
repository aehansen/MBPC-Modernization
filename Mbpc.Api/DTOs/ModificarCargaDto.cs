using System.ComponentModel.DataAnnotations;

namespace Mbpc.Api.DTOs
{
    public class ModificarCargaDto
    {
        [Required(ErrorMessage = "El ID del viaje es requerido para el scoping.")]
        public string ViajeId { get; set; } = string.Empty;

        [Required(ErrorMessage = "El ID de la barcaza es requerido.")]
        [Range(0, long.MaxValue, ErrorMessage = "El BarcazaId debe ser un entero positivo válido.")]
        public long BarcazaId { get; set; }

        [Required(ErrorMessage = "El tipo de carga es requerido.")]
        public string Tipo { get; set; } = string.Empty;

        [Required(ErrorMessage = "El tonelaje es requerido.")]
        [Range(0.01, double.MaxValue, ErrorMessage = "El tonelaje debe ser un valor positivo.")]
        public double Tonelaje { get; set; }

        [Required(ErrorMessage = "La mercadería/naturaleza es requerida.")]
        [Range(1, int.MaxValue, ErrorMessage = "Debe seleccionar una mercadería válida.")]
        public int MercaderiaId { get; set; }
    }
}
