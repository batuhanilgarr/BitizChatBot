using System.Text.Json;

namespace BitizChatBot.Services;

public class OllamaService : IOllamaService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<OllamaService> _logger;

    public OllamaService(HttpClient httpClient, ILogger<OllamaService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _httpClient.Timeout = TimeSpan.FromSeconds(10);
    }

    public async Task<bool> TestConnectionAsync(string baseUrl)
    {
        try
        {
            var url = $"{baseUrl.TrimEnd('/')}/api/tags";
            var response = await _httpClient.GetAsync(url);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error testing Ollama connection");
            return false;
        }
    }

    public async Task<List<string>> GetAvailableModelsAsync(string baseUrl)
    {
        try
        {
            var url = $"{baseUrl.TrimEnd('/')}/api/tags";
            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<JsonElement>(content);

            var models = new List<string>();
            if (result.TryGetProperty("models", out var modelsArray) && modelsArray.ValueKind == JsonValueKind.Array)
            {
                foreach (var model in modelsArray.EnumerateArray())
                {
                    if (model.TryGetProperty("name", out var name))
                    {
                        models.Add(name.GetString() ?? string.Empty);
                    }
                }
            }

            return models.Where(m => !string.IsNullOrEmpty(m)).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching Ollama models");
            return new List<string>();
        }
    }
}

