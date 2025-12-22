using System.ComponentModel.DataAnnotations;

namespace BitizChatBot.Models;

public class DomainApiKeyEntity
{
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString();
    
    [Required]
    [MaxLength(255)]
    public string Domain { get; set; } = string.Empty;
    
    [Required]
    [MaxLength(500)]
    public string ApiKey { get; set; } = string.Empty;
    
    public bool IsActive { get; set; } = true;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

