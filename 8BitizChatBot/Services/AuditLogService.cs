using BitizChatBot.Data;
using BitizChatBot.Models;
using Microsoft.EntityFrameworkCore;

namespace BitizChatBot.Services;

public interface IAuditLogService
{
    Task LogAsync(string action, string? userId = null, string? username = null, string? entityType = null, string? entityId = null, string? details = null, string? ipAddress = null, string? userAgent = null, bool success = true, string? errorMessage = null);
    Task<List<AuditLogEntity>> GetLogsAsync(int page = 1, int pageSize = 50, string? action = null, string? userId = null, DateTime? startDate = null, DateTime? endDate = null);
    Task<int> GetLogCountAsync(string? action = null, string? userId = null, DateTime? startDate = null, DateTime? endDate = null);
}

public class AuditLogService : IAuditLogService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<AuditLogService> _logger;
    private readonly IHttpContextAccessor? _httpContextAccessor;

    public AuditLogService(
        ApplicationDbContext context, 
        ILogger<AuditLogService> logger,
        IHttpContextAccessor? httpContextAccessor = null)
    {
        _context = context;
        _logger = logger;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task LogAsync(
        string action, 
        string? userId = null, 
        string? username = null, 
        string? entityType = null, 
        string? entityId = null, 
        string? details = null, 
        string? ipAddress = null, 
        string? userAgent = null, 
        bool success = true, 
        string? errorMessage = null)
    {
        try
        {
            // IP ve UserAgent'ı otomatik al
            if (string.IsNullOrEmpty(ipAddress) && _httpContextAccessor?.HttpContext != null)
            {
                ipAddress = _httpContextAccessor.HttpContext.Connection.RemoteIpAddress?.ToString();
            }

            if (string.IsNullOrEmpty(userAgent) && _httpContextAccessor?.HttpContext != null)
            {
                userAgent = _httpContextAccessor.HttpContext.Request.Headers["User-Agent"].ToString();
                if (!string.IsNullOrEmpty(userAgent) && userAgent.Length > 500)
                {
                    userAgent = userAgent.Substring(0, 500);
                }
            }

            var log = new AuditLogEntity
            {
                UserId = userId,
                Username = username,
                Action = action,
                EntityType = entityType,
                EntityId = entityId,
                Details = details,
                IpAddress = ipAddress,
                UserAgent = userAgent,
                Success = success,
                ErrorMessage = errorMessage,
                Timestamp = DateTime.UtcNow
            };

            _context.AuditLogs.Add(log);
            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error logging audit event: {Action}", action);
        }
    }

    public async Task<List<AuditLogEntity>> GetLogsAsync(
        int page = 1, 
        int pageSize = 50, 
        string? action = null, 
        string? userId = null, 
        DateTime? startDate = null, 
        DateTime? endDate = null)
    {
        try
        {
            var query = _context.AuditLogs.AsQueryable();

        if (!string.IsNullOrEmpty(action))
            query = query.Where(l => l.Action == action);

        if (!string.IsNullOrEmpty(userId))
            query = query.Where(l => l.UserId == userId);

        if (startDate.HasValue)
            query = query.Where(l => l.Timestamp >= startDate.Value);

        if (endDate.HasValue)
            query = query.Where(l => l.Timestamp <= endDate.Value);

            return await query
                .OrderByDescending(l => l.Timestamp)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting audit logs. Table may not exist yet.");
            return new List<AuditLogEntity>();
        }
    }

    public async Task<int> GetLogCountAsync(
        string? action = null, 
        string? userId = null, 
        DateTime? startDate = null, 
        DateTime? endDate = null)
    {
        var query = _context.AuditLogs.AsQueryable();

        if (!string.IsNullOrEmpty(action))
            query = query.Where(l => l.Action == action);

        if (!string.IsNullOrEmpty(userId))
            query = query.Where(l => l.UserId == userId);

        if (startDate.HasValue)
            query = query.Where(l => l.Timestamp >= startDate.Value);

        if (endDate.HasValue)
            query = query.Where(l => l.Timestamp <= endDate.Value);

        return await query.CountAsync();
    }
}

