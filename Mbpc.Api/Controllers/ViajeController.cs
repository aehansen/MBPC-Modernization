using Microsoft.AspNetCore.Mvc;
using Mbpc.Api.Services;
using Mbpc.Api.Models.Mongo;
using Mbpc.Api.DTOs; // <-- AGREGAR ESTO
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Mbpc.Api.Controllers
{
    [ApiController]
    [Route("api/viajes")]
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
                Id = p.Id,
                Buque = p.VesselName ?? "DESCONOCIDO",
                Ruta = $"Lat: {Math.Round(p.Latitude, 4)} | Lon: {Math.Round(p.Longitude, 4)} ({Math.Round(p.SpeedOverGround, 1)} nds)",
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
        [HttpPost]
        public async Task<ActionResult> IniciarViaje([FromBody] NuevoViajeDto nuevoViaje)
        {
            // Log para confirmar que la petición entró al controlador
            Console.WriteLine($"\n[API] Recibida petición de despacho para buque: {nuevoViaje?.NombreBuque}");

            if (nuevoViaje == null || string.IsNullOrWhiteSpace(nuevoViaje.NombreBuque))
            {
                return BadRequest(new { mensaje = "Datos de buque inválidos o incompletos." });
            }

            var exito = await _viajeService.IniciarViajeAsync(nuevoViaje);
            
            if (!exito)
            {
                return StatusCode(500, new { mensaje = "Error interno al procesar el inicio de viaje." });
            }

            return Ok(new { mensaje = $"🚢 Viaje para {nuevoViaje.NombreBuque} iniciado correctamente." });
        }
    }
}