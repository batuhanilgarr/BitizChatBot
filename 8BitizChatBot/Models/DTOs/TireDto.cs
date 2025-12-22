using System.Text.Json.Serialization;

namespace BitizChatBot.Models.DTOs;

public class TireDto
{
    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("availableSizes")]
    public string AvailableSizes { get; set; } = string.Empty;

    [JsonPropertyName("productUrl")]
    public string ProductUrl { get; set; } = string.Empty;

    [JsonPropertyName("season")]
    public string Season { get; set; } = string.Empty;

    // Helper properties
    public string Name => Content;
    public List<string> ProductUrls => ProductUrl?.Split(',').Select(u => u.Trim()).Where(u => !string.IsNullOrEmpty(u)).ToList() ?? new List<string>();
}

public class TireSearchResponse
{
    public List<TireDto> Tires { get; set; } = new();

    public bool Success => Tires.Count > 0;

    public string Message { get; set; } = string.Empty;
}

