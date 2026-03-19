using Microsoft.AspNetCore.Mvc;
using Mbpc.Api.Services;
using Mbpc.Api.Models.Mongo;
using Mbpc.Api.DTOs; // <-- AGREGAR ESTO
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Mbpc.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ViajesController : ControllerBase
    {
        private readonly IViajeService _viajeService;

        public ViajesController(IViajeService viajeService)
        {
            _viajeService = viajeService;
        }

        [HttpGet]
        public async Task<ActionResult<List<ViajeDto>>> GetViajes()
        {
            var posicionesMongo = await _viajeService.GetViajesAsync();

            var viajesDto = posicionesMongo.Select(p => new ViajeDto
            {
                Id = p.VesselName,
                Buque = p.VesselName ?? "DESCONOCIDO",
                Ruta = $"Lat: {p.Latitude} | Lon: {p.Longitude} ({p.SpeedOverGround} nds)",
                FechaInicioFormateada = p.MsgTime.ToString("dd/MM/yyyy HH:mm"),
                EstadoActual = p.NavegationStatusDesc ?? "N/A"
            }).ToList();

            return Ok(viajesDto);
        }

        [HttpGet("{mmsi}")]
        public async Task<ActionResult<ViajePosicionMongo>> GetViajeByMmsi(string mmsi)
        {
            var viaje = await _viajeService.GetViajeByMmsiAsync(mmsi);
            
            if (viaje == null)
            {
                return NotFound(new { mensaje = $"No se encontró posición para el buque con MMSI {mmsi}" });
            }

            return Ok(viaje);
        }
    }
}