using System.ComponentModel.DataAnnotations;

namespace Mbpc.Api.DTOs.Catalogos
{
    // ── BuqueAltaDto ──────────────────────────────────────────────────────────
    // Payload de creación de buque. El CosteraId NUNCA viene del cliente;
    // el controller lo extrae del JWT y lo inyecta en el servicio.

    public class BuqueAltaDto
    {
        /// <summary>Nombre comercial o de matrícula del buque. Obligatorio.</summary>
        [Required(ErrorMessage = "El nombre del buque es obligatorio.")]
        [MaxLength(200, ErrorMessage = "El nombre no puede superar los 200 caracteres.")]
        public string Nombre { get; set; } = null!;

        /// <summary>
        /// Número IMO (International Maritime Organization).
        /// Opcional: se usa para buques de bandera extranjera o ultramar.
        /// Si se informa, debe ser positivo y único en el padrón.
        /// </summary>
        [Range(1, 9_999_999, ErrorMessage = "El número OMI debe ser un entero positivo de hasta 7 dígitos.")]
        public int? NroOmi { get; set; }

        /// <summary>
        /// MMSI del transponder AIS (9 dígitos). Opcional, único si informado.
        /// </summary>
        [RegularExpression(@"^\d{9}$", ErrorMessage = "El MMSI debe ser un número de exactamente 9 dígitos.")]
        public string? Mmsi { get; set; }

        /// <summary>Matrícula oficial de la embarcación. Opcional, única si informada.</summary>
        [MaxLength(50, ErrorMessage = "La matrícula no puede superar los 50 caracteres.")]
        public string? Matricula { get; set; }

        /// <summary>Bandera (país de registro) del buque.</summary>
        [MaxLength(100, ErrorMessage = "La bandera no puede superar los 100 caracteres.")]
        public string? Bandera { get; set; }

        /// <summary>
        /// Tipo de embarcación (ej: Buque Motor, Remolcador, Embarcación Menor, Barcaza).
        /// Obligatorio.
        /// </summary>
        [Required(ErrorMessage = "El tipo de embarcación es obligatorio.")]
        [MaxLength(60, ErrorMessage = "El tipo no puede superar los 60 caracteres.")]
        public string Tipo { get; set; } = null!;

        /// <summary>
        /// Calado máximo en metros. Positivo si informado.
        /// </summary>
        [Range(0.0, 30.0, ErrorMessage = "El calado debe estar entre 0 y 30 metros.")]
        public double? Calado { get; set; }

        /// <summary>Indicativo de llamada (Call Sign) del transponder.</summary>
        [MaxLength(20)]
        public string? CallSign { get; set; }

        /// <summary>
        /// Estado inicial del registro: Activo / Inactivo. Por defecto: Activo.
        /// </summary>
        public string Estado { get; set; } = "Activo";
    }

    // ── BuqueEditDto ──────────────────────────────────────────────────────────

    public class BuqueEditDto
    {
        /// <summary>Identificador numérico del buque en el padrón BUQUES_NEW. Obligatorio.</summary>
        [Required(ErrorMessage = "El IdBuque es obligatorio para la edición.")]
        [Range(1, long.MaxValue, ErrorMessage = "IdBuque debe ser un entero positivo.")]
        public long IdBuque { get; set; }

        /// <summary>Nuevo nombre del buque. Obligatorio.</summary>
        [Required(ErrorMessage = "El nombre del buque es obligatorio.")]
        [MaxLength(200)]
        public string Nombre { get; set; } = null!;

        /// <summary>
        /// Número OMI actualizado. Si se cambia, el servicio valida unicidad excluyendo el propio registro.
        /// </summary>
        [Range(1, 9_999_999, ErrorMessage = "El número OMI debe ser un entero positivo de hasta 7 dígitos.")]
        public int? NroOmi { get; set; }

        /// <summary>MMSI actualizado. 9 dígitos exactos.</summary>
        [RegularExpression(@"^\d{9}$", ErrorMessage = "El MMSI debe ser un número de exactamente 9 dígitos.")]
        public string? Mmsi { get; set; }

        /// <summary>Matrícula actualizada.</summary>
        [MaxLength(50)]
        public string? Matricula { get; set; }

        /// <summary>Bandera actualizada.</summary>
        [MaxLength(100)]
        public string? Bandera { get; set; }

        /// <summary>Tipo de embarcación actualizado.</summary>
        [Required(ErrorMessage = "El tipo de embarcación es obligatorio.")]
        [MaxLength(60)]
        public string Tipo { get; set; } = null!;

        /// <summary>Calado actualizado en metros.</summary>
        [Range(0.0, 30.0)]
        public double? Calado { get; set; }

        /// <summary>Call Sign actualizado.</summary>
        [MaxLength(20)]
        public string? CallSign { get; set; }

        /// <summary>Estado del registro (Activo / Inactivo / Dado de Baja).</summary>
        [MaxLength(30)]
        public string? Estado { get; set; }
    }

    // ── BuqueDetalleDto ───────────────────────────────────────────────────────
    // DTO de respuesta completa para GET /api/buques/{id}.
    // Mapea 1:1 contra el resultado de Dapper/Oracle.

    public class BuqueDetalleDto
    {
        public long   IdBuque   { get; set; }
        public string? Nombre   { get; set; }
        public string? Omi      { get; set; }
        public string? Mmsi     { get; set; }
        public string? Matricula { get; set; }
        public string? Bandera  { get; set; }
        public string? Tipo     { get; set; }
        public double? Calado   { get; set; }
        public string? CallSign { get; set; }
        public string? Estado   { get; set; }
        public int?    CosteraId { get; set; }
    }
}
