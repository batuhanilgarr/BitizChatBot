using System.ComponentModel.DataAnnotations;

namespace BitizChatBot.Models;

public class ChatSession
{
    [Key]
    [MaxLength(100)]
    public string SessionId { get; set; } = Guid.NewGuid().ToString();
    
    [MaxLength(255)]
    public string? Domain { get; set; }
    
    [MaxLength(500)]
    public string? UserAgent { get; set; }
    
    [MaxLength(45)]
    public string? IpAddress { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastActivityAt { get; set; }
    public bool IsActive { get; set; } = true;
}

