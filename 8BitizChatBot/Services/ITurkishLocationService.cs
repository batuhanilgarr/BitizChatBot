namespace BitizChatBot.Services;

public interface ITurkishLocationService
{
    Task InitializeAsync();
    string? FindCity(string message);
    string? FindDistrict(string message, string? city = null);
    bool IsValidCity(string city);
    bool IsValidDistrict(string district, string city);
}

