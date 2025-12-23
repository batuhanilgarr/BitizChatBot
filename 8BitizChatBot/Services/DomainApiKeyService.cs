using System.Security.Cryptography;
using BitizChatBot.Models;
using BitizChatBot.Data;
using Microsoft.EntityFrameworkCore;

namespace BitizChatBot.Services;

public class DomainApiKeyService : IDomainApiKeyService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DomainApiKeyService> _logger;

    public DomainApiKeyService(IServiceScopeFactory scopeFactory, ILogger<DomainApiKeyService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task<IReadOnlyList<DomainApiKey>> GetAllAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var entities = await context.DomainApiKeys
            .OrderByDescending(k => k.CreatedAt)
            .ToListAsync();

        return entities.Select(MapToDomainApiKey).ToList();
    }

    public async Task<DomainApiKey> CreateAsync(string domain)
    {
        if (string.IsNullOrWhiteSpace(domain))
            throw new ArgumentException("Domain is required", nameof(domain));

        var normalizedDomain = NormalizeDomain(domain);

        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // Check if domain already exists
        var existing = await context.DomainApiKeys
            .FirstOrDefaultAsync(k => k.Domain == normalizedDomain);

        if (existing != null)
            throw new InvalidOperationException($"Domain '{normalizedDomain}' already exists");

        var entity = new DomainApiKeyEntity
        {
            Id = Guid.NewGuid().ToString(),
            Domain = normalizedDomain,
            ApiKey = GenerateApiKey(),
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        context.DomainApiKeys.Add(entity);
        await context.SaveChangesAsync();

        return MapToDomainApiKey(entity);
    }

    public async Task DeleteAsync(string id)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var entity = await context.DomainApiKeys.FindAsync(id);
        if (entity != null)
        {
            context.DomainApiKeys.Remove(entity);
            await context.SaveChangesAsync();
        }
    }

    public async Task<bool> ValidateAsync(string domain, string apiKey)
    {
        if (string.IsNullOrWhiteSpace(domain) || string.IsNullOrWhiteSpace(apiKey))
            return false;

        var normalizedDomain = NormalizeDomain(domain);

        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        
        return await context.DomainApiKeys
            .AnyAsync(k =>
                k.IsActive &&
                k.Domain == normalizedDomain &&
                k.ApiKey == apiKey);
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

    private static string GenerateApiKey()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static DomainApiKey MapToDomainApiKey(DomainApiKeyEntity entity)
    {
        return new DomainApiKey
        {
            Id = entity.Id,
            Domain = entity.Domain,
            ApiKey = entity.ApiKey,
            IsActive = entity.IsActive,
            CreatedAt = entity.CreatedAt
        };
    }
}


