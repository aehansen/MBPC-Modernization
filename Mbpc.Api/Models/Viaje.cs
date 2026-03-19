using System;

namespace Mbpc.Api.Models
{
    public class Viaje
    {
        public int Id { get; set; }
        public string NombreBuque { get; set; }
        public string Origen { get; set; }
        public string Destino { get; set; }
        public DateTime FechaInicio { get; set; }
        public string Estado { get; set; } // Ej: "En Curso", "Fondeado", "Terminado"
    }
}