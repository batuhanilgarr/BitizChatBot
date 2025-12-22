using BitizChatBot.Models;

namespace BitizChatBot.Services;

public interface IDomainApiKeyService
{
    Task<IReadOnlyList<DomainApiKey>> GetAllAsync();
    Task<DomainApiKey> CreateAsync(string domain);
    Task DeleteAsync(string id);
    Task<bool> ValidateAsync(string domain, string apiKey);
}


