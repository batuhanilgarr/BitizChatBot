using BitizChatBot.Models.DTOs;

namespace BitizChatBot.Services;

public interface IExternalApiService
{
    Task<DealerSearchResponse> SearchDealersByLocationAsync(double latitude, double longitude);
    Task<DealerSearchResponse> SearchDealersByCityDistrictAsync(string city, string district);
    Task<TireSearchResponse> SearchTiresAsync(string brand, string model, int year, string season);
    Task<(bool IsMismatch, string? Message)> ValidateBrandModelAsync(string brand, string model);
}

