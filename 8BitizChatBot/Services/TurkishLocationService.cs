using System.Text.Json;
using Microsoft.Extensions.Hosting;

namespace BitizChatBot.Services;

public class TurkishLocationService : ITurkishLocationService
{
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<TurkishLocationService> _logger;
    private Dictionary<string, string> _cities = new(); // lowercase -> original name
    private Dictionary<string, Dictionary<string, string>> _districtsByCity = new(); // city -> (lowercase -> original name)
    private bool _initialized = false;
    private readonly object _lock = new object();

    public TurkishLocationService(IWebHostEnvironment environment, ILogger<TurkishLocationService> logger)
    {
        _environment = environment;
        _logger = logger;
    }

    public async Task InitializeAsync()
    {
        if (_initialized)
            return;

        lock (_lock)
        {
            if (_initialized)
                return;

            try
            {
                var dataPath = Path.Combine(_environment.ContentRootPath, "Data");
                var ilPath = Path.Combine(dataPath, "il.json");
                var ilcePath = Path.Combine(dataPath, "ilce.json");

                // Load cities
                if (File.Exists(ilPath))
                {
                    var ilJson = File.ReadAllText(ilPath);
                    var ilDoc = JsonDocument.Parse(ilJson);
                    
                    if (ilDoc.RootElement.TryGetProperty("data", out var dataArray))
                    {
                        foreach (var city in dataArray.EnumerateArray())
                        {
                            if (city.TryGetProperty("name", out var nameElement))
                            {
                                var name = nameElement.GetString();
                                if (!string.IsNullOrEmpty(name))
                                {
                                    _cities[name.ToLowerInvariant()] = name;
                                }
                            }
                        }
                    }
                    _logger.LogInformation("Loaded {Count} cities from JSON", _cities.Count);
                }

                // First, create il_id -> city name mapping
                var ilIdToCityName = new Dictionary<string, string>();
                if (File.Exists(ilPath))
                {
                    var ilJson = File.ReadAllText(ilPath);
                    var ilDoc = JsonDocument.Parse(ilJson);
                    
                    if (ilDoc.RootElement.TryGetProperty("data", out var dataArray))
                    {
                        foreach (var city in dataArray.EnumerateArray())
                        {
                            if (city.TryGetProperty("id", out var idElement) &&
                                city.TryGetProperty("name", out var nameElement))
                            {
                                var id = idElement.GetString();
                                var name = nameElement.GetString();
                                if (!string.IsNullOrEmpty(id) && !string.IsNullOrEmpty(name))
                                {
                                    ilIdToCityName[id] = name.ToLowerInvariant();
                                }
                            }
                        }
                    }
                }

                // Load districts and map them to cities
                if (File.Exists(ilcePath))
                {
                    var ilceJson = File.ReadAllText(ilcePath);
                    var ilceDoc = JsonDocument.Parse(ilceJson);
                    
                    if (ilceDoc.RootElement.TryGetProperty("data", out var ilceDataArray))
                    {
                        foreach (var district in ilceDataArray.EnumerateArray())
                        {
                            if (district.TryGetProperty("il_id", out var ilIdElement) &&
                                district.TryGetProperty("name", out var nameElement))
                            {
                                var ilId = ilIdElement.GetString();
                                var name = nameElement.GetString();
                                
                                if (!string.IsNullOrEmpty(ilId) && !string.IsNullOrEmpty(name) &&
                                    ilIdToCityName.TryGetValue(ilId, out var cityName))
                                {
                                    if (!_districtsByCity.ContainsKey(cityName))
                                    {
                                        _districtsByCity[cityName] = new Dictionary<string, string>();
                                    }
                                    _districtsByCity[cityName][name.ToLowerInvariant()] = name;
                                }
                            }
                        }
                    }
                    _logger.LogInformation("Loaded {Count} districts from JSON", 
                        _districtsByCity.Values.Sum(d => d.Count));
                }

                _initialized = true;
                _logger.LogInformation("TurkishLocationService initialized successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing TurkishLocationService");
                // Initialize with empty dictionaries to prevent crashes
                _cities = new Dictionary<string, string>();
                _districtsByCity = new Dictionary<string, Dictionary<string, string>>();
            }
        }
    }

    public string? FindCity(string message)
    {
        if (!_initialized)
            return null;

        var lowerMessage = message.ToLowerInvariant();
        var normalizedMessage = NormalizeTurkish(lowerMessage);
        
        // Try exact match first
        foreach (var kvp in _cities)
        {
            // Hem doğrudan hem de aksansız hale getirilmiş karşılaştırma yap
            var key = kvp.Key;
            if (lowerMessage.Contains(key) || normalizedMessage.Contains(NormalizeTurkish(key)))
            {
                return kvp.Value; // Return original name
            }
        }

        return null;
    }

    public string? FindDistrict(string message, string? city = null)
    {
        if (!_initialized)
            return null;

        var lowerMessage = message.ToLowerInvariant();
        var normalizedMessage = NormalizeTurkish(lowerMessage);
        var lowerCity = city?.ToLowerInvariant();

        // If city is specified, search only in that city's districts
        if (!string.IsNullOrEmpty(lowerCity) && _districtsByCity.TryGetValue(lowerCity, out var districts))
        {
            foreach (var kvp in districts)
            {
                var key = kvp.Key;
                if (lowerMessage.Contains(key) || normalizedMessage.Contains(NormalizeTurkish(key)))
                {
                    return kvp.Value; // Return original name
                }
            }
        }
        else
        {
            // Search in all districts
            foreach (var cityDistricts in _districtsByCity.Values)
            {
                foreach (var kvp in cityDistricts)
                {
                    var key = kvp.Key;
                    if (lowerMessage.Contains(key) || normalizedMessage.Contains(NormalizeTurkish(key)))
                    {
                        return kvp.Value; // Return original name
                    }
                }
            }
        }

        return null;
    }

    public bool IsValidCity(string city)
    {
        if (!_initialized || string.IsNullOrEmpty(city))
            return false;

        return _cities.ContainsKey(city.ToLowerInvariant());
    }

    public bool IsValidDistrict(string district, string city)
    {
        if (!_initialized || string.IsNullOrEmpty(district) || string.IsNullOrEmpty(city))
            return false;

        var lowerCity = city.ToLowerInvariant();
        var lowerDistrict = district.ToLowerInvariant();

        if (_districtsByCity.TryGetValue(lowerCity, out var districts))
        {
            return districts.ContainsKey(lowerDistrict);
        }

        return false;
    }

    /// <summary>
    /// Türkçe karakterleri aksansız hale getirir (istanbul / i̇stanbul, ümraniye / umraniye gibi).
    /// Mesaj ve şehir/ilçe isimlerini aynı forma çekip daha toleranslı eşleşme için kullanılır.
    /// </summary>
    private static string NormalizeTurkish(string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        return input
            .Replace('ı', 'i')
            .Replace('İ', 'i')
            .Replace('i', 'i')
            .Replace('ş', 's')
            .Replace('Ş', 's')
            .Replace('ğ', 'g')
            .Replace('Ğ', 'g')
            .Replace('ü', 'u')
            .Replace('Ü', 'u')
            .Replace('ö', 'o')
            .Replace('Ö', 'o')
            .Replace('ç', 'c')
            .Replace('Ç', 'c');
    }
}

