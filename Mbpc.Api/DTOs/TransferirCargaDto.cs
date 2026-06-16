using System.ComponentModel.DataAnnotations;

namespace Mbpc.Api.DTOs
{
    /// <summary>
    /// DTO para transferir (transbordar) carga de un viaje/barcaza de origen a otro de destino.
    /// </summary>
    public record TransferirCargaDto
    {
        /// <summary>
        /// ID del viaje de destino (ObjectId de MongoDB).
        /// </summary>
        [Required(ErrorMessage = "El ID del viaje destino es requerido.")]
        public string DestinoViajeId { get; init; } = string.Empty;

        /// <summary>
        /// ID de la barcaza de destino en el padrón legacy Oracle.
        /// </summary>
        [Required(ErrorMessage = "El ID de la barcaza destino es requerido.")]
        [Range(0, long.MaxValue, ErrorMessage = "El ID de la barcaza destino debe ser un entero positivo válido (0 para Bodegas).")]
        public long DestinoBarcazaId { get; init; }

        /// <summary>
        /// Tonelaje de carga a transferir.
        /// </summary>
        [Required(ErrorMessage = "El tonelaje a transferir es requerido.")]
        [Range(0.01, double.MaxValue, ErrorMessage = "El tonelaje a transferir debe ser mayor a cero.")]
        public double Tonelaje { get; init; }
    }
}
