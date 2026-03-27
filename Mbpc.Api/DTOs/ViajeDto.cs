// ViajeDto.cs
// DTOs de presentación — exponen al Frontend la información nueva de EJE 1
// sin filtrar ruido de implementación Mongo/BSON.
// Namespace: Mbpc.Api.DTOs

namespace Mbpc.Api.DTOs
{
    // ══════════════════════════════════════════════════════════════════════════
    // DTO raíz — resumen de viaje para listas y tarjetas
    // ══════════════════════════════════════════════════════════════════════════
    public class ViajeDto
    {
        public string Id { get; set; } = null!;
        public string Buque { get; set; } = null!;
        /// <summary>Concatenación "Origen → Destino".</summary>
        public string Ruta { get; set; } = null!;
        public string FechaInicioFormateada { get; set; } = null!;
        public string EstadoActual { get; set; } = null!;

        // ── EJE 1: datos operativos extendidos ──────────────────────────────
        public string? CosteraId { get; set; }
        public List<BarcazaDto> Barcazas { get; set; } = new List<BarcazaDto>();
        public RemolcadorDto? Remolcador { get; set; }
        public List<EtapaDto> Etapas { get; set; } = new List<EtapaDto>();
        public List<PracticoDto> Practicos { get; set; } = new List<PracticoDto>();
        public List<InspectorDto> Inspectores { get; set; } = new List<InspectorDto>();
    }

    // ══════════════════════════════════════════════════════════════════════════
    // DTOs de Etapa — el estado viaja como string al Frontend para desacoplarlo
    // del Enum interno: el cliente no debe depender de nuestro modelo de dominio.
    // ══════════════════════════════════════════════════════════════════════════
    public record EtapaDto(
        string? PuntoControl,
        string? Hrp,
        string? Eta,
        /// <summary>
        /// Estado como string ("Navegando", "Fondeado", etc.).
        /// Se convierte desde EstadoEtapa en la capa de mapeo del servicio.
        /// </summary>
        string Estado,
        bool EsActiva
    );

    // ══════════════════════════════════════════════════════════════════════════
    // DTOs de Barcaza y Remolcador
    // ══════════════════════════════════════════════════════════════════════════
    public record BarcazaDto(
        string Nombre,
        string Bandera,
        string? Matricula,
        string Carga,
        double Cantidad,
        string Unidad,
        string? MuelleActual
    );

    public record RemolcadorDto(
        string Nombre,
        string Estado,
        string? FechaSalida
    );

    // ══════════════════════════════════════════════════════════════════════════
    // DTOs de Práctico e Inspector — EJE 1 nuevos
    // ══════════════════════════════════════════════════════════════════════════
    public record PracticoDto(
        string Nombre,
        string? FechaEmbarque,
        string? FechaDesembarque,
        string? Zona
    );

    public record InspectorDto(
        string Nombre,
        string Organismo
    );
}