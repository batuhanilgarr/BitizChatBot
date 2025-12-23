using BitizChatBot.Data;
using BitizChatBot.Models;
using Microsoft.EntityFrameworkCore;
using BCrypt.Net;

namespace BitizChatBot.Services;

public interface IUserService
{
    Task<(UserEntity? User, string? Error)> AuthenticateAsync(string username, string password);
    Task<UserEntity?> GetByIdAsync(int id);
    Task<UserEntity?> GetByUsernameAsync(string username);
    Task<(bool Success, string? Error)> CreateUserAsync(string username, string password, bool isAdmin = true, string? email = null, string? fullName = null);
    Task<bool> UpdateUserAsync(int id, string? password = null, bool? isAdmin = null, string? email = null, string? fullName = null, bool? isActive = null);
    Task<bool> DeleteUserAsync(int id);
    Task<List<UserEntity>> GetAllUsersAsync();
    Task UpdateLastLoginAsync(int userId);
    Task<bool> UnlockUserAsync(int id);
    bool ValidatePasswordPolicy(string password, out string? error);
}

public class UserService : IUserService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<UserService> _logger;
    private readonly IAuditLogService? _auditLogService;

    // Account lockout settings
    private const int MaxFailedLoginAttempts = 5;
    private const int LockoutDurationMinutes = 30;

    public UserService(
        ApplicationDbContext context, 
        ILogger<UserService> logger,
        IAuditLogService? auditLogService = null)
    {
        _context = context;
        _logger = logger;
        _auditLogService = auditLogService;
    }

    public async Task<(UserEntity? User, string? Error)> AuthenticateAsync(string username, string password)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            return (null, "Kullanıcı adı ve şifre gereklidir.");

        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Username.ToLower() == username.ToLower());

        if (user == null)
        {
            try
            {
                await _auditLogService?.LogAsync("LoginFailed", null, username, null, null, "User not found", null, null, false, "User not found")!;
            }
            catch
            {
                // AuditLogs tablosu yoksa sessizce devam et
            }
            return (null, "Kullanıcı adı veya şifre hatalı.");
        }

        // Account locked kontrolü
        if (user.LockedUntil.HasValue && user.LockedUntil.Value > DateTime.UtcNow)
        {
            var remainingMinutes = (int)Math.Ceiling((user.LockedUntil.Value - DateTime.UtcNow).TotalMinutes);
            try
            {
                await _auditLogService?.LogAsync("LoginFailed", user.Id.ToString(), username, "User", user.Id.ToString(), $"Account locked. Remaining: {remainingMinutes} minutes", null, null, false, "Account locked")!;
            }
            catch { }
            return (null, $"Hesap kilitli. {remainingMinutes} dakika sonra tekrar deneyin.");
        }

        // Lockout süresi dolmuşsa sıfırla
        if (user.LockedUntil.HasValue && user.LockedUntil.Value <= DateTime.UtcNow)
        {
            user.LockedUntil = null;
            user.FailedLoginAttempts = 0;
        }

        // Account active kontrolü
        if (!user.IsActive)
        {
            try
            {
                await _auditLogService?.LogAsync("LoginFailed", user.Id.ToString(), username, "User", user.Id.ToString(), "Account inactive", null, null, false, "Account inactive")!;
            }
            catch { }
            return (null, "Hesap aktif değil.");
        }

        // Admin kontrolü
        if (!user.IsAdmin)
        {
            try
            {
                await _auditLogService?.LogAsync("LoginFailed", user.Id.ToString(), username, "User", user.Id.ToString(), "Not admin", null, null, false, "Not admin")!;
            }
            catch { }
            return (null, "Admin yetkiniz bulunmuyor.");
        }

        // Password hash kontrolü
        if (!BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
        {
            user.FailedLoginAttempts++;
            
            // Max attempts'a ulaşıldıysa kilitle
            if (user.FailedLoginAttempts >= MaxFailedLoginAttempts)
            {
                user.LockedUntil = DateTime.UtcNow.AddMinutes(LockoutDurationMinutes);
                try
                {
                    await _auditLogService?.LogAsync("AccountLocked", user.Id.ToString(), username, "User", user.Id.ToString(), $"Failed attempts: {user.FailedLoginAttempts}", null, null, false, "Account locked due to failed attempts")!;
                }
                catch { }
            }
            else
            {
                try
                {
                    await _auditLogService?.LogAsync("LoginFailed", user.Id.ToString(), username, "User", user.Id.ToString(), $"Failed attempts: {user.FailedLoginAttempts}/{MaxFailedLoginAttempts}", null, null, false, "Invalid password")!;
                }
                catch { }
            }

            _context.Users.Update(user);
            await _context.SaveChangesAsync();

            var remainingAttempts = MaxFailedLoginAttempts - user.FailedLoginAttempts;
            if (remainingAttempts > 0)
            {
                return (null, $"Kullanıcı adı veya şifre hatalı. Kalan deneme hakkı: {remainingAttempts}");
            }
            else
            {
                return (null, $"Çok fazla başarısız giriş denemesi. Hesap {LockoutDurationMinutes} dakika kilitlendi.");
            }
        }

        // Başarılı giriş - failed attempts'ı sıfırla
        user.FailedLoginAttempts = 0;
        user.LockedUntil = null;
        user.LastLoginAt = DateTime.UtcNow;
        _context.Users.Update(user);
        await _context.SaveChangesAsync();

        try
        {
            await _auditLogService?.LogAsync("LoginSuccess", user.Id.ToString(), username, "User", user.Id.ToString(), "Successful login", null, null, true, null)!;
        }
        catch { }

        return (user, null);
    }

    public async Task<UserEntity?> GetByIdAsync(int id)
    {
        return await _context.Users.FindAsync(id);
    }

    public async Task<UserEntity?> GetByUsernameAsync(string username)
    {
        return await _context.Users
            .FirstOrDefaultAsync(u => u.Username.ToLower() == username.ToLower());
    }

    public bool ValidatePasswordPolicy(string password, out string? error)
    {
        error = null;

        if (string.IsNullOrWhiteSpace(password))
        {
            error = "Şifre boş olamaz.";
            return false;
        }

        if (password.Length < 8)
        {
            error = "Şifre en az 8 karakter olmalıdır.";
            return false;
        }

        if (password.Length > 128)
        {
            error = "Şifre en fazla 128 karakter olabilir.";
            return false;
        }

        // En az bir büyük harf, bir küçük harf, bir rakam kontrolü
        if (!System.Text.RegularExpressions.Regex.IsMatch(password, @"[A-Z]"))
        {
            error = "Şifre en az bir büyük harf içermelidir.";
            return false;
        }

        if (!System.Text.RegularExpressions.Regex.IsMatch(password, @"[a-z]"))
        {
            error = "Şifre en az bir küçük harf içermelidir.";
            return false;
        }

        if (!System.Text.RegularExpressions.Regex.IsMatch(password, @"[0-9]"))
        {
            error = "Şifre en az bir rakam içermelidir.";
            return false;
        }

        return true;
    }

    public async Task<(bool Success, string? Error)> CreateUserAsync(string username, string password, bool isAdmin = true, string? email = null, string? fullName = null)
    {
        try
        {
            // Username kontrolü
            var existingUser = await GetByUsernameAsync(username);
            if (existingUser != null)
            {
                _logger.LogWarning("Username already exists: {Username}", username);
                return (false, "Bu kullanıcı adı zaten kullanılıyor.");
            }

            // Password policy kontrolü
            if (!ValidatePasswordPolicy(password, out var passwordError))
            {
                return (false, passwordError);
            }

            // Password hash
            var passwordHash = BCrypt.Net.BCrypt.HashPassword(password);

            var user = new UserEntity
            {
                Username = username,
                PasswordHash = passwordHash,
                IsAdmin = isAdmin,
                Email = email,
                FullName = fullName,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            _logger.LogInformation("User created: {Username}", username);
            
            // Audit log (tablo yoksa sessizce başarısız olur)
            try
            {
                await _auditLogService?.LogAsync("CreateUser", null, null, "User", user.Id.ToString(), $"Created user: {username}, Admin: {isAdmin}", null, null, true, null)!;
            }
            catch
            {
                // AuditLogs tablosu yoksa sessizce devam et
            }
            
            return (true, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating user: {Username}", username);
            return (false, $"Kullanıcı oluşturulurken hata oluştu: {ex.Message}");
        }
    }

    public async Task<bool> UnlockUserAsync(int id)
    {
        try
        {
            var user = await GetByIdAsync(id);
            if (user == null)
                return false;

            user.LockedUntil = null;
            user.FailedLoginAttempts = 0;
            _context.Users.Update(user);
            await _context.SaveChangesAsync();

            try
            {
                await _auditLogService?.LogAsync("UnlockUser", null, null, "User", id.ToString(), $"Unlocked user: {user.Username}", null, null, true, null)!;
            }
            catch { }
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error unlocking user: {UserId}", id);
            return false;
        }
    }

    public async Task<bool> UpdateUserAsync(int id, string? password = null, bool? isAdmin = null, string? email = null, string? fullName = null, bool? isActive = null)
    {
        try
        {
            var user = await GetByIdAsync(id);
            if (user == null)
                return false;

            if (!string.IsNullOrWhiteSpace(password))
            {
                // Password policy kontrolü
                if (!ValidatePasswordPolicy(password, out var passwordError))
                {
                    _logger.LogWarning("Password policy validation failed: {Error}", passwordError);
                    return false;
                }

                user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(password);
                user.PasswordChangedAt = DateTime.UtcNow;
            }

            if (isAdmin.HasValue)
                user.IsAdmin = isAdmin.Value;

            if (email != null)
                user.Email = email;

            if (fullName != null)
                user.FullName = fullName;

            if (isActive.HasValue)
                user.IsActive = isActive.Value;

            _context.Users.Update(user);
            await _context.SaveChangesAsync();

            _logger.LogInformation("User updated: {UserId}", id);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating user: {UserId}", id);
            return false;
        }
    }

    public async Task<bool> DeleteUserAsync(int id)
    {
        try
        {
            var user = await GetByIdAsync(id);
            if (user == null)
                return false;

            _context.Users.Remove(user);
            await _context.SaveChangesAsync();

            _logger.LogInformation("User deleted: {UserId}", id);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting user: {UserId}", id);
            return false;
        }
    }

    public async Task<List<UserEntity>> GetAllUsersAsync()
    {
        return await _context.Users
            .OrderBy(u => u.Username)
            .ToListAsync();
    }

    public async Task UpdateLastLoginAsync(int userId)
    {
        try
        {
            var user = await GetByIdAsync(userId);
            if (user != null)
            {
                user.LastLoginAt = DateTime.UtcNow;
                _context.Users.Update(user);
                await _context.SaveChangesAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating last login: {UserId}", userId);
        }
    }
}

