using BitizChatBot.Models;
using BitizChatBot.Services;
using Microsoft.EntityFrameworkCore;

namespace BitizChatBot.Data;

public static class DbInitializer
{
    public static async Task InitializeAsync(ApplicationDbContext context, IUserService userService, ILogger logger)
    {
        try
        {
            // Migration'ları uygula (database'i oluştur ve güncelle)
            await context.Database.MigrateAsync();

            // İlk admin kullanıcısı var mı kontrol et
            var hasAdmin = await context.Users.AnyAsync(u => u.IsAdmin && u.IsActive);
            
            if (!hasAdmin)
            {
                // Varsayılan admin kullanıcısı oluştur
                var (success, error) = await userService.CreateUserAsync(
                    username: "admin",
                    password: "Halic.1903",
                    isAdmin: true,
                    email: "admin@example.com",
                    fullName: "System Administrator"
                );

                if (success)
                {
                    logger.LogInformation("Default admin user created: admin / admin123!");
                }
                else
                {
                    logger.LogWarning("Failed to create default admin user: {Error}", error);
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error initializing database");
        }
    }
}

