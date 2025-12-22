using System.Text.Json.Serialization;

namespace BitizChatBot.Models;

public class DomainApiKey
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Domain { get; set; } = string.Empty; // example.com
    public string ApiKey { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}


