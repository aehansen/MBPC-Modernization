using System;
using System.Collections.Generic;

namespace Mbpc.Api.DTOs
{
    public class EtapaDetalleDto
    {
        public long EtapaId { get; set; }
        public DateTime? FechaInicio { get; set; }
        public DateTime? FechaFin { get; set; }
        public string? RemolcadorNombre { get; set; }
        public string? RemolcadorMatricula { get; set; }
        public List<string> Barcazas { get; set; } = new();
    }
}
