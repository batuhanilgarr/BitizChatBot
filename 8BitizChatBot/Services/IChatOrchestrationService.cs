using BitizChatBot.Models;
using BitizChatBot.Models.DTOs;

namespace BitizChatBot.Services;

public interface IChatOrchestrationService
{
    Task<ChatResponse> ProcessMessageAsync(string userMessage, string? sessionId = null);
    void ClearContext(string sessionId);
    Task ClearContextAsync(string sessionId);
}

public class ChatResponse
{
    public string Message { get; set; } = string.Empty;
    public List<DealerDto>? Dealers { get; set; }
    public List<TireDto>? Tires { get; set; }
}

