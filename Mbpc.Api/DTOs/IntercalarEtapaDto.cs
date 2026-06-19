using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Mbpc.Api.DTOs
{
    public class IntercalarEtapaDto
    {
        [Required(ErrorMessage = "La fecha de inicio de la etapa es requerida.")]
        public DateTime FechaInicio { get; set; }

        public DateTime? FechaFin { get; set; }

        public string? RemolcadorNombre { get; set; }
        public string? RemolcadorMatricula { get; set; }
        public List<string>? BarcazasNombres { get; set; }
    }
}
