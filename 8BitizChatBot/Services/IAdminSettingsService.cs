using BitizChatBot.Models;

namespace BitizChatBot.Services;

public interface IAdminSettingsService
{
    Task<AdminSettings> GetSettingsAsync();
    Task SaveSettingsAsync(AdminSettings settings);
}

