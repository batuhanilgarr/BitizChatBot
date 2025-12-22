using BitizChatBot.Models;
using BitizChatBot.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.Text.Json;

namespace BitizChatBot.Services;

public class AdminSettingsService : IAdminSettingsService
{
    private readonly ApplicationDbContext _context;
    private readonly IConfiguration _configuration;
    private AdminSettings? _cachedSettings;

    public AdminSettingsService(ApplicationDbContext context, IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;
    }

    public async Task<AdminSettings> GetSettingsAsync()
    {
        if (_cachedSettings != null)
            return _cachedSettings;

        var entity = await _context.AdminSettings.FindAsync(1);
        
        if (entity != null)
        {
            _cachedSettings = MapToAdminSettings(entity);
            return _cachedSettings;
        }

        // Create default settings if not exists
        var defaultSettings = new AdminSettings
        {
            Id = 1,
            LlmProvider = _configuration["LlmSettings:Provider"] ?? "Ollama",
            ModelName = _configuration["LlmSettings:ModelName"] ?? "hermes3:8b",
            ApiKey = _configuration["LlmSettings:ApiKey"] ?? string.Empty,
            OllamaBaseUrl = _configuration["LlmSettings:OllamaBaseUrl"] ?? "http://3.14.208.235:11434",
            SystemPrompt = _configuration["LlmSettings:SystemPrompt"] ??
                           "Sen Bridgestone lastikleri ve bayi konumları için yardımcı bir asistansın. Kullanıcılara lastik ve bayi bilgisi sağla. Asla <think> etiketi veya herhangi bir içsel düşünce gösterme. Sadece son cevabı temiz ve Türkçe ver. Sadece Bridgestone lastikleri ve bayi konumları hakkında soruları cevapla. Başka bir konuda soru gelirse, sadece şu cevabı ver: \"Üzgünüm, sadece Bridgestone lastikleri ve bayi konumları hakkında sorulara cevap verebilirim. Size lastik önerileri konusunda yardımcı olabilirim.\"",
            Temperature = double.TryParse(_configuration["LlmSettings:Temperature"], out var temp) ? temp : 0.7,
            MaxTokens = int.TryParse(_configuration["LlmSettings:MaxTokens"], out var tokens) ? tokens : 2000,
            UpdatedAt = DateTime.UtcNow
        };

        await SaveSettingsAsync(defaultSettings);
        return defaultSettings;
    }

    public async Task SaveSettingsAsync(AdminSettings settings)
    {
        settings.UpdatedAt = DateTime.UtcNow;
        
        var entity = await _context.AdminSettings.FindAsync(1);
        
        if (entity == null)
        {
            entity = MapToEntity(settings);
            _context.AdminSettings.Add(entity);
        }
        else
        {
            UpdateEntity(entity, settings);
            _context.AdminSettings.Update(entity);
        }

        await _context.SaveChangesAsync();
        
        // Update cache
        _cachedSettings = settings;
    }

    private AdminSettings MapToAdminSettings(AdminSettingsEntity entity)
    {
        var quickReplies = new List<string>();
        if (!string.IsNullOrEmpty(entity.QuickRepliesJson))
        {
            try
            {
                quickReplies = JsonSerializer.Deserialize<List<string>>(entity.QuickRepliesJson) ?? new List<string>();
            }
            catch
            {
                quickReplies = new List<string>();
            }
        }

        return new AdminSettings
        {
            Id = entity.Id,
            LlmProvider = entity.LlmProvider,
            ModelName = entity.ModelName,
            ApiKey = entity.ApiKey ?? string.Empty,
            OllamaBaseUrl = entity.OllamaBaseUrl,
            SystemPrompt = entity.SystemPrompt,
            Temperature = entity.Temperature,
            MaxTokens = entity.MaxTokens,
            ChatbotName = entity.ChatbotName,
            ChatbotLogoUrl = entity.ChatbotLogoUrl ?? string.Empty,
            PrimaryColor = entity.PrimaryColor,
            SecondaryColor = entity.SecondaryColor,
            WelcomeMessage = entity.WelcomeMessage,
            ChatbotOnline = entity.ChatbotOnline,
            OpenChatOnLoad = entity.OpenChatOnLoad,
            QuickReplies = quickReplies,
            GreetingResponse = entity.GreetingResponse ?? string.Empty,
            HowAreYouResponse = entity.HowAreYouResponse ?? string.Empty,
            WhoAreYouResponse = entity.WhoAreYouResponse ?? string.Empty,
            WhatCanYouDoResponse = entity.WhatCanYouDoResponse ?? string.Empty,
            ThanksResponse = entity.ThanksResponse ?? string.Empty,
            GoodbyeResponse = entity.GoodbyeResponse ?? string.Empty,
            UpdatedAt = entity.UpdatedAt
        };
    }

    private AdminSettingsEntity MapToEntity(AdminSettings settings)
    {
        return new AdminSettingsEntity
        {
            Id = settings.Id,
            LlmProvider = settings.LlmProvider,
            ModelName = settings.ModelName,
            ApiKey = settings.ApiKey,
            OllamaBaseUrl = settings.OllamaBaseUrl,
            SystemPrompt = settings.SystemPrompt,
            Temperature = settings.Temperature,
            MaxTokens = settings.MaxTokens,
            ChatbotName = settings.ChatbotName,
            ChatbotLogoUrl = settings.ChatbotLogoUrl,
            PrimaryColor = settings.PrimaryColor,
            SecondaryColor = settings.SecondaryColor,
            WelcomeMessage = settings.WelcomeMessage,
            ChatbotOnline = settings.ChatbotOnline,
            OpenChatOnLoad = settings.OpenChatOnLoad,
            QuickRepliesJson = JsonSerializer.Serialize(settings.QuickReplies ?? new List<string>()),
            GreetingResponse = settings.GreetingResponse,
            HowAreYouResponse = settings.HowAreYouResponse,
            WhoAreYouResponse = settings.WhoAreYouResponse,
            WhatCanYouDoResponse = settings.WhatCanYouDoResponse,
            ThanksResponse = settings.ThanksResponse,
            GoodbyeResponse = settings.GoodbyeResponse,
            UpdatedAt = settings.UpdatedAt
        };
    }

    private void UpdateEntity(AdminSettingsEntity entity, AdminSettings settings)
    {
        entity.LlmProvider = settings.LlmProvider;
        entity.ModelName = settings.ModelName;
        entity.ApiKey = settings.ApiKey;
        entity.OllamaBaseUrl = settings.OllamaBaseUrl;
        entity.SystemPrompt = settings.SystemPrompt;
        entity.Temperature = settings.Temperature;
        entity.MaxTokens = settings.MaxTokens;
        entity.ChatbotName = settings.ChatbotName;
        entity.ChatbotLogoUrl = settings.ChatbotLogoUrl;
        entity.PrimaryColor = settings.PrimaryColor;
        entity.SecondaryColor = settings.SecondaryColor;
        entity.WelcomeMessage = settings.WelcomeMessage;
        entity.ChatbotOnline = settings.ChatbotOnline;
        entity.OpenChatOnLoad = settings.OpenChatOnLoad;
        entity.QuickRepliesJson = JsonSerializer.Serialize(settings.QuickReplies ?? new List<string>());
        entity.GreetingResponse = settings.GreetingResponse;
        entity.HowAreYouResponse = settings.HowAreYouResponse;
        entity.WhoAreYouResponse = settings.WhoAreYouResponse;
        entity.WhatCanYouDoResponse = settings.WhatCanYouDoResponse;
        entity.ThanksResponse = settings.ThanksResponse;
        entity.GoodbyeResponse = settings.GoodbyeResponse;
        entity.UpdatedAt = settings.UpdatedAt;
    }
}

