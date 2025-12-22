using System.Text.Json;
using BitizChatBot.Models;
using BitizChatBot.Data;
using Microsoft.EntityFrameworkCore;

namespace BitizChatBot.Services;

public class DomainAppearanceService : IDomainAppearanceService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<DomainAppearanceService> _logger;

    public DomainAppearanceService(ApplicationDbContext context, ILogger<DomainAppearanceService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<DomainAppearance?> GetAsync(string domain)
    {
        var normalized = NormalizeDomain(domain);
        var entity = await _context.DomainAppearances
            .FirstOrDefaultAsync(d => d.Domain == normalized);

        return entity != null ? MapToDomainAppearance(entity) : null;
    }

    public async Task<List<DomainAppearance>> GetAllAsync()
    {
        var entities = await _context.DomainAppearances
            .OrderBy(d => d.Domain)
            .ToListAsync();

        return entities.Select(MapToDomainAppearance).ToList();
    }

    public async Task SaveAsync(DomainAppearance appearance)
    {
        if (appearance == null) throw new ArgumentNullException(nameof(appearance));

        appearance.Domain = NormalizeDomain(appearance.Domain);
        var entity = await _context.DomainAppearances
            .FirstOrDefaultAsync(d => d.Domain == appearance.Domain);

        if (entity == null)
        {
            entity = MapToEntity(appearance);
            _context.DomainAppearances.Add(entity);
        }
        else
        {
            UpdateEntity(entity, appearance);
            _context.DomainAppearances.Update(entity);
        }

        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(string domain)
    {
        var normalized = NormalizeDomain(domain);
        var entity = await _context.DomainAppearances
            .FirstOrDefaultAsync(d => d.Domain == normalized);

        if (entity != null)
        {
            _context.DomainAppearances.Remove(entity);
            await _context.SaveChangesAsync();
        }
    }

    private static string NormalizeDomain(string domain)
    {
        domain = domain.Trim();
        if (domain.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
            domain = domain.Substring("http://".Length);
        if (domain.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            domain = domain.Substring("https://".Length);
        if (domain.StartsWith("www.", StringComparison.OrdinalIgnoreCase))
            domain = domain.Substring("www.".Length);

        var slashIndex = domain.IndexOf('/');
        if (slashIndex >= 0)
            domain = domain.Substring(0, slashIndex);

        return domain.ToLowerInvariant();
    }

    private static DomainAppearance MapToDomainAppearance(DomainAppearanceEntity entity)
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

        return new DomainAppearance
        {
            Domain = entity.Domain,
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
            GoodbyeResponse = entity.GoodbyeResponse ?? string.Empty
        };
    }

    private static DomainAppearanceEntity MapToEntity(DomainAppearance appearance)
    {
        return new DomainAppearanceEntity
        {
            Domain = appearance.Domain,
            ChatbotName = appearance.ChatbotName,
            ChatbotLogoUrl = appearance.ChatbotLogoUrl,
            PrimaryColor = appearance.PrimaryColor,
            SecondaryColor = appearance.SecondaryColor,
            WelcomeMessage = appearance.WelcomeMessage,
            ChatbotOnline = appearance.ChatbotOnline,
            OpenChatOnLoad = appearance.OpenChatOnLoad,
            QuickRepliesJson = JsonSerializer.Serialize(appearance.QuickReplies ?? new List<string>()),
            GreetingResponse = appearance.GreetingResponse,
            HowAreYouResponse = appearance.HowAreYouResponse,
            WhoAreYouResponse = appearance.WhoAreYouResponse,
            WhatCanYouDoResponse = appearance.WhatCanYouDoResponse,
            ThanksResponse = appearance.ThanksResponse,
            GoodbyeResponse = appearance.GoodbyeResponse,
            UpdatedAt = DateTime.UtcNow
        };
    }

    private static void UpdateEntity(DomainAppearanceEntity entity, DomainAppearance appearance)
    {
        entity.ChatbotName = appearance.ChatbotName;
        entity.ChatbotLogoUrl = appearance.ChatbotLogoUrl;
        entity.PrimaryColor = appearance.PrimaryColor;
        entity.SecondaryColor = appearance.SecondaryColor;
        entity.WelcomeMessage = appearance.WelcomeMessage;
        entity.ChatbotOnline = appearance.ChatbotOnline;
        entity.OpenChatOnLoad = appearance.OpenChatOnLoad;
        entity.QuickRepliesJson = JsonSerializer.Serialize(appearance.QuickReplies ?? new List<string>());
        entity.GreetingResponse = appearance.GreetingResponse;
        entity.HowAreYouResponse = appearance.HowAreYouResponse;
        entity.WhoAreYouResponse = appearance.WhoAreYouResponse;
        entity.WhatCanYouDoResponse = appearance.WhatCanYouDoResponse;
        entity.ThanksResponse = appearance.ThanksResponse;
        entity.GoodbyeResponse = appearance.GoodbyeResponse;
        entity.UpdatedAt = DateTime.UtcNow;
    }
}


