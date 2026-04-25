using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Mbpc.Api.DTOs;
using Mbpc.Api.Services;
using System.Threading.Tasks;

namespace Mbpc.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    // [Authorize] // Lo dejo comentado temporalmente para facilitar la primera prueba de conexión sin lidiar con el token JWT. Luego lo activamos.
    public class ChatController : ControllerBase
    {
        private readonly IViajeService _viajeService;

        public ChatController(IViajeService viajeService)
        {
            _viajeService = viajeService;
        }

        [HttpPost]
        public async Task<ActionResult<ChatResponseDto>> PostMessage([FromBody] ChatRequestDto request)
        {
            if (string.IsNullOrWhiteSpace(request.Message))
                return BadRequest("El mensaje no puede estar vacío.");

            // MOCK DE ORQUESTACIÓN: Simula el tiempo de la IA y consulta la base real
            await Task.Delay(1000); 
            var viajes = await _viajeService.GetViajesAsync();
            
            var response = new ChatResponseDto
            {
                Reply = $"¡Conexión HTTP exitosa desde React al backend .NET! Recibí tu mensaje: '{request.Message}'. " +
                        $"Además, la API leyó la base local y encontró {viajes.Count} buques en jurisdicción."
            };

            return Ok(response);
        }
    }
}