// Archivo: Mbpc.Api/Controllers/ChatController.cs
using Microsoft.AspNetCore.Mvc;
using Mbpc.Api.DTOs;
using Mbpc.Api.Services;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Mbpc.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    // [Authorize] — Descomentá cuando el frontend envíe el JWT correctamente.
    public class ChatController : ControllerBase
    {
        private readonly IChatService            _chatService;
        private readonly ILogger<ChatController> _logger;

        public ChatController(
            IChatService            chatService,
            ILogger<ChatController> logger)
        {
            _chatService = chatService;
            _logger      = logger;
        }

        [HttpPost]
        public async Task<ActionResult<ChatResponseDto>> PostMessage(
            [FromBody] ChatRequestDto request,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(request?.Message))
                return BadRequest(new { mensaje = "El mensaje no puede estar vacío." });

            try
            {
                var (reply, conversationId) = await _chatService.GetChatResponseAsync(
                    request.Message,
                    request.ConversationId,
                    ct);

                return Ok(new ChatResponseDto
                {
                    Reply          = reply,
                    IsSuccess      = true,
                    ConversationId = conversationId
                });
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("API Key"))
            {
                _logger.LogError(ex, "Configuración inválida de Gemini.");
                return StatusCode(500, new ChatResponseDto
                {
                    Reply     = "API Key no configurada.",
                    IsSuccess = false
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al procesar mensaje con Gemini.");
                return StatusCode(500, new ChatResponseDto
                {
                    Reply     = "Error interno del servidor al contactar al LLM.",
                    IsSuccess = false
                });
            }
        }
    }
}
