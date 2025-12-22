namespace BitizChatBot.Services;

public interface IOllamaService
{
    Task<bool> TestConnectionAsync(string baseUrl);
    Task<List<string>> GetAvailableModelsAsync(string baseUrl);
}

