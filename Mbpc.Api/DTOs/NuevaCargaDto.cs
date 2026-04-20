using System.ComponentModel.DataAnnotations;

namespace Mbpc.Api.DTOs
{
    /// <summary>
    /// DTO para agregar una nueva carga (barcaza) a un viaje en curso.
    ///
    /// MIGRACIÓN MDM (Cimientos):
    ///   El campo de texto libre <c>Nombre</c> fue reemplazado por <c>BarcazaId</c> (long).
    ///   El padrón de barcazas vive en el sistema legacy Oracle; el frontend debe resolver
    ///   el ID previamente a través de IBuqueService.BuscarBarcazasDisponiblesAsync().
    /// </summary>
    public class NuevaCargaDto
    {
        /// <summary>
        /// ID de la barcaza en el padrón del sistema legacy Oracle.
        /// Reemplaza el campo de texto libre Nombre.
        /// El frontend resuelve este valor mediante el autocomplete de IBuqueService.
        /// </summary>
        [Required(ErrorMessage = "El ID de la barcaza es requerido.")]
        [Range(0, long.MaxValue, ErrorMessage = "El BarcazaId debe ser un entero positivo válido del padrón de barcazas.")]
        public long BarcazaId { get; set; }

        /// <summary>
        /// Tipo de carga transportada (ej: "Barcaza" o "Bodega").
        /// </summary>
        [Required(ErrorMessage = "El tipo de carga es requerido.")]
        public string Tipo { get; set; } = string.Empty;

        /// <summary>
        /// Tonelaje de la carga en toneladas métricas.
        /// </summary>
        [Required(ErrorMessage = "El tonelaje es requerido.")]
        [Range(0.01, double.MaxValue, ErrorMessage = "El tonelaje debe ser un valor positivo.")]
        public double Tonelaje { get; set; }
    }
}
