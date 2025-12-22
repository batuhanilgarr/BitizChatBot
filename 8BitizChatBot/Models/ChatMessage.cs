using BitizChatBot.Models.DTOs;

namespace BitizChatBot.Models;

public class ChatMessage
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Content { get; set; } = string.Empty;
    public bool IsUser { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public bool IsLoading { get; set; }
    public string? ErrorMessage { get; set; }
    public bool ShowLocationButton { get; set; }
    public List<DealerDto>? Dealers { get; set; }
    public List<TireDto>? Tires { get; set; }
}

