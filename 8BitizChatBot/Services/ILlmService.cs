using BitizChatBot.Models.DTOs;
using BitizChatBot.Models;

namespace BitizChatBot.Services;

public interface ILlmService
{
    Task<string> GenerateResponseAsync(string userMessage, string systemPrompt, double temperature, int maxTokens);
    Task<IntentDetectionResult> DetectIntentAsync(string userMessage, string systemPrompt, ConversationContext? context = null);
}

