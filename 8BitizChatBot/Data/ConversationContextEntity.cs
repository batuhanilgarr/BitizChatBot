using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BitizChatBot.Models;

public class ConversationContextEntity
{
    [Key]
    [MaxLength(100)]
    public string SessionId { get; set; } = string.Empty;
    
    [ForeignKey("SessionId")]
    public ChatSession? Session { get; set; }
    
    // Intent tracking
    public string? CurrentIntent { get; set; }
    
    // Collected parameters as JSON
    [Column(TypeName = "text")]
    public string? CollectedParametersJson { get; set; }
    
    // Tire search specific
    [MaxLength(100)]
    public string? Brand { get; set; }
    
    [MaxLength(100)]
    public string? Model { get; set; }
    
    [MaxLength(10)]
    public string? Year { get; set; }
    
    [MaxLength(50)]
    public string? Season { get; set; }
    
    // Validation counters
    public int BrandModelInvalidAttempts { get; set; } = 0;
    
    // WhatsApp follow-up flow
    public bool AwaitingWhatsAppConsent { get; set; } = false;
    public bool AwaitingWhatsAppPhone { get; set; } = false;
    
    [Column(TypeName = "text")]
    public string? LastDealerSummary { get; set; }
    
    public DateTime LastActivity { get; set; } = DateTime.UtcNow;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

