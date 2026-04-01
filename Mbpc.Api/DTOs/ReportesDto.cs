using System;

namespace Mbpc.Api.DTOs
{
    public record BarcoPuertoDto 
    {
        public string Id { get; init; } = null!;
        public string Buque { get; init; } = null!;
        public string Origen { get; init; } = null!;
        public string Destino { get; init; } = null!;
        public string Eta { get; init; } = null!;
        public string Estado { get; init; } = null!;
        public string Mmsi { get; init; } = null!;
    }

    public record ViajeHistoricoDto 
    {
        public string Id { get; init; } = null!;
        public string Buque { get; init; } = null!;
        public string Omi { get; init; } = null!;
        public string Matricula { get; init; } = null!;
        public string Origen { get; init; } = null!;
        public string Destino { get; init; } = null!;
        public string FechaPartida { get; init; } = null!;
        public string Eta { get; init; } = null!;
        public string Estado { get; init; } = null!;
        
        // ── NUEVO: MULTITENANT GEOGRÁFICO ──
        // Agregado con 'init' para respetar la inmutabilidad del record
        public string CosteraId { get; init; } = string.Empty;
    }

    public record FiltroHistoricoDto 
    {
        public string? Nombre { get; init; }
        public string? Omi { get; init; }
        public string? Matricula { get; init; }
        public string? Origen { get; init; }
        public string? Destino { get; init; }
        public DateTime? Desde { get; init; }
        public DateTime? Hasta { get; init; }
    }
}