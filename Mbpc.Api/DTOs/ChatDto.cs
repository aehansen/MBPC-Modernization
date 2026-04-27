namespace Mbpc.Api.DTOs
{
    public class ChatRequestDto
    {
        public string Message { get; set; } = string.Empty;
        public string? ConversationId { get; set; }
    }

    public class ChatResponseDto
    {
        public string Reply { get; set; } = string.Empty;
        public bool IsSuccess { get; set; } = true;
        public string? ConversationId { get; set; }
    }
}
