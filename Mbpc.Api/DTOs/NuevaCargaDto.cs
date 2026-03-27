namespace Mbpc.Api.DTOs
{
    public class NuevaCargaDto
    {
        public string Nombre { get; set; } = string.Empty;
        public string Tipo { get; set; } = string.Empty; // "Barcaza" o "Bodega"
        public double Tonelaje { get; set; }
    }
}