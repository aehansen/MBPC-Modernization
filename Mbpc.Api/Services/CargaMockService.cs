using System;
using System.Collections.Generic;
using System.Linq;
using Mbpc.Api.Models;
using Mbpc.Api.DTOs;

namespace Mbpc.Api.Services
{
    public interface ICargaService
    {
        // Cambiamos int a string acá
        IEnumerable<CargaDto> ObtenerCargasPorViaje(string viajeId);
        bool AmarrarBarcaza(string cargaId, string nuevoMuelle);
        bool FondearBarcaza(string cargaId, string zonaFondeo);
    }

    public class CargaMockService : ICargaService
    {
        private readonly List<Carga> _cargasEnMemoria;

        public CargaMockService()
        {
            _cargasEnMemoria = new List<Carga>
            {
                new Carga { Id = 1, ViajeId = 101, TipoMercaderia = "Combustible", Toneladas = 1500.5, CantidadUnidades = 1, EsPeligrosa = true }
            };
        }

        // Cambiamos int a string acá para que coincida con la interfaz
        public IEnumerable<CargaDto> ObtenerCargasPorViaje(string viajeId)
        {
            // Como el mock usaba ints, hacemos un parseo temporal solo para que compile
            if (int.TryParse(viajeId, out int vId))
            {
                return _cargasEnMemoria
                    .Where(c => c.ViajeId == vId)
                    .Select(c => new CargaDto
                    {
                        Id = c.Id.ToString(), // Convertimos el int a string
                        ViajeId = c.ViajeId.ToString(), // Convertimos el int a string
                        DescripcionLista = $"{c.TipoMercaderia} ({c.Toneladas} tons.)",
                        NivelRiesgo = c.EsPeligrosa ? "Alto" : "Estándar"
                    });
            }
            return new List<CargaDto>();
        }

        public bool AmarrarBarcaza(string cargaId, string nuevoMuelle) => true;
        public bool FondearBarcaza(string cargaId, string zonaFondeo) => true;
    }
}