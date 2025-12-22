using System.ComponentModel.DataAnnotations;

namespace BitizChatBot.Models;

public class DomainAppearanceEntity
{
    [Key]
    public int Id { get; set; }
    
    [Required]
    [MaxLength(255)]
    public string Domain { get; set; } = string.Empty;
    
    public string ChatbotName { get; set; } = "Bridgestone Chatbot";
    public string? ChatbotLogoUrl { get; set; }
    public string PrimaryColor { get; set; } = "#000000";
    public string SecondaryColor { get; set; } = "#dc143c";
    public string WelcomeMessage { get; set; } = string.Empty;
    public bool ChatbotOnline { get; set; } = true;
    public bool OpenChatOnLoad { get; set; } = false;
    public string QuickRepliesJson { get; set; } = "[]"; // JSON array as string
    
    // Hazır Cevaplar
    public string? GreetingResponse { get; set; }
    public string? HowAreYouResponse { get; set; }
    public string? WhoAreYouResponse { get; set; }
    public string? WhatCanYouDoResponse { get; set; }
    public string? ThanksResponse { get; set; }
    public string? GoodbyeResponse { get; set; }
    
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

