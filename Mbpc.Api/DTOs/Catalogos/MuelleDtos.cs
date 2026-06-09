using System.ComponentModel.DataAnnotations;

namespace Mbpc.Api.DTOs.Catalogos
{
    // ── MuelleAltaDto ─────────────────────────────────────────────────────────
    // Payload de creación de muelle.

    public class MuelleAltaDto
    {
        /// <summary>Nombre descriptivo del muelle (ej: Terminal Zárate - Muelle 1). Obligatorio.</summary>
        [Required(ErrorMessage = "El nombre del muelle es obligatorio.")]
        [MaxLength(300, ErrorMessage = "El nombre no puede superar los 300 caracteres.")]
        public string Nombre { get; set; } = null!;

        /// <summary>
        /// Código alfanumérico corto del muelle (ej: TZA-01).
        /// Debe ser único en el sistema.
        /// </summary>
        [MaxLength(20, ErrorMessage = "El código no puede superar los 20 caracteres.")]
        public string? Codigo { get; set; }

        /// <summary>
        /// Zona geográfica o jurisdicción del muelle (ej: Zárate, Rosario, San Lorenzo).
        /// </summary>
        [MaxLength(100)]
        public string? Zona { get; set; }

        /// <summary>
        /// Kilómetro par del río/canal donde se ubica el muelle (sistema fluvial hidrovía).
        /// </summary>
        [Range(0.0, 3_500.0, ErrorMessage = "El km par debe estar entre 0 y 3.500 km.")]
        public double? KmPar { get; set; }

        /// <summary>Profundidad máxima en metros para operación segura.</summary>
        [Range(0.0, 30.0, ErrorMessage = "La profundidad debe estar entre 0 y 30 metros.")]
        public double? ProfundidadM { get; set; }

        /// <summary>Estado inicial del registro: Activo / Inactivo. Por defecto: Activo.</summary>
        public string Estado { get; set; } = "Activo";
    }

    // ── MuelleEditDto ─────────────────────────────────────────────────────────

    public class MuelleEditDto
    {
        /// <summary>Identificador numérico del muelle. Obligatorio.</summary>
        [Required(ErrorMessage = "El Id del muelle es obligatorio para la edición.")]
        [Range(1, int.MaxValue, ErrorMessage = "El Id debe ser un entero positivo.")]
        public int Id { get; set; }

        /// <summary>Nombre actualizado. Obligatorio.</summary>
        [Required(ErrorMessage = "El nombre del muelle es obligatorio.")]
        [MaxLength(300)]
        public string Nombre { get; set; } = null!;

        /// <summary>Código alfanumérico actualizado. Único si informado.</summary>
        [MaxLength(20)]
        public string? Codigo { get; set; }

        /// <summary>Zona geográfica actualizada.</summary>
        [MaxLength(100)]
        public string? Zona { get; set; }

        /// <summary>Km par actualizado.</summary>
        [Range(0.0, 3_500.0)]
        public double? KmPar { get; set; }

        /// <summary>Profundidad máxima actualizada en metros.</summary>
        [Range(0.0, 30.0)]
        public double? ProfundidadM { get; set; }

        /// <summary>Estado del registro (Activo / Inactivo / Cerrado).</summary>
        [MaxLength(30)]
        public string? Estado { get; set; }
    }

    // ── MuelleDetalleDto ──────────────────────────────────────────────────────
    // DTO de respuesta completa para GET /api/catalogos/muelles/{id}.

    public class MuelleDetalleDto
    {
        public int     Id           { get; set; }
        public string? Nombre       { get; set; }
        public string? Codigo       { get; set; }
        public string? Zona         { get; set; }
        public double? KmPar        { get; set; }
        public double? ProfundidadM { get; set; }
        public string? Estado       { get; set; }
        public int?    CosteraId    { get; set; }
    }
}
