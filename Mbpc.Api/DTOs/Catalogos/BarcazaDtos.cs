using System.ComponentModel.DataAnnotations;

namespace Mbpc.Api.DTOs.Catalogos
{
    // ── BarcazaAltaDto ────────────────────────────────────────────────────────
    // Las barcazas son un tipo específico de embarcación sin propulsión propia.
    // A diferencia de los buques, NO tienen OMI ni MMSI.
    // Su identificador regulatorio es la matrícula (Ej: PY-101, ARG-201).

    public class BarcazaAltaDto
    {
        /// <summary>Nombre comercial de la barcaza (ej: UABL 101). Obligatorio.</summary>
        [Required(ErrorMessage = "El nombre de la barcaza es obligatorio.")]
        [MaxLength(200, ErrorMessage = "El nombre no puede superar los 200 caracteres.")]
        public string Nombre { get; set; } = null!;

        /// <summary>
        /// Matrícula oficial. Debe ser única en el padrón si se informa.
        /// Valores especiales aceptados: 'EN TRAMITE', 'PASAVANTE', 'S/M'.
        /// </summary>
        [MaxLength(50, ErrorMessage = "La matrícula no puede superar los 50 caracteres.")]
        public string? Matricula { get; set; }

        /// <summary>Bandera (país de registro).</summary>
        [MaxLength(100)]
        public string? Bandera { get; set; }

        /// <summary>
        /// Capacidad máxima de carga en toneladas métricas.
        /// Positiva si informada.
        /// </summary>
        [Range(0.0, 99_999.0, ErrorMessage = "La capacidad debe estar entre 0 y 99.999 toneladas.")]
        public double? CapacidadTn { get; set; }

        /// <summary>Largo total de la barcaza en metros.</summary>
        [Range(0.0, 500.0, ErrorMessage = "El largo debe estar entre 0 y 500 metros.")]
        public double? LargoM { get; set; }

        /// <summary>Manga (ancho) de la barcaza en metros.</summary>
        [Range(0.0, 100.0, ErrorMessage = "La manga debe estar entre 0 y 100 metros.")]
        public double? MangaM { get; set; }

        /// <summary>Propietario/operador de la barcaza.</summary>
        [MaxLength(200)]
        public string? Propietario { get; set; }

        /// <summary>Estado inicial del registro: Activo / Inactivo. Por defecto: Activo.</summary>
        public string Estado { get; set; } = "Activo";
    }

    // ── BarcazaEditDto ────────────────────────────────────────────────────────

    public class BarcazaEditDto
    {
        /// <summary>Identificador numérico de la barcaza. Obligatorio.</summary>
        [Required(ErrorMessage = "El IdBarcaza es obligatorio para la edición.")]
        [Range(1, long.MaxValue, ErrorMessage = "IdBarcaza debe ser un entero positivo.")]
        public long IdBarcaza { get; set; }

        /// <summary>Nombre actualizado. Obligatorio.</summary>
        [Required(ErrorMessage = "El nombre de la barcaza es obligatorio.")]
        [MaxLength(200)]
        public string Nombre { get; set; } = null!;

        /// <summary>Matrícula actualizada. Única si informada (validación server-side).</summary>
        [MaxLength(50)]
        public string? Matricula { get; set; }

        /// <summary>Bandera actualizada.</summary>
        [MaxLength(100)]
        public string? Bandera { get; set; }

        /// <summary>Capacidad máxima actualizada en toneladas.</summary>
        [Range(0.0, 99_999.0)]
        public double? CapacidadTn { get; set; }

        /// <summary>Largo total actualizado en metros.</summary>
        [Range(0.0, 500.0)]
        public double? LargoM { get; set; }

        /// <summary>Manga actualizada en metros.</summary>
        [Range(0.0, 100.0)]
        public double? MangaM { get; set; }

        /// <summary>Propietario/operador actualizado.</summary>
        [MaxLength(200)]
        public string? Propietario { get; set; }

        /// <summary>Estado del registro (Activo / Inactivo / Dado de Baja).</summary>
        [MaxLength(30)]
        public string? Estado { get; set; }
    }

    // ── BarcazaDetalleDto ─────────────────────────────────────────────────────
    // DTO de respuesta completa para GET /api/buques/barcazas/{id}.

    public class BarcazaDetalleDto
    {
        public long    IdBarcaza   { get; set; }
        public string? Nombre      { get; set; }
        public string? Matricula   { get; set; }
        public string? Bandera     { get; set; }
        public double? CapacidadTn { get; set; }
        public double? LargoM      { get; set; }
        public double? MangaM      { get; set; }
        public string? Propietario { get; set; }
        public string? Estado      { get; set; }
        public int?    CosteraId   { get; set; }
    }
}
