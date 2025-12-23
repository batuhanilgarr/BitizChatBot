using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using BitizChatBot.Models;
using BitizChatBot.Models.DTOs;

namespace BitizChatBot.Services;

public class LlmService : ILlmService
{
    private readonly HttpClient _httpClient;
    private readonly IAdminSettingsService _settingsService;
    private readonly ITurkishLocationService _locationService;
    private readonly ILogger<LlmService> _logger;

    public LlmService(HttpClient httpClient, IAdminSettingsService settingsService, ITurkishLocationService locationService, ILogger<LlmService> logger)
    {
        _httpClient = httpClient;
        _settingsService = settingsService;
        _locationService = locationService;
        _logger = logger;
    }

    public async Task<string> GenerateResponseAsync(string userMessage, string systemPrompt, double temperature, int maxTokens)
    {
        try
        {
            var settings = await _settingsService.GetSettingsAsync();
            
            // Ollama doesn't require API key
            if (settings.LlmProvider.ToLower() != "ollama" && string.IsNullOrWhiteSpace(settings.ApiKey))
            {
                return "LLM API key is not configured. Please configure it in the admin dashboard.";
            }

            var apiUrl = GetApiUrl(settings.LlmProvider, settings.OllamaBaseUrl);
            var requestBody = CreateChatRequest(userMessage, systemPrompt, settings.ModelName, temperature, maxTokens, settings.LlmProvider);

            var request = new HttpRequestMessage(HttpMethod.Post, apiUrl);
            
            // Set headers based on provider
            if (settings.LlmProvider.ToLower() == "anthropic")
            {
                request.Headers.Add("x-api-key", settings.ApiKey);
                request.Headers.Add("anthropic-version", "2023-06-01");
            }
            else if (settings.LlmProvider.ToLower() == "ollama")
            {
                // Ollama doesn't require authentication headers
            }
            else
            {
                request.Headers.Add("Authorization", $"Bearer {settings.ApiKey}");
            }
            
            request.Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<JsonElement>(content);

            return ExtractResponseText(result, settings.LlmProvider);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating LLM response");
            return $"Sorry, I encountered an error: {ex.Message}";
        }
    }

    public async Task<IntentDetectionResult> DetectIntentAsync(string userMessage, string systemPrompt, ConversationContext? context = null)
    {
        try
        {
            // First try simple keyword-based detection (fast and reliable)
            var simpleResult = await SimpleIntentDetectionAsync(userMessage, context);
            
            // If TireSearch intent detected but brand/model not extracted, use LLM to extract them
            if (simpleResult.Intent == IntentType.TireSearch && 
                (string.IsNullOrEmpty(simpleResult.Parameters.GetValueOrDefault("brand")) || 
                 string.IsNullOrEmpty(simpleResult.Parameters.GetValueOrDefault("model"))))
            {
                _logger.LogInformation("TireSearch detected but brand/model missing, using LLM to extract");
                // Continue to LLM-based detection to extract brand/model
            }
            // If simple detection found a complete intent with all parameters, return it
            else if (simpleResult.Intent != IntentType.Unknown && simpleResult.Intent != IntentType.GeneralQuestion)
            {
                _logger.LogInformation("Intent detected via keyword matching: {Intent}", simpleResult.Intent);
                return simpleResult;
            }

            // If simple detection didn't find a specific intent, try LLM-based detection
            // But skip LLM if we're in a tire search context with all parameters (to avoid confusion)
            if (context?.CurrentIntent == IntentType.TireSearch && 
                !string.IsNullOrEmpty(context.Brand) && !string.IsNullOrEmpty(context.Model))
            {
                return simpleResult;
            }
            
            var settings = await _settingsService.GetSettingsAsync();
            
            // Ollama doesn't require API key
            if (settings.LlmProvider.ToLower() != "ollama" && string.IsNullOrWhiteSpace(settings.ApiKey))
            {
                // Fallback to simple detection if API key not configured
                return simpleResult;
            }

            var intentPrompt = BuildIntentDetectionPrompt(userMessage);
            var apiUrl = GetApiUrl(settings.LlmProvider, settings.OllamaBaseUrl);
            var requestBody = CreateChatRequest(intentPrompt, systemPrompt, settings.ModelName, 0.3, 500, settings.LlmProvider);

            var request = new HttpRequestMessage(HttpMethod.Post, apiUrl);
            
            // Set headers based on provider
            if (settings.LlmProvider.ToLower() == "anthropic")
            {
                request.Headers.Add("x-api-key", settings.ApiKey);
                request.Headers.Add("anthropic-version", "2023-06-01");
            }
            else if (settings.LlmProvider.ToLower() == "ollama")
            {
                // Ollama doesn't require authentication headers
            }
            else
            {
                request.Headers.Add("Authorization", $"Bearer {settings.ApiKey}");
            }
            
            request.Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<JsonElement>(content);
            var intentResponse = ExtractResponseText(result, settings.LlmProvider);

            var llmResult = await ParseIntentResponseAsync(intentResponse, userMessage);
            
            // If LLM didn't find a specific intent, use simple detection result
            if (llmResult.Intent == IntentType.Unknown || llmResult.Intent == IntentType.GeneralQuestion)
            {
                return simpleResult;
            }
            
            return llmResult;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error detecting intent, falling back to simple detection");
            // Fallback to simple detection on error
            return await SimpleIntentDetectionAsync(userMessage, context);
        }
    }

    private string GetApiUrl(string provider, string? ollamaBaseUrl = null)
    {
        return provider.ToLower() switch
        {
            "ollama" => $"{ollamaBaseUrl ?? "http://localhost:11434"}/api/chat",
            "openai" => "https://api.openai.com/v1/chat/completions",
            "anthropic" => "https://api.anthropic.com/v1/messages",
            _ => $"{ollamaBaseUrl ?? "http://localhost:11434"}/api/chat"
        };
    }

    private object CreateChatRequest(string userMessage, string systemPrompt, string model, double temperature, int maxTokens, string provider)
    {
        if (provider.ToLower() == "ollama")
        {
            // Ollama API format
            return new
            {
                model = model,
                messages = new[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = userMessage }
                },
                stream = false,
                options = new
                {
                    temperature = temperature,
                    num_predict = maxTokens
                }
            };
        }
        else if (provider.ToLower() == "anthropic")
        {
            // Anthropic API format
            return new
            {
                model = model,
                max_tokens = maxTokens,
                temperature = temperature,
                system = systemPrompt,
                messages = new[]
                {
                    new { role = "user", content = userMessage }
                }
            };
        }
        else
        {
            // OpenAI API format (default)
            return new
            {
                model = model,
                messages = new[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = userMessage }
                },
                temperature = temperature,
                max_tokens = maxTokens
            };
        }
    }

    private string ExtractResponseText(JsonElement result, string provider)
    {
        if (provider.ToLower() == "ollama")
        {
            // Ollama response format: { "message": { "content": "..." } }
            if (result.TryGetProperty("message", out var message))
            {
                if (message.TryGetProperty("content", out var content))
                {
                    return content.GetString() ?? string.Empty;
                }
            }
        }
        else if (provider.ToLower() == "anthropic")
        {
            if (result.TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.Array)
            {
                return content[0].GetProperty("text").GetString() ?? string.Empty;
            }
        }
        else
        {
            // OpenAI format
            if (result.TryGetProperty("choices", out var choices) && choices.ValueKind == JsonValueKind.Array && choices.GetArrayLength() > 0)
            {
                return choices[0].GetProperty("message").GetProperty("content").GetString() ?? string.Empty;
            }
        }
        return string.Empty;
    }

    private string BuildIntentDetectionPrompt(string userMessage)
    {
        return $@"Analyze the following user message and determine the intent. Respond ONLY with a JSON object in this exact format:
{{
  ""intent"": ""DealerSearchByLocation|DealerSearchByCityDistrict|TireSearch|GeneralQuestion"",
  ""parameters"": {{
    ""latitude"": ""<number if mentioned>"",
    ""longitude"": ""<number if mentioned>"",
    ""city"": ""<city name if mentioned>"",
    ""district"": ""<district name if mentioned>"",
    ""brand"": ""<vehicle brand if mentioned>"",
    ""model"": ""<vehicle model if mentioned>"",
    ""year"": ""<year if mentioned>"",
    ""season"": ""<summer|winter|all season if mentioned>""
  }},
  ""requiresClarification"": <true|false>,
  ""clarificationMessage"": ""<message if clarification needed>""
}}

User message: {userMessage}

Examples:
- ""En yakın bayi nerede?"" -> intent: DealerSearchByLocation, requiresClarification: true (needs lat/long)
- ""İstanbul Kadıköy bayi"" -> intent: DealerSearchByCityDistrict, city: İstanbul, district: Kadıköy
- ""2021 Corolla için yaz lastiği öner"" -> intent: TireSearch, brand: Toyota, model: Corolla, year: 2021, season: summer";
    }

    private async Task<IntentDetectionResult> ParseIntentResponseAsync(string intentResponse, string userMessage)
    {
        try
        {
            // Try to extract JSON from the response
            var jsonStart = intentResponse.IndexOf('{');
            var jsonEnd = intentResponse.LastIndexOf('}');
            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                var json = intentResponse.Substring(jsonStart, jsonEnd - jsonStart + 1);
                var result = JsonSerializer.Deserialize<IntentDetectionResult>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (result != null)
                {
                    result.UserMessage = userMessage;
                    return result;
                }
            }

            // Fallback: Simple keyword-based detection
            return await SimpleIntentDetectionAsync(userMessage);
        }
        catch
        {
            return await SimpleIntentDetectionAsync(userMessage);
        }
    }

    private async Task<IntentDetectionResult> SimpleIntentDetectionAsync(string userMessage, ConversationContext? context = null)
    {
        var lowerMessage = userMessage.ToLowerInvariant();
        var result = new IntentDetectionResult { UserMessage = userMessage };
        
        // Known vehicle brands (extended list)
        var vehicleBrands = new[]
        {
            "ALFA ROMEO",
            "ASKAM",
            "ASTON MARTIN",
            "AUDI",
            "BENTLEY",
            "BMC",
            "BMW",
            "BYD",
            "CATERHAM",
            "CHERY",
            "CHEVROLET",
            "CHRYSLER",
            "CITROEN",
            "CUPRA",
            "DACIA",
            "DAEWOO",
            "DAIHATSU",
            "DFM",
            "DFSK",
            "DODGE",
            "DS",
            "FAW",
            "FERRARI",
            "FIAT",
            "FISKER",
            "FORD",
            "FUSO",
            "GAZ",
            "GEELY",
            "HONDA",
            "HONGQI",
            "HYUNDAI",
            "INFINITI",
            "ISUZU",
            "IVECO",
            "IVECO-OTOYOL",
            "JAECOO",
            "JAGUAR",
            "JEEP",
            "KARSAN",
            "KIA",
            "LADA",
            "LAMBORGHINI",
            "LANCIA",
            "LAND ROVER",
            "LEAPMOTOR",
            "LEXUS",
            "MAHINDRA",
            "MASERATI",
            "MAXUS",
            "MAZDA",
            "MERCEDES",
            "MERCEDES-BENZ",
            "MG",
            "MINI",
            "MITSUBISHI",
            "NISSAN",
            "OPEL",
            "OTOKAR",
            "PEUGEOT",
            "PIAGGIO",
            "PORSCHE",
            "PROTON",
            "RENAULT",
            "ROLLS-ROYCE",
            "ROVER",
            "SAAB",
            "SAMAND",
            "SEAT",
            "SERES",
            "SKODA",
            "SKYWELL",
            "SMART",
            "SSANGYONG",
            "SUBARU",
            "SUZUKI",
            "TATA",
            "TESLA",
            "TOFA?",
            "TOFA_",
            "TOFAŞ",
            "TOGG",
            "TOYOTA",
            "TVR",
            "VOLKSWAGEN",
            "VOLVO",
            "VOYAH",
            "YUDO"
        }.Select(b => b.ToLowerInvariant()).ToArray();
        
        // Known vehicle models (extended list)
        var vehicleModels = new[]
        {
            "?AH?N",
            "_AH_N",
            "1 SERISI",
            "106",
            "107",
            "12",
            "124 SPIDER",
            "145",
            "146",
            "147",
            "156",
            "159",
            "166",
            "19",
            "2 SERISI",
            "2 SERISI ACTIVE TOURER",
            "200 SX",
            "2008",
            "206",
            "206 +",
            "207",
            "208",
            "25",
            "296",
            "3 SERISI",
            "3008",
            "300C",
            "300M",
            "301",
            "306",
            "307",
            "308",
            "3200 GT",
            "323",
            "35.9",
            "350Z",
            "360",
            "360 MODENA",
            "360 SPIDER",
            "370Z",
            "4 SERISI",
            "4007",
            "406",
            "407",
            "408",
            "45",
            "456M",
            "456M GT",
            "458",
            "488",
            "4C",
            "4X4",
            "5 SERISI",
            "500",
            "500 C",
            "5008",
            "500C",
            "500L",
            "500L LIVING",
            "500X",
            "508",
            "550 MARANELLO",
            "575M MARANELLO",
            "599",
            "6 SERISI",
            "600",
            "607",
            "612",
            "626",
            "7 SERISI",
            "718",
            "75",
            "8 SERISI",
            "806",
            "812",
            "9",
            "9.Mar",
            "9.May",
            "911",
            "9-3",
            "9-3 SPORT SEDAN",
            "9-3 SPORT WAGON",
            "9-5",
            "A1",
            "A3",
            "A4",
            "A4 ALLROAD QUATTRO",
            "A5",
            "A6",
            "A6 ALLROAD QUATTRO",
            "A7",
            "A8",
            "ACCENT",
            "ACCENT BLUE",
            "ACCENT ERA",
            "ACCORD",
            "A-CLASS",
            "ACTIS V70",
            "ACTYON",
            "ACTYON SPORTS",
            "ADAM",
            "AGILA",
            "ALBEA",
            "ALHAMBRA",
            "ALIA",
            "ALLROAD",
            "ALMERA",
            "ALTEA",
            "ALTEA FREETRACK",
            "ALTEA XL",
            "ALTO",
            "AMAROK",
            "AMG GT",
            "ANTARA",
            "ARENA",
            "ARNAGE",
            "ARONA",
            "ARTEON",
            "AS 250",
            "A-SERISI",
            "ASTRA",
            "ASTRAVAN",
            "ASX",
            "ATECA",
            "ATEGO",
            "ATLAS",
            "ATOS",
            "ATOS PRIME",
            "ATTO 3",
            "ATTRAGE",
            "AURIS",
            "AUSTRAL",
            "AVENGER",
            "AVENSIS",
            "AVENTADOR",
            "AVEO",
            "AZURE",
            "B9 TRIBECA",
            "BALENO",
            "BAYON",
            "B-CLASS",
            "BEETLE",
            "BENTAYGA",
            "BERLINGO",
            "BIPPER",
            "B-MAX",
            "BOLERO",
            "BONGO",
            "BORA",
            "BORN",
            "BOXER",
            "BOXSTER",
            "BRAVA",
            "BRAVO",
            "BRERA",
            "BROOKLANDS",
            "BRZ",
            "B-SERISI",
            "BT-50",
            "C1",
            "C2",
            "C3",
            "C3 AIRCROSS",
            "C3 PICASSO",
            "C30",
            "C31",
            "C32",
            "C35",
            "C4",
            "C4 CACTUS",
            "C4 GRAND PICASSO",
            "C4 PICASSO",
            "C4 X",
            "C40",
            "C5",
            "C5 AIRCROSS",
            "C6",
            "C70",
            "C8",
            "CADDY",
            "CALIBER",
            "CALIFORNIA",
            "CALIFORNIA T",
            "CAMARO",
            "CAMRY",
            "CANTER",
            "CAPTIVA",
            "CAPTUR",
            "CARAVELLE",
            "CARENS",
            "CARISMA",
            "CARNIVAL",
            "CARRY",
            "CASCADA",
            "CAYENNE",
            "CAYENNE COUPE",
            "CAYMAN",
            "CC",
            "C-CLASS",
            "CEED",
            "C-ELYSEE",
            "CERATO",
            "CERES",
            "CHAIRMAN",
            "CHANCE",
            "CHEROKEE",
            "CHIMAERA",
            "C-HR",
            "CITAN",
            "CITIGO",
            "CITY",
            "CITYVAN",
            "CIVIC",
            "CLA",
            "CLA-SERISI",
            "CLC-SERISI",
            "CLE",
            "CLIO",
            "CLIO SPORT TOURER",
            "CLK-SERISI",
            "CLS",
            "CL-SERISI",
            "CLS-SERISI",
            "CLUBMAN",
            "C-MAX",
            "COLT",
            "COM5",
            "COMBO",
            "COMBO TOUR",
            "COMMANDER",
            "COMPASS",
            "CONTINENTAL FLYING SPUR",
            "CONTINENTAL FLYING SPUR 6.0 SPEED",
            "CONTINENTAL GT",
            "CONTINENTAL GTC",
            "CONTINENTAL SUPERSPORTS",
            "COOPER",
            "COPEN",
            "CORDOBA",
            "COROLLA",
            "COROLLA CROSS",
            "COROLLA VERSO",
            "CORSA",
            "CORSAVAN",
            "COUNTRYMAN",
            "COUPE",
            "CRAFTER",
            "CRAFTER VOLT",
            "CROSSFIRE",
            "CROSSLAND",
            "CROSSLAND X",
            "CROSSTREK",
            "CRUZE",
            "CR-V",
            "CR-Z",
            "C-SERISI",
            "CT",
            "CUORE",
            "CX-3",
            "CX-5",
            "CX-9",
            "DAILY",
            "DAMAS",
            "DB11",
            "DB9",
            "DBS",
            "DEFENDER",
            "DELTA",
            "DICOR",
            "DISCOVERY",
            "DISCOVERY SPORT",
            "D-MAX",
            "DO?AN",
            "DO_AN",
            "DOBLO",
            "DOBLO CARGO",
            "DOBLO CLASSIC",
            "DOBLO COMBI",
            "DOBLO COMBI MY",
            "DOBLO PANORAMA",
            "DOĞAN",
            "DOKKER",
            "DOLPHIN",
            "DREAM",
            "DS 3",
            "DS 3 CROSSBACK",
            "DS 4",
            "DS 4 CROSSBACK",
            "DS 5",
            "DS 7",
            "DS 7 CROSSBACK",
            "DS 9",
            "DUCATO",
            "DUSTER",
            "E5",
            "EC40",
            "EC7 EMGRAND",
            "ECHO",
            "E-CLASS",
            "ECLIPSE CROSS",
            "ECODAILY",
            "ECOSPORT",
            "E-DELIVER 3",
            "E-DELIVER 5",
            "E-DELIVER 7",
            "E-DELIVER 9",
            "EDGE",
            "EGEA",
            "EHS",
            "E-HS9",
            "ELANTRA",
            "ELITE",
            "EOS",
            "E-PACE",
            "EPICA",
            "EQA",
            "EQB",
            "EQC",
            "EQE",
            "EQS",
            "ES",
            "ESCORT",
            "E-SERISI",
            "ESPACE",
            "ET5",
            "E-TRON",
            "E-TRON GT",
            "E-TRON SPORTBACK",
            "EUROCARGO",
            "EV3",
            "EV6",
            "EV9",
            "EVANDA",
            "EVASION",
            "EX",
            "EX40",
            "EXEO",
            "EXPERT",
            "EXPERT TRAVELLER",
            "EXPLORER",
            "EXPRESS",
            "F12",
            "F-150",
            "F430",
            "F8",
            "FABIA",
            "FAMILIA",
            "FARGO FORA",
            "FC",
            "FELICIA",
            "FENGON 5",
            "FENGON 500",
            "FF",
            "FIESTA",
            "FIORINO",
            "FIORINO PANORAMA",
            "FLUENCE",
            "FLYING SPUR",
            "FOCUS",
            "FOCUS CC",
            "FOCUS C-MAX",
            "FORESTER",
            "FORFOUR",
            "FORMENTOR",
            "FORTWO",
            "F-PACE",
            "FREE",
            "FREELANDER",
            "FREELANDER 2",
            "FREEMONT",
            "FRONTERA",
            "F-TYPE",
            "FULLBACK",
            "FUSION",
            "FX",
            "G",
            "GALAXY",
            "GALLARDO",
            "GALLOPER",
            "GAZELLE",
            "GAZELLE NN",
            "G-CLASS",
            "GEN-2",
            "GENESIS",
            "GETZ",
            "GHIBLI",
            "GHOST",
            "GIULIA",
            "GIULIETTA",
            "GLA",
            "GLA-SERISI",
            "GLB",
            "GLC",
            "GLC COUPE",
            "GLC-SERISI",
            "GLC-SERISI COUPE",
            "GLE",
            "GLE-SERISI",
            "GLE-SERISI COUPE",
            "GLK-SERISI",
            "GL-SERISI",
            "GLS-SERISI",
            "GOA",
            "GOLF",
            "GOLF R",
            "GRANCABRIO",
            "GRAND CALIFORNIA",
            "GRAND CHEROKEE",
            "GRAND C-MAX",
            "GRAND VITARA",
            "GRAND VOYAGER",
            "GRANDE PUNTO",
            "GRANDE PUNTO S5",
            "GRANDEUR",
            "GRANDIS",
            "GRANDLAND",
            "GRANDLAND X",
            "GRANTURISMO",
            "GRECALE",
            "GRIFFITH",
            "GS",
            "G-SERISI",
            "GT",
            "GT 86",
            "GTC4",
            "GTV",
            "H-1",
            "H100",
            "H350",
            "HD 35",
            "HIACE",
            "HIJET",
            "HILUX",
            "HR-V",
            "HS",
            "HURACAN",
            "I10",
            "I20",
            "I20 TROY",
            "I3",
            "I30",
            "I4",
            "I40",
            "I5",
            "I7",
            "I8",
            "IBIZA",
            "IBIZA SPORT COUPE",
            "ID. BUZZ",
            "ID.4",
            "IDEA",
            "IMPREZA",
            "INCA",
            "INDICA",
            "INDIGO",
            "INSIGNIA",
            "IONIQ",
            "IONIQ 5",
            "IONIQ 6",
            "I-PACE",
            "IS",
            "IX",
            "IX1",
            "IX2",
            "IX20",
            "IX3",
            "IX35",
            "IX55",
            "J10",
            "J7",
            "J9",
            "J9 PREMIER",
            "J9 PREMIER MAXI",
            "JAZZ",
            "JETTA",
            "JIMNY",
            "JOGGER",
            "JOHN COOPER WORKS",
            "JOHN COOPER WORKS CLUBMAN",
            "JOURNEY",
            "JUKE",
            "JUMPER",
            "JUMPY",
            "JUNIOR",
            "K2500",
            "K2700",
            "K2900",
            "K3000S",
            "KA",
            "KADJAR",
            "KALINA",
            "KALOS",
            "KAMIQ",
            "KANGOO",
            "KANGOO EXPRESS",
            "KANGOO MULTIX",
            "KARMA",
            "KAROQ",
            "KARTAL",
            "KIMO",
            "KODIAQ",
            "KOLEOS",
            "KONA",
            "KORANDO",
            "KORANDO C",
            "KORANDO SPORTS",
            "KUGA",
            "KYRON",
            "L200",
            "L300",
            "LABO",
            "LACETTI",
            "LAGUNA",
            "LANCER",
            "LAND CRUISER",
            "LAND CRUISER PRADO",
            "LANDCRUISER PRADO",
            "LANOS",
            "LATITUDE",
            "LBX",
            "LC",
            "LEGACY",
            "LEGACY OUTBACK",
            "LEGANZA",
            "LEGEND",
            "LEON",
            "LEVANTE",
            "LEVEND",
            "LEVORG",
            "LIANA",
            "LINEA",
            "LM",
            "LODGY",
            "LOGAN",
            "LOGAN MCV",
            "LS",
            "LT",
            "LUBLIN",
            "LUPO",
            "M",
            "MACAN",
            "MAGENTIS",
            "MANZA",
            "MAREA",
            "MARINA",
            "MARUTI",
            "MARVEL R",
            "MASTER",
            "MATERIA",
            "MATIZ",
            "MATRIX",
            "MATRIX SPACE",
            "MAXIMA QX",
            "MAZDA2",
            "MAZDA3",
            "MAZDA5",
            "MAZDA6",
            "MC20",
            "MEGA",
            "MEGANE",
            "MEGANE E-TECH",
            "MEGASTAR",
            "MERIVA",
            "MG4",
            "MGF",
            "MICRA",
            "MIDI",
            "MINI",
            "MITO",
            "MODEL Y",
            "MODUS",
            "MOKKA",
            "MOKKA X",
            "MONDEO",
            "MOVANO",
            "MPV",
            "M-SERISI",
            "MULSANNE",
            "MULTIVAN",
            "MURANO",
            "MURCIELAGO",
            "MUSSO",
            "MUSTANG MACH-E",
            "MX-5",
            "MY FIORINO",
            "NAVARA",
            "NEMO",
            "NEON",
            "NICHE",
            "NIRO",
            "NITRO",
            "NIVA",
            "NKR",
            "NKR-WIDE",
            "NLR",
            "NNR",
            "NOTE",
            "NP300 PICK UP",
            "NPR",
            "NSX",
            "NUBIRA",
            "N-WIDE",
            "NX",
            "OCTAVIA",
            "OMEGA",
            "OMODA 5",
            "ONE",
            "OPIRUS",
            "OPTIMA",
            "OUTBACK",
            "OUTLANDER",
            "PACEMAN",
            "PAJERO",
            "PAJERO PININ",
            "PALIO",
            "PALIO VAN",
            "PANAMERA",
            "PANDA",
            "PANELVAN",
            "PARTNER",
            "PASSAT",
            "PASSAT ALLTRACK",
            "PASSAT VARIANT",
            "PATHFINDER",
            "PATRIOT",
            "PATROL",
            "PATROL GR",
            "PERSONA",
            "PHAETON",
            "PHANTOM",
            "PICANTO",
            "PICK UP",
            "PICK-UP",
            "PIK UP",
            "PIK UP EU4",
            "PIK UP EU5",
            "POLO",
            "PORTER",
            "PORTOFINO",
            "PRATICO",
            "PREGIO",
            "PREMACY",
            "PRIDE",
            "PRIMERA",
            "PRIORA",
            "PRIUS",
            "PRO CEED",
            "PROACE CITY",
            "PT CRUISER",
            "PULSAR",
            "PUMA",
            "PUNTO",
            "PUROSANGUE",
            "Q2",
            "Q3",
            "Q3 SPORTBACK",
            "Q30",
            "Q4",
            "Q4 SPORTBACK",
            "Q5",
            "Q5 SPORTBACK",
            "Q50",
            "Q60",
            "Q7",
            "Q8",
            "Q8 E-TRON",
            "Q8 E-TRON SPORTBACK",
            "QASHQAI",
            "QASHQAI+2",
            "QUATTROPORTE",
            "QX4",
            "QX70",
            "R8",
            "RAFALE",
            "RANGE ROVER",
            "RANGE ROVER EVOQUE",
            "RANGE ROVER SPORT",
            "RANGE ROVER VELAR",
            "RANGER",
            "RAPID",
            "RAPIDE",
            "RAPIDE S",
            "RAV4",
            "RC",
            "RCZ",
            "RENEGADE",
            "REXTON",
            "REZZO",
            "RICH",
            "RIFTER",
            "RIO",
            "ROADSTER",
            "RODIUS",
            "ROMA",
            "ROOMSTER",
            "RS3",
            "RS4",
            "RS5",
            "RS6",
            "RS7",
            "R-SERISI",
            "RX",
            "RX-8",
            "RZ",
            "S2000",
            "S3",
            "S4",
            "S40",
            "S5",
            "S6",
            "S60",
            "S60 CROSS COUNTRY",
            "S7",
            "S70",
            "S8",
            "S80",
            "S90",
            "SAFARI",
            "SAFRANE",
            "SAGA",
            "SAMAND LX",
            "SAMARA",
            "SAMURAI",
            "SANDERO",
            "SANDERO STEPWAY",
            "SANTA FE",
            "SAVVY",
            "SAXO",
            "SCALA",
            "SCENIC",
            "SCIROCCO",
            "S-CLASS",
            "S-CROSS",
            "SCUDO",
            "SEAL U",
            "SEBRING",
            "SEDICI",
            "SEPHIA",
            "SERES 3",
            "SERIES 1",
            "SERIES 2",
            "SERIES 2 ACTIVE TOURER",
            "SERIES 3",
            "SERIES 4",
            "SERIES 5",
            "SERIES 6",
            "SERIES 7",
            "SERIES 8",
            "SF90",
            "SHARAN",
            "SHUMA",
            "SIENA",
            "SIGNUM",
            "SIRION",
            "SL",
            "SLC-SERISI",
            "SLK-SERISI",
            "SLS",
            "SL-SERISI",
            "S-MAX",
            "SOBOL",
            "SOLENZA",
            "SOLTERRA",
            "SONATA",
            "SOREN",
            "SORENTO",
            "SOUL",
            "SPACE STAR",
            "SPARK",
            "SPEEDSTER",
            "SPIDER",
            "SPLASH",
            "SPORTAGE",
            "SPRING",
            "SPRINTER",
            "SPYDER",
            "S-SERISI",
            "STAREX",
            "STARIA",
            "STELVIO",
            "STILO",
            "STINGER",
            "STONIC",
            "STRADA",
            "STRATUS",
            "STREAM",
            "STREETWISE",
            "S-TYPE",
            "SUCCE",
            "SUPER 7",
            "SUPERB",
            "SWIFT",
            "SX4",
            "SX4 S-CROSS",
            "SYMBOL",
            "ŞAHİN",
            "T03",
            "T10X",
            "TAIGO",
            "TALIANT",
            "TALISMAN",
            "TARRACO",
            "TAXIM",
            "TAYCAN",
            "T-CROSS",
            "TELCOLINE",
            "TERIOS",
            "TERRANO",
            "TERRANO II",
            "TF",
            "TFR",
            "THEMA",
            "TICO",
            "TIGGO 7 PRO",
            "TIGGO 8 PRO",
            "TIGGO3",
            "TIGRA",
            "TIGUAN",
            "TIGUAN ALLSPACE",
            "TIPO",
            "TIVOLI",
            "TIVOLI XLV",
            "TOLEDO",
            "TONALE",
            "TORRES",
            "TOUAREG",
            "TOURAN",
            "TOURNEO CONNECT",
            "TOURNEO COURIER",
            "TOURNEO COURIER JOURNEY",
            "TOURNEO CUSTOM",
            "TRAFIC",
            "TRAFIC MULTIX",
            "TRAJET",
            "TRANSIT",
            "TRANSIT CONNECT",
            "TRANSIT COURIER",
            "TRANSIT CUSTOM",
            "TRANSPORTER",
            "TRAVELLER",
            "TRAX",
            "TRIBECA",
            "T-ROC",
            "TRUCK PLUS",
            "TT",
            "TT RS",
            "TUCSON",
            "TWIN",
            "TWINGO",
            "ULYSSE",
            "UNO",
            "URBAN CRUISER",
            "URVAN",
            "UX",
            "V12 VANTAGE",
            "V40",
            "V40 CROSS COUNTRY",
            "V50",
            "V60",
            "V60 CROSS COUNTRY",
            "V70",
            "V70 XC",
            "V8 VANTAGE",
            "V90",
            "V90 CROSS COUNTRY",
            "VANEO",
            "VANETTE",
            "VANETTE CITY VAN",
            "VANQUISH",
            "V-CLASS",
            "VECTRA",
            "VEGA",
            "VEL SATIS",
            "VENGA",
            "VERSO",
            "VIANO",
            "VIPER",
            "VIRAGE",
            "VISTA",
            "VITARA",
            "VITO",
            "VIVARO",
            "VOLT",
            "VOYAGER",
            "V-SERISI",
            "WAJA",
            "WRANGLER",
            "WRX",
            "WRX STI",
            "X1",
            "X2",
            "X3",
            "X4",
            "X5",
            "X6",
            "X7",
            "XANTIA",
            "XC40",
            "XC60",
            "XC70",
            "XC90",
            "XCEED",
            "X-CLASS",
            "XE",
            "XEDOS-9",
            "XENON",
            "XF",
            "XG",
            "XJ",
            "XK SERISI",
            "XLV",
            "XSARA",
            "XSARA PICASSO",
            "XT",
            "X-TRAIL",
            "X-TYPE",
            "XV",
            "YARIS",
            "YARIS CROSS",
            "YETI",
            "YPSILON",
            "YRV",
            "Z3",
            "Z4",
            "Z8",
            "ZAFIRA",
            "ZAFIRA TOURER",
            "ZOE",
            "ZR",
            "ZR-V",
            "ZS",
            "ZT",
            "ZT-T"
        }.Select(m => m.ToLowerInvariant()).ToArray();

        // Check if message contains coordinates (latitude/longitude pattern)
        // Pattern: "Latitude X, Longitude Y" or "X, Y" where X and Y are numbers
        var coordPattern = System.Text.RegularExpressions.Regex.Match(lowerMessage, 
            @"(?:latitude|lat|enlem)[\s:]*([+-]?\d+\.?\d*)[\s,]+(?:longitude|long|lng|boylam)[\s:]*([+-]?\d+\.?\d*)", 
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        
        if (coordPattern.Success && double.TryParse(coordPattern.Groups[1].Value, out var lat) && 
            double.TryParse(coordPattern.Groups[2].Value, out var lon))
        {
            _logger.LogInformation("Detected coordinates in message: Lat={Lat}, Lon={Lon}", lat, lon);
            result.Intent = IntentType.DealerSearchByLocation;
            result.Parameters["latitude"] = lat.ToString();
            result.Parameters["longitude"] = lon.ToString();
            result.RequiresClarification = false;
            return result;
        }

        // Try simple coordinate pattern: "X, Y" or "X Y"
        var simpleCoordPattern = System.Text.RegularExpressions.Regex.Match(lowerMessage, 
            @"([+-]?\d+\.?\d+)[\s,]+([+-]?\d+\.?\d+)");
        if (simpleCoordPattern.Success && double.TryParse(simpleCoordPattern.Groups[1].Value, out var lat2) && 
            double.TryParse(simpleCoordPattern.Groups[2].Value, out var lon2))
        {
            // Check if these look like coordinates (latitude: -90 to 90, longitude: -180 to 180)
            if (lat2 >= -90 && lat2 <= 90 && lon2 >= -180 && lon2 <= 180)
            {
                _logger.LogInformation("Detected coordinates in simple format: Lat={Lat}, Lon={Lon}", lat2, lon2);
                result.Intent = IntentType.DealerSearchByLocation;
                result.Parameters["latitude"] = lat2.ToString();
                result.Parameters["longitude"] = lon2.ToString();
                result.RequiresClarification = false;
                return result;
            }
        }

        // Check for location-based dealer search keywords
        // Look for "yakın", "yakınım", "en yakın" combined with "bayi", "dealer", "bayiler"
        var locationKeywords = new[] { "yakın", "yakınım", "en yakın", "yakındaki", "yakınımdaki", "en yakındaki" };
        var dealerKeywords = new[] { "bayi", "bayiler", "dealer", "dealers", "yetkili", "servis", "bayileri", "bayiyi" };
        var purchaseKeywords = new[] { "nereden alabilirim", "nereden alabilirim", "alabileceğim", "alabilecegim", "satın alabilirim", "satın alabilirim", "alabilirim", "nereden", "nereye", "nerede alabilirim", "nerede satın alabilirim" };
        var actionKeywords = new[] { "listele", "listeler", "göster", "bul", "bulur", "bulabilir", "bulabilir misin", "listeler misin", "gösterir misin", "listeler misin" };

        // Normalize user message and keywords to catch missing Turkish accents (e.g., "yakin" -> "yakın")
        var normalizedMessage = NormalizeForMatching(lowerMessage);
        var locationKeywordsNorm = locationKeywords.Select(NormalizeForMatching).ToArray();
        var dealerKeywordsNorm = dealerKeywords.Select(NormalizeForMatching).ToArray();
        var purchaseKeywordsNorm = purchaseKeywords.Select(NormalizeForMatching).ToArray();
        var actionKeywordsNorm = actionKeywords.Select(NormalizeForMatching).ToArray();
        
        // Fuzzy match to tolerate minor typos like "yakn" or "yakni"
        bool hasLocationKeyword = ContainsKeywordWithFuzzy(normalizedMessage, locationKeywordsNorm);
        bool hasDealerKeyword = ContainsKeywordWithFuzzy(normalizedMessage, dealerKeywordsNorm);
        bool hasPurchaseKeyword = ContainsKeywordWithFuzzy(normalizedMessage, purchaseKeywordsNorm);
        bool hasActionKeyword = ContainsKeywordWithFuzzy(normalizedMessage, actionKeywordsNorm);
        
        _logger.LogInformation("SimpleIntentDetection - Message: {Message}, HasLocation: {HasLocation}, HasDealer: {HasDealer}, HasPurchase: {HasPurchase}, HasAction: {HasAction}", 
            userMessage, hasLocationKeyword, hasDealerKeyword, hasPurchaseKeyword, hasActionKeyword);
        
        // Priority 0: If user just completed a tire search and asks where to buy, treat as dealer search
        if (context?.CurrentIntent == IntentType.TireSearch && (hasPurchaseKeyword || hasDealerKeyword || hasLocationKeyword))
        {
            _logger.LogInformation("Detected DealerSearch after TireSearch - User asking where to buy");
            // Check for city/district in message
            await _locationService.InitializeAsync();
            string? tireSearchCity = _locationService.FindCity(userMessage);
            string? tireSearchDistrict = null;
            if (tireSearchCity != null)
            {
                tireSearchDistrict = _locationService.FindDistrict(userMessage, tireSearchCity);
            }
            
            if (tireSearchCity != null)
            {
                result.Intent = IntentType.DealerSearchByCityDistrict;
                result.Parameters["city"] = tireSearchCity;
                if (!string.IsNullOrEmpty(tireSearchDistrict))
                {
                    result.Parameters["district"] = tireSearchDistrict;
                }
                _logger.LogInformation("Detected DealerSearchByCityDistrict after TireSearch - City: {City}, District: {District}", tireSearchCity, tireSearchDistrict ?? "none");
                return result;
            }
            else if (hasLocationKeyword)
            {
                result.Intent = IntentType.DealerSearchByLocation;
                result.RequiresClarification = true;
                result.ClarificationMessage = "Konumunuzu almak için izin verir misiniz? Konum butonuna tıklayın veya konumunuzu manuel olarak paylaşın.";
                return result;
            }
        }
        
        // Priority 1: "en yakın" + "bayi" pattern (most specific)
        if (lowerMessage.Contains("en yakın") && (hasDealerKeyword || hasPurchaseKeyword))
        {
            _logger.LogInformation("Detected DealerSearchByLocation via 'en yakın + bayi' pattern");
            result.Intent = IntentType.DealerSearchByLocation;
            result.RequiresClarification = true;
            result.ClarificationMessage = "Konumunuzu almak için izin verir misiniz? Konum butonuna tıklayın veya konumunuzu manuel olarak paylaşın.";
            return result;
        }
        
        // Priority 2: purchase keywords + location (user asking where to buy)
        if (hasPurchaseKeyword && hasLocationKeyword)
        {
            _logger.LogInformation("Detected DealerSearch via 'purchase + location' pattern");
            // Try to find city/district first
            await _locationService.InitializeAsync();
            string? purchaseCity = _locationService.FindCity(userMessage);
            string? purchaseDistrict = null;
            if (purchaseCity != null)
            {
                purchaseDistrict = _locationService.FindDistrict(userMessage, purchaseCity);
            }
            
            if (purchaseCity != null)
            {
                result.Intent = IntentType.DealerSearchByCityDistrict;
                result.Parameters["city"] = purchaseCity;
                if (!string.IsNullOrEmpty(purchaseDistrict))
                {
                    result.Parameters["district"] = purchaseDistrict;
                }
                _logger.LogInformation("Detected DealerSearchByCityDistrict - City: {City}, District: {District}", purchaseCity, purchaseDistrict ?? "none");
                return result;
            }
            else
            {
                result.Intent = IntentType.DealerSearchByLocation;
                result.RequiresClarification = true;
                result.ClarificationMessage = "Konumunuzu almak için izin verir misiniz? Konum butonuna tıklayın veya konumunuzu manuel olarak paylaşın.";
                return result;
            }
        }
        
        // Priority 3: location + dealer keywords
        if (hasLocationKeyword && hasDealerKeyword)
        {
            _logger.LogInformation("Detected DealerSearchByLocation via 'location + dealer' pattern");
            result.Intent = IntentType.DealerSearchByLocation;
            result.RequiresClarification = true;
            result.ClarificationMessage = "Konumunuzu almak için izin verir misiniz? Konum butonuna tıklayın veya konumunuzu manuel olarak paylaşın.";
            return result;
        }
        
        // Priority 4: location + action keyword (might be asking for nearest dealer)
        if (hasLocationKeyword && hasActionKeyword)
        {
            _logger.LogInformation("Detected DealerSearchByLocation via 'location + action' pattern");
            result.Intent = IntentType.DealerSearchByLocation;
            result.RequiresClarification = true;
            result.ClarificationMessage = "Konumunuzu almak için izin verir misiniz? Konum butonuna tıklayın veya konumunuzu manuel olarak paylaşın.";
            return result;
        }

        // Check for city/district dealer search using TurkishLocationService
        string? detectedCity = null;
        string? detectedDistrict = null;
        
        // Initialize location service if not already initialized
        await _locationService.InitializeAsync();
        
        // Find city in message
        detectedCity = _locationService.FindCity(userMessage);
        
        // If city found, try to find district
        if (detectedCity != null)
        {
            detectedDistrict = _locationService.FindDistrict(userMessage, detectedCity);
        }
        
        // Also check for dealer keywords to confirm this is a dealer search
        bool hasDealerInMessage = dealerKeywords.Any(k => lowerMessage.Contains(k)) || 
                                  purchaseKeywords.Any(k => lowerMessage.Contains(k)) ||
                                  actionKeywords.Any(k => lowerMessage.Contains(k)) ||
                                  lowerMessage.Contains("var mı") || 
                                  lowerMessage.Contains("var") ||
                                  lowerMessage.Contains("bulunur") ||
                                  lowerMessage.Contains("bulabilir");
        
        if (detectedCity != null && hasDealerInMessage)
        {
            result.Intent = IntentType.DealerSearchByCityDistrict;
            result.Parameters["city"] = detectedCity;
            if (!string.IsNullOrEmpty(detectedDistrict))
            {
                result.Parameters["district"] = detectedDistrict;
            }
            _logger.LogInformation("Detected DealerSearchByCityDistrict - City: {City}, District: {District}", detectedCity, detectedDistrict ?? "none");
            return result;
        }

        // Check for tire search keywords OR vehicle brand/model mentions
        bool hasTireKeyword = lowerMessage.Contains("lastik") || lowerMessage.Contains("tire") || 
                             lowerMessage.Contains("yaz") || lowerMessage.Contains("kış") ||
                             lowerMessage.Contains("lastiği") || lowerMessage.Contains("lastikleri");
        
        bool hasVehicleBrand = vehicleBrands.Any(brand => 
            System.Text.RegularExpressions.Regex.IsMatch(
                lowerMessage, 
                $@"\b{System.Text.RegularExpressions.Regex.Escape(brand)}\b"));
        
        bool hasVehicleModel = vehicleModels.Any(model =>
            System.Text.RegularExpressions.Regex.IsMatch(
                lowerMessage,
                $@"\b{System.Text.RegularExpressions.Regex.Escape(model)}\b"));
        bool hasYear = System.Text.RegularExpressions.Regex.IsMatch(userMessage, @"\b(19|20)\d{2}\b");
        
        // If user mentions vehicle brand/model/year or tire keywords, it's likely a tire search
        if (hasTireKeyword || hasVehicleBrand || hasVehicleModel || hasYear || context?.CurrentIntent == IntentType.TireSearch)
        {
            result.Intent = IntentType.TireSearch;
            
            // Try to extract vehicle brand from known brands
            foreach (var brand in vehicleBrands)
            {
                if (System.Text.RegularExpressions.Regex.IsMatch(
                        lowerMessage,
                        $@"\b{System.Text.RegularExpressions.Regex.Escape(brand)}\b"))
                {
                    result.Parameters["brand"] = brand;
                    break;
                }
            }
            
            // Try to extract vehicle model from known models
            foreach (var model in vehicleModels)
            {
                if (System.Text.RegularExpressions.Regex.IsMatch(
                        lowerMessage,
                        $@"\b{System.Text.RegularExpressions.Regex.Escape(model)}\b"))
                {
                    result.Parameters["model"] = model;
                    break;
                }
            }
            
            // If tire keyword exists but brand/model not found in our list,
            // return TireSearch intent but let LLM extract brand/model
            // (LLM knows many more brands/models than our hardcoded list)
            if (hasTireKeyword && 
                string.IsNullOrEmpty(result.Parameters.GetValueOrDefault("brand")) && 
                string.IsNullOrEmpty(result.Parameters.GetValueOrDefault("model")) &&
                !hasVehicleBrand && !hasVehicleModel)
            {
                _logger.LogInformation("Tire search detected but brand/model not in known list - will use LLM for extraction");
                // Keep TireSearch intent, extract year/season if available, then let LLM extract brand/model
            }
            
            // Try to extract year
            var yearMatch = System.Text.RegularExpressions.Regex.Match(userMessage, @"\b(19|20)\d{2}\b");
            if (yearMatch.Success)
            {
                result.Parameters["year"] = yearMatch.Value;
            }
            
            // Try to extract season
            if (lowerMessage.Contains("yaz") || lowerMessage.Contains("summer") || lowerMessage.Contains("yazlık"))
            {
                result.Parameters["season"] = "summer";
            }
            else if (lowerMessage.Contains("kış") || lowerMessage.Contains("winter") || lowerMessage.Contains("kışlık"))
            {
                result.Parameters["season"] = "winter";
            }
            else if (lowerMessage.Contains("dört mevsim") || lowerMessage.Contains("all season") || lowerMessage.Contains("allseason"))
            {
                result.Parameters["season"] = "all season";
            }
            
            // If we found brand/model in our list, return immediately
            if (!string.IsNullOrEmpty(result.Parameters.GetValueOrDefault("brand")) || 
                !string.IsNullOrEmpty(result.Parameters.GetValueOrDefault("model")) ||
                hasVehicleBrand || hasVehicleModel)
            {
                return result;
            }
            
            // If brand/model not found but tire search detected, return TireSearch intent
            // LLM will extract brand/model in DetectIntentAsync
            return result;
        }

        result.Intent = IntentType.GeneralQuestion;
        return result;
    }

    /// <summary>
    /// Accent-insensitive normalization for keyword matching (e.g., "yakin" -> "yakin" matches "yakın").
    /// Removes diacritics and maps Turkish dotted/undotted i to 'i'.
    /// </summary>
    private static string NormalizeForMatching(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        var normalized = text.Normalize(System.Text.NormalizationForm.FormD);
        var sb = new System.Text.StringBuilder(normalized.Length);
        foreach (var ch in normalized)
        {
            var uc = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(ch);
            if (uc != System.Globalization.UnicodeCategory.NonSpacingMark)
            {
                var c = ch switch
                {
                    'ı' or 'İ' => 'i',
                    _ => ch
                };
                sb.Append(char.ToLowerInvariant(c));
            }
        }
        return sb.ToString().Normalize(System.Text.NormalizationForm.FormC);
    }

    /// <summary>
    /// Checks if the normalized message contains any keyword (also normalized) with tolerance for small typos.
    /// Uses substring containment OR Levenshtein distance <= maxDistance on words and whole message.
    /// </summary>
    private static bool ContainsKeywordWithFuzzy(string normalizedMessage, string[] normalizedKeywords, int maxDistance = 1)
    {
        if (string.IsNullOrEmpty(normalizedMessage) || normalizedKeywords.Length == 0)
            return false;

        // Quick pass: direct substring match
        foreach (var k in normalizedKeywords)
        {
            if (normalizedMessage.Contains(k))
                return true;
        }

        // Token-level fuzzy match
        var tokens = normalizedMessage.Split(new[] { ' ', '\t', '\r', '\n', ',', '.', ';', ':', '!', '?', '-', '_' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var token in tokens)
        {
            foreach (var k in normalizedKeywords)
            {
                if (LevenshteinDistance(token, k) <= maxDistance)
                    return true;
            }
        }

        // Whole-message fuzzy match (for very short inputs)
        if (normalizedMessage.Length <= 10)
        {
            foreach (var k in normalizedKeywords)
            {
                if (LevenshteinDistance(normalizedMessage, k) <= maxDistance)
                    return true;
            }
        }

        return false;
    }

    private static int LevenshteinDistance(string s, string t)
    {
        if (string.IsNullOrEmpty(s)) return t.Length;
        if (string.IsNullOrEmpty(t)) return s.Length;

        var n = s.Length;
        var m = t.Length;
        var d = new int[n + 1, m + 1];

        for (int i = 0; i <= n; i++) d[i, 0] = i;
        for (int j = 0; j <= m; j++) d[0, j] = j;

        for (int i = 1; i <= n; i++)
        {
            for (int j = 1; j <= m; j++)
            {
                var cost = s[i - 1] == t[j - 1] ? 0 : 1;
                d[i, j] = Math.Min(
                    Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                    d[i - 1, j - 1] + cost);
            }
        }

        return d[n, m];
    }
}

