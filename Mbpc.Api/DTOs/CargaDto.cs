namespace Mbpc.Api.DTOs
{
    public class CargaDto
    {
        // El "= null!;" apaga las advertencias amarillas de .NET 8
        public string Id { get; set; } = null!;
        public string ViajeId { get; set; } = null!;
        public string DescripcionLista { get; set; } = null!;
        public string NivelRiesgo { get; set; } = null!;

        // Campo para la sincronización CQRS
        public string? MuelleActual { get; set; }

        public double Tonelaje { get; set; }

        // ── Hito 5.7: diferenciación explícita de Bodega vs Barcaza ──────────
        // Calculado en la capa de servicio; nunca persiste en base de datos.
        public string TipoUnidad { get; set; } = string.Empty;

        // ── Hito 5.9: Propiedades para edición de carga ─────────────────────
        public int? MercaderiaId { get; set; }
        public string? MercaderiaNombre { get; set; }
    }
}
