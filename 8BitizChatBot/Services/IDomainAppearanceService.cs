using BitizChatBot.Models;

namespace BitizChatBot.Services;

public interface IDomainAppearanceService
{
    Task<DomainAppearance?> GetAsync(string domain);
    Task<List<DomainAppearance>> GetAllAsync();
    Task SaveAsync(DomainAppearance appearance);
    Task DeleteAsync(string domain);
}
