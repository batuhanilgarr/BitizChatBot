using BitizChatBot.Models.DTOs;

namespace BitizChatBot.Models;

public class ConversationContext
{
    public string SessionId { get; set; } = Guid.NewGuid().ToString();
    public IntentType? CurrentIntent { get; set; }
    public Dictionary<string, string> CollectedParameters { get; set; } = new();
    public DateTime LastActivity { get; set; } = DateTime.UtcNow;
    
    // Tire search specific
    public string? Brand { get; set; }
    public string? Model { get; set; }
    public string? Year { get; set; }
    public string? Season { get; set; }
}

