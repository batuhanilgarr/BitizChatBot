using System.ComponentModel.DataAnnotations;

namespace BitizChatBot.Models;

public class UserEntity
{
    [Key]
    public int Id { get; set; }
    
    [Required]
    [MaxLength(100)]
    public string Username { get; set; } = string.Empty;
    
    [Required]
    [MaxLength(255)]
    public string PasswordHash { get; set; } = string.Empty;
    
    [Required]
    public bool IsAdmin { get; set; } = true;
    
    [MaxLength(255)]
    public string? Email { get; set; }
    
    [MaxLength(100)]
    public string? FullName { get; set; }
    
    public bool IsActive { get; set; } = true;
    
    public int FailedLoginAttempts { get; set; } = 0;
    
    public DateTime? LockedUntil { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public DateTime? LastLoginAt { get; set; }
    
    public DateTime? PasswordChangedAt { get; set; }
}

