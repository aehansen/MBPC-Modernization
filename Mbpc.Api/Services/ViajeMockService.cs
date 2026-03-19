using System;
using System.Collections.Generic;
using System.Linq;
using Mbpc.Api.Models;
using Mbpc.Api.DTOs;

namespace Mbpc.Api.Services
{
    // 1. Actualizamos el contrato
    public interface IViajeService
    {
        IEnumerable<ViajeDto> ObtenerViajesActivos();
        ViajeDto CrearViaje(NuevoViajeDto nuevoViaje); // <--- Nueva operación
    }

    public class ViajeMockService : IViajeService
    {
        private readonly List<Viaje> _viajesEnMemoria;

        public ViajeMockService()
        {
            _viajesEnMemoria = new List<Viaje>
            {
                new Viaje { Id = 101, NombreBuque = "GC-24 MANTILLA", Origen = "Buenos Aires", Destino = "Rosario", FechaInicio = DateTime.Now.AddDays(-2), Estado = "En Curso" },
                new Viaje { Id = 102, NombreBuque = "BARCAZA T-05", Origen = "Zarate", Destino = "Campana", FechaInicio = DateTime.Now.AddHours(-5), Estado = "Fondeado" }
            };
        }

        public IEnumerable<ViajeDto> ObtenerViajesActivos()
        {
            return _viajesEnMemoria.Select(v => MapearADto(v)).OrderByDescending(v => v.Id);
        }

        // 2. Implementamos la nueva operación
        public ViajeDto CrearViaje(NuevoViajeDto nuevoViaje)
        {
            // Simulamos el autoincremental de la base de datos
            int nuevoId = _viajesEnMemoria.Max(v => v.Id) + 1;

            var viaje = new Viaje
            {
                Id = nuevoId,
                NombreBuque = nuevoViaje.NombreBuque.ToUpper(),
                Origen = nuevoViaje.Origen,
                Destino = nuevoViaje.Destino,
                FechaInicio = DateTime.Now,
                Estado = "En Curso" // Todo viaje nuevo nace en curso
            };

            _viajesEnMemoria.Add(viaje);

            return MapearADto(viaje);
        }

        // Método auxiliar privado para no repetir código
        private ViajeDto MapearADto(Viaje v)
        {
            return new ViajeDto
            {
                Id = v.Id.ToString(),
                Buque = v.NombreBuque,
                Ruta = $"{v.Origen} -> {v.Destino}",
                FechaInicioFormateada = v.FechaInicio.ToString("dd/MM/yyyy HH:mm"),
                EstadoActual = v.Estado
            };
        }
    }
}