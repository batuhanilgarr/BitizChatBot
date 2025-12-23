using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BitizChatBot.Models;

public class AuditLogEntity
{
    [Key]
    public long Id { get; set; }
    
    [MaxLength(100)]
    public string? UserId { get; set; }
    
    [MaxLength(100)]
    public string? Username { get; set; }
    
    [Required]
    [MaxLength(50)]
    public string Action { get; set; } = string.Empty; // Login, Logout, CreateUser, UpdateSettings, etc.
    
    [MaxLength(255)]
    public string? EntityType { get; set; } // User, DomainApiKey, DomainAppearance, etc.
    
    [MaxLength(100)]
    public string? EntityId { get; set; }
    
    [Column(TypeName = "text")]
    public string? Details { get; set; } // JSON or text details
    
    [MaxLength(45)]
    public string? IpAddress { get; set; }
    
    [MaxLength(500)]
    public string? UserAgent { get; set; }
    
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    
    public bool Success { get; set; } = true;
    
    [MaxLength(500)]
    public string? ErrorMessage { get; set; }
}

