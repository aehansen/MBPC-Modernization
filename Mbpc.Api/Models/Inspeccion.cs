using System;

namespace Mbpc.Api.Models
{
    public class Inspeccion
    {
        public Guid Id { get; set; }
        public string ViajeId { get; set; } = string.Empty;
        public int BuqueId { get; set; }
        public DateTime FechaInspeccion { get; set; }
        public string TipoInspeccion { get; set; } = string.Empty;
        public string Resultado { get; set; } = string.Empty;
        public string Observaciones { get; set; } = string.Empty;
        public string InspectorDatos { get; set; } = string.Empty;
        public string LugarInspeccion { get; set; } = string.Empty;
        public int CosteraId { get; set; }
    }
}
