using System.ComponentModel.DataAnnotations;

namespace BitizChatBot.Models;

public class AdminSettingsEntity
{
    [Key]
    public int Id { get; set; } = 1; // Always 1, single row

    public string LlmProvider { get; set; } = "Ollama";
    public string ModelName { get; set; } = "hermes3:8b";
    public string? ApiKey { get; set; }
    public string OllamaBaseUrl { get; set; } = "http://3.14.208.235:11434";
    public string SystemPrompt { get; set; } = string.Empty;
    public double Temperature { get; set; } = 0.7;
    public int MaxTokens { get; set; } = 2000;

    // Appearance settings
    public string ChatbotName { get; set; } = "Bridgestone Chatbot";
    public string? ChatbotLogoUrl { get; set; }
    public string PrimaryColor { get; set; } = "#000000";
    public string SecondaryColor { get; set; } = "#dc143c";
    public string WelcomeMessage { get; set; } = string.Empty;
    public bool ChatbotOnline { get; set; } = true;
    public bool OpenChatOnLoad { get; set; } = false;
    public string QuickRepliesJson { get; set; } = "[]"; // JSON array as string

    // Response templates
    public string? GreetingResponse { get; set; }
    public string? HowAreYouResponse { get; set; }
    public string? WhoAreYouResponse { get; set; }
    public string? WhatCanYouDoResponse { get; set; }
    public string? ThanksResponse { get; set; }
    public string? GoodbyeResponse { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

