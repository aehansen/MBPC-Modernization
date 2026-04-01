namespace Mbpc.Api.Models
{
    public class Carga
    {
        public int Id { get; set; }
        public int ViajeId { get; set; } // Relación clave con el viaje/barcaza
        public string TipoMercaderia { get; set; } = string.Empty;
        
        // Usamos double (o decimal) de forma nativa. ¡Adiós al parseo manual de strings!
        public double Toneladas { get; set; } 
        public int CantidadUnidades { get; set; }
        public bool EsPeligrosa { get; set; } // Mercancía IMO, por ejemplo
    }
}