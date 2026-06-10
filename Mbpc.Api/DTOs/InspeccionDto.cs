using System;

namespace Mbpc.Api.DTOs
{
    public class InspeccionDto
    {
        public Guid Id { get; set; }
        public string ViajeId { get; set; } = null!;
        public int BuqueId { get; set; }
        public DateTime FechaInspeccion { get; set; }
        public string TipoInspeccion { get; set; } = null!;
        public string Resultado { get; set; } = null!;
        public string Observaciones { get; set; } = null!;
        public string InspectorDatos { get; set; } = null!;
        public string LugarInspeccion { get; set; } = null!;
        public int CosteraId { get; set; }
    }
}
