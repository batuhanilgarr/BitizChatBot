namespace BitizChatBot.Models.DTOs;

public enum IntentType
{
    Unknown,
    DealerSearchByLocation,
    DealerSearchByCityDistrict,
    TireSearch,
    GeneralQuestion
}

public class IntentDetectionResult
{
    public IntentType Intent { get; set; } = IntentType.Unknown;
    public Dictionary<string, string> Parameters { get; set; } = new();
    public string? UserMessage { get; set; }
    public bool RequiresClarification { get; set; }
    public string? ClarificationMessage { get; set; }
}

