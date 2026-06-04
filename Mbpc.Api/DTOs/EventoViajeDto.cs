using System;

namespace Mbpc.Api.DTOs
{
    public class EventoViajeDto
    {
        public string Id { get; set; } = null!;
        public string TipoEvento { get; set; } = null!;
        public DateTime FechaHora { get; set; }
        public string Usuario { get; set; } = null!;
        public string Detalle { get; set; } = null!;
        public string? EstadoAnterior { get; set; }
        public string? EstadoNuevo { get; set; }
    }
}
