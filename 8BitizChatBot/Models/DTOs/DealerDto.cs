using System.Text.Json.Serialization;

namespace BitizChatBot.Models.DTOs;

public class DealerDto
{
    [JsonPropertyName("unvan1")]
    public string Unvan1 { get; set; } = string.Empty;

    [JsonPropertyName("unvan2")]
    public string Unvan2 { get; set; } = string.Empty;

    [JsonPropertyName("il")]
    public string Il { get; set; } = string.Empty;

    [JsonPropertyName("ilce")]
    public string Ilce { get; set; } = string.Empty;

    [JsonPropertyName("adres1")]
    public string Adres1 { get; set; } = string.Empty;

    [JsonPropertyName("adres2")]
    public string Adres2 { get; set; } = string.Empty;

    [JsonPropertyName("telefon1")]
    public string Telefon1 { get; set; } = string.Empty;

    [JsonPropertyName("email")]
    public string Email { get; set; } = string.Empty;

    [JsonPropertyName("enlem")]
    public string Enlem { get; set; } = string.Empty;

    [JsonPropertyName("boylam")]
    public string Boylam { get; set; } = string.Empty;

    [JsonPropertyName("distance")]
    public double? Distance { get; set; }

    [JsonPropertyName("googleMapsUrl")]
    public string GoogleMapsUrl { get; set; } = string.Empty;

    // Helper properties
    public string FullName => $"{Unvan1} {Unvan2}".Trim();
    public string FullAddress => $"{Adres1} {Adres2}".Trim();
    public double? Latitude => double.TryParse(Enlem?.Replace(",", "."), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var lat) ? lat : null;
    public double? Longitude => double.TryParse(Boylam?.Replace(",", "."), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var lon) ? lon : null;
}

public class DealerSearchResponse
{
    [JsonPropertyName("data")]
    public List<DealerDto> Data { get; set; } = new();

    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    // Helper property for compatibility
    public List<DealerDto> Dealers => Data;
}

