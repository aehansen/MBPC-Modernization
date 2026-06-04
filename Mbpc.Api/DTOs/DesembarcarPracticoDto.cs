using System.ComponentModel.DataAnnotations;

namespace Mbpc.Api.DTOs
{
    public class DesembarcarPracticoDto
    {
        public DateTime FechaDesembarque { get; set; } = DateTime.UtcNow;
    }
}
