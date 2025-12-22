using System.Text.Json;
using BitizChatBot.Models.DTOs;

namespace BitizChatBot.Services;

public class ExternalApiService : IExternalApiService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ExternalApiService> _logger;
    private const string BaseUrl = "https://test.bridgestone.com.tr/api/ai";

    public ExternalApiService(HttpClient httpClient, ILogger<ExternalApiService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _httpClient.Timeout = TimeSpan.FromSeconds(30);
    }

    public async Task<DealerSearchResponse> SearchDealersByLocationAsync(double latitude, double longitude)
    {
        try
        {
            var url = $"{BaseUrl}/SearchDealers?lat={latitude}&longitude={longitude}";
            _logger.LogInformation("Calling API: {Url}", url);

            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<DealerSearchResponse>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            return result ?? new DealerSearchResponse
            {
                Success = false,
                Message = "Failed to parse response"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching dealers by location");
            return new DealerSearchResponse
            {
                Success = false,
                Message = $"Error: {ex.Message}"
            };
        }
    }

    public async Task<DealerSearchResponse> SearchDealersByCityDistrictAsync(string city, string district)
    {
        try
        {
            var url = $"{BaseUrl}/SearchByLocation?city={Uri.EscapeDataString(city)}&district={Uri.EscapeDataString(district)}";
            _logger.LogInformation("Calling API: {Url}", url);

            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<DealerSearchResponse>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            return result ?? new DealerSearchResponse
            {
                Success = false,
                Message = "Failed to parse response"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching dealers by city/district");
            return new DealerSearchResponse
            {
                Success = false,
                Message = $"Error: {ex.Message}"
            };
        }
    }

    public async Task<TireSearchResponse> SearchTiresAsync(string brand, string model, int year, string season)
    {
        try
        {
            // Sezon bilgisi kullanıcıdan istenmediği için API çağrısına eklemiyoruz
            var url = $"{BaseUrl}/Search?brand={Uri.EscapeDataString(brand)}&model={Uri.EscapeDataString(model)}&year={year}";
            _logger.LogInformation("Calling API: {Url}", url);

            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            
            // API iki farklı formatta dönebiliyor:
            // 1) Başarılı ise: dizi olarak lastikler
            //    [ { ...TireDto }, ... ]
            // 2) Hata / yanlış marka-model ise: sarmalanmış obje
            //    { "success": false, "message": "...", "data": [] }
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var trimmed = content.TrimStart();

            if (trimmed.StartsWith("{"))
            {
                // Sarmalanmış cevap
                var apiResponse = JsonSerializer.Deserialize<BridgestoneTireApiResponse>(content, options);

                var tiresFromData = apiResponse?.Data ?? new List<TireDto>();
                var message = !string.IsNullOrWhiteSpace(apiResponse?.Message)
                    ? apiResponse!.Message
                    : (tiresFromData.Count > 0
                        ? $"{tiresFromData.Count} adet lastik bulundu"
                        : "Lastik bulunamadı");

                return new TireSearchResponse
                {
                    Tires = tiresFromData,
                    Message = message
                };
            }
            else
            {
                // Dizi formatında cevap
                var tires = JsonSerializer.Deserialize<List<TireDto>>(content, options);

                return new TireSearchResponse
                {
                    Tires = tires ?? new List<TireDto>(),
                    Message = tires?.Count > 0 ? $"{tires.Count} adet lastik bulundu" : "Lastik bulunamadı"
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching tires");
            return new TireSearchResponse
            {
                Tires = new List<TireDto>(),
                Message = $"Error: {ex.Message}"
            };
        }
    }

    // Bridgestone lastik arama API cevabı için yardımcı model
    private class BridgestoneTireApiResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public List<TireDto> Data { get; set; } = new();
    }
}

