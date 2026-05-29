using System.ComponentModel.DataAnnotations;

namespace Mbpc.Api.DTOs
{
    public class TransferirJurisdiccionDto
    {
        [Required(ErrorMessage = "La nueva costera es requerida.")]
        [Range(1, int.MaxValue, ErrorMessage = "El ID de la nueva costera debe ser mayor a 0.")]
        public int NuevaCosteraId { get; set; }

        public double? Velocidad { get; set; }

        public double? Rumbo { get; set; }

        public string? MuelleLlegadaBuqueId { get; set; }
    }
}
