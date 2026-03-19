namespace Mbpc.Api.DTOs
{
    public class ViajeDto
    {
        public string Id { get; set; }
        public string Buque { get; set; }
        public string Ruta { get; set; } // Concatenaremos Origen -> Destino
        public string FechaInicioFormateada { get; set; }
        public string EstadoActual { get; set; }
    }
}