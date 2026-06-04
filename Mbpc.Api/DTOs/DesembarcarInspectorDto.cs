using System.ComponentModel.DataAnnotations;

namespace Mbpc.Api.DTOs
{
    public class DesembarcarInspectorDto
    {
        public DateTime FechaDesembarque { get; set; } = DateTime.UtcNow;
    }
}
