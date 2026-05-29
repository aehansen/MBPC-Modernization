using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Mbpc.Api.DTOs
{
    public class ViajeComplementosDto
    {
        public string ViajeId { get; set; } = null!;
        public List<NotaBitacoraDto> NotasBitacora { get; set; } = new();
        public List<AgenciaDto> Agencias { get; set; } = new();
        public DatosPbipDto? DatosPbip { get; set; }

        public ViajeComplementosDto() { }

        public ViajeComplementosDto(string ViajeId, List<NotaBitacoraDto> NotasBitacora, List<AgenciaDto> Agencias, DatosPbipDto? DatosPbip)
        {
            this.ViajeId = ViajeId;
            this.NotasBitacora = NotasBitacora;
            this.Agencias = Agencias;
            this.DatosPbip = DatosPbip;
        }
    }

    public class NotaBitacoraDto
    {
        public string Id { get; set; } = null!;
        public string Texto { get; set; } = null!;
        public string Usuario { get; set; } = null!;
        public DateTime FechaHora { get; set; }
        public string Categoria { get; set; } = null!;

        public NotaBitacoraDto() { }

        public NotaBitacoraDto(string Id, string Texto, string Usuario, DateTime FechaHora, string Categoria)
        {
            this.Id = Id;
            this.Texto = Texto;
            this.Usuario = Usuario;
            this.FechaHora = FechaHora;
            this.Categoria = Categoria;
        }
    }

    public class AgenciaDto
    {
        public string Rol { get; set; } = null!;
        public string Nombre { get; set; } = null!;
        public string Contacto { get; set; } = null!;

        public AgenciaDto() { }

        public AgenciaDto(string Rol, string Nombre, string Contacto)
        {
            this.Rol = Rol;
            this.Nombre = Nombre;
            this.Contacto = Contacto;
        }
    }

    public class DatosPbipDto
    {
        public string ContactoOcpm { get; set; } = null!;
        public string NroInmarsat { get; set; } = null!;
        public double? ArqueoBruto { get; set; }
        public int NivelProteccion { get; set; }

        public DatosPbipDto() { }

        public DatosPbipDto(string ContactoOcpm, string NroInmarsat, double? ArqueoBruto, int NivelProteccion)
        {
            this.ContactoOcpm = ContactoOcpm;
            this.NroInmarsat = NroInmarsat;
            this.ArqueoBruto = ArqueoBruto;
            this.NivelProteccion = NivelProteccion;
        }
    }

    public class AgregarNotaBitacoraDto
    {
        [Required(ErrorMessage = "El texto de la nota es obligatorio.")]
        public string Texto { get; set; } = null!;

        [Required(ErrorMessage = "La categoría de la nota es obligatoria.")]
        public string Categoria { get; set; } = null!;
    }

    public class AsignarAgenciaDto
    {
        [Required(ErrorMessage = "El rol de la agencia es obligatorio.")]
        public string Rol { get; set; } = null!;

        [Required(ErrorMessage = "El nombre de la agencia es obligatorio.")]
        public string Nombre { get; set; } = null!;

        public string Contacto { get; set; } = string.Empty;
    }

    public class ActualizarDatosPbipDto
    {
        public string? ContactoOcpm { get; set; }
        public string? NroInmarsat { get; set; }
        public double ArqueoBruto { get; set; } // double no nullable para coincidir con la entidad DatosPbipMongo
        public int NivelProteccion { get; set; }
    }
}