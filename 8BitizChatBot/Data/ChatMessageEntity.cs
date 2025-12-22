using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BitizChatBot.Models;

public class ChatMessageEntity
{
    [Key]
    public long Id { get; set; }
    
    [Required]
    [MaxLength(100)]
    public string SessionId { get; set; } = string.Empty;
    
    [ForeignKey("SessionId")]
    public ChatSession? Session { get; set; }
    
    [Required]
    public bool IsUser { get; set; }
    
    [Required]
    public string Content { get; set; } = string.Empty;
    
    public string? ErrorMessage { get; set; }
    
    // JSON serialized data
    public string? DealersJson { get; set; }
    public string? TiresJson { get; set; }
    
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

