namespace Mbpc.Api.DTOs
{
    public class CargaDto
    {
        // El "= null!;" apaga las advertencias amarillas de .NET 8
        public string Id { get; set; } = null!;
        public string ViajeId { get; set; } = null!;
        public string DescripcionLista { get; set; } = null!;
        public string NivelRiesgo { get; set; } = null!;
        
        // Campo para la sincronización CQRS
        public string? MuelleActual { get; set; }

        // ¡ESTE ES EL CAMPO QUE FALTABA Y ROMPÍA LA COMPILACIÓN!
        public double Tonelaje { get; set; }
    }
}