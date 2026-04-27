// Archivo: Mbpc.Api/Services/IChatService.cs
namespace Mbpc.Api.Services
{
    public interface IChatService
    {
        Task<(string Reply, string ConversationId)> GetChatResponseAsync(
            string userMessage,
            string? conversationId = null,
            CancellationToken ct = default);
    }
}
