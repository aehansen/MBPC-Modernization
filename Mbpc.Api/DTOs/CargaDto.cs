namespace Mbpc.Api.DTOs
{
    public class CargaDto
    {
        public string Id { get; set; }
        public string ViajeId { get; set; }
        
        // Formateamos la vista en el DTO para que el frontend solo tenga que pintar datos
        public string DescripcionLista { get; set; } // Ej: "Soja (5000 tons.)"
        public string NivelRiesgo { get; set; } // Ej: "Alto" o "Estándar"
    }
}