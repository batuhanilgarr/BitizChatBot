using BitizChatBot.Data;
using BitizChatBot.Models;
using Microsoft.EntityFrameworkCore;

namespace BitizChatBot.Services;

public interface IAnalyticsService
{
    Task<ConversationStats> GetConversationStatsAsync(DateTime? startDate = null, DateTime? endDate = null, string? domain = null);
    Task<List<PopularQuestion>> GetPopularQuestionsAsync(int topN = 10, DateTime? startDate = null, DateTime? endDate = null);
    Task<List<DailyStats>> GetDailyStatsAsync(int days = 30, string? domain = null);
    Task<List<HourlyStats>> GetHourlyStatsAsync(DateTime? date = null, string? domain = null);
    Task<DomainStats> GetDomainStatsAsync(string domain, DateTime? startDate = null, DateTime? endDate = null);
    Task<int> GetActiveUsersCountAsync(int minutesThreshold = 10, string? domain = null);
}

public class AnalyticsService : IAnalyticsService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<AnalyticsService> _logger;

    public AnalyticsService(ApplicationDbContext context, ILogger<AnalyticsService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<ConversationStats> GetConversationStatsAsync(DateTime? startDate = null, DateTime? endDate = null, string? domain = null)
    {
        try
        {
            startDate ??= DateTime.UtcNow.AddDays(-30);
            endDate ??= DateTime.UtcNow;

            var query = _context.ChatMessages
                .Where(m => m.Timestamp >= startDate && m.Timestamp <= endDate);

            if (!string.IsNullOrEmpty(domain))
            {
                query = query.Where(m => m.Session != null && m.Session.Domain == domain);
            }

            var messages = await query.ToListAsync();
            var sessions = await _context.ChatSessions
                .Where(s => s.CreatedAt >= startDate && s.CreatedAt <= endDate)
                .ToListAsync();

            if (!string.IsNullOrEmpty(domain))
            {
                sessions = sessions.Where(s => s.Domain == domain).ToList();
            }

            var userMessages = messages.Where(m => m.IsUser).ToList();
            var botMessages = messages.Where(m => !m.IsUser).ToList();

            return new ConversationStats
            {
                TotalConversations = sessions.Count,
                TotalMessages = messages.Count,
                UserMessages = userMessages.Count,
                BotMessages = botMessages.Count,
                AverageMessagesPerConversation = sessions.Count > 0 ? (double)messages.Count / sessions.Count : 0,
                UniqueUsers = sessions.Select(s => s.IpAddress).Distinct().Count(),
                StartDate = startDate.Value,
                EndDate = endDate.Value
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting conversation stats");
            return new ConversationStats();
        }
    }

    public async Task<List<PopularQuestion>> GetPopularQuestionsAsync(int topN = 10, DateTime? startDate = null, DateTime? endDate = null)
    {
        try
        {
            startDate ??= DateTime.UtcNow.AddDays(-30);
            endDate ??= DateTime.UtcNow;

            var userMessages = await _context.ChatMessages
                .Where(m => m.IsUser && 
                           m.Timestamp >= startDate && 
                           m.Timestamp <= endDate &&
                           !string.IsNullOrEmpty(m.Content))
                .ToListAsync();

            // Mesajları normalize et ve grupla
            var messageGroups = userMessages
                .Select(m => new
                {
                    Original = m.Content,
                    Normalized = NormalizeQuestion(m.Content),
                    Timestamp = m.Timestamp
                })
                .Where(x => !string.IsNullOrWhiteSpace(x.Normalized))
                .GroupBy(x => x.Normalized)
                .Select(g => new PopularQuestion
                {
                    Question = g.First().Original,
                    Count = g.Count(),
                    LastAsked = g.Max(x => x.Timestamp)
                })
                .OrderByDescending(q => q.Count)
                .Take(topN)
                .ToList();

            return messageGroups;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting popular questions");
            return new List<PopularQuestion>();
        }
    }

    public async Task<List<DailyStats>> GetDailyStatsAsync(int days = 30, string? domain = null)
    {
        try
        {
            var startDate = DateTime.UtcNow.AddDays(-days);
            var endDate = DateTime.UtcNow;

            var query = _context.ChatSessions
                .Where(s => s.CreatedAt >= startDate && s.CreatedAt <= endDate);

            if (!string.IsNullOrEmpty(domain))
            {
                query = query.Where(s => s.Domain == domain);
            }

            var sessions = await query.ToListAsync();

            var dailyStats = sessions
                .GroupBy(s => s.CreatedAt.Date)
                .Select(g => new DailyStats
                {
                    Date = g.Key,
                    ConversationCount = g.Count(),
                    MessageCount = 0 // Will be calculated separately if needed
                })
                .OrderBy(s => s.Date)
                .ToList();

            // Fill missing dates with 0
            for (var date = startDate.Date; date <= endDate.Date; date = date.AddDays(1))
            {
                if (!dailyStats.Any(s => s.Date == date))
                {
                    dailyStats.Add(new DailyStats { Date = date, ConversationCount = 0, MessageCount = 0 });
                }
            }

            return dailyStats.OrderBy(s => s.Date).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting daily stats");
            return new List<DailyStats>();
        }
    }

    public async Task<List<HourlyStats>> GetHourlyStatsAsync(DateTime? date = null, string? domain = null)
    {
        try
        {
            date ??= DateTime.UtcNow.Date;
            var startDate = date.Value;
            var endDate = date.Value.AddDays(1);

            var query = _context.ChatSessions
                .Where(s => s.CreatedAt >= startDate && s.CreatedAt < endDate);

            if (!string.IsNullOrEmpty(domain))
            {
                query = query.Where(s => s.Domain == domain);
            }

            var sessions = await query.ToListAsync();

            var hourlyStats = sessions
                .GroupBy(s => s.CreatedAt.Hour)
                .Select(g => new HourlyStats
                {
                    Hour = g.Key,
                    ConversationCount = g.Count()
                })
                .OrderBy(s => s.Hour)
                .ToList();

            // Fill missing hours with 0
            for (int hour = 0; hour < 24; hour++)
            {
                if (!hourlyStats.Any(s => s.Hour == hour))
                {
                    hourlyStats.Add(new HourlyStats { Hour = hour, ConversationCount = 0 });
                }
            }

            return hourlyStats.OrderBy(s => s.Hour).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting hourly stats");
            return new List<HourlyStats>();
        }
    }

    public async Task<DomainStats> GetDomainStatsAsync(string domain, DateTime? startDate = null, DateTime? endDate = null)
    {
        try
        {
            startDate ??= DateTime.UtcNow.AddDays(-30);
            endDate ??= DateTime.UtcNow;

            var sessions = await _context.ChatSessions
                .Where(s => s.Domain == domain && s.CreatedAt >= startDate && s.CreatedAt <= endDate)
                .ToListAsync();

            var messages = await _context.ChatMessages
                .Where(m => m.Session != null && m.Session.Domain == domain && 
                           m.Timestamp >= startDate && m.Timestamp <= endDate)
                .ToListAsync();

            return new DomainStats
            {
                Domain = domain,
                TotalConversations = sessions.Count,
                TotalMessages = messages.Count,
                UserMessages = messages.Count(m => m.IsUser),
                BotMessages = messages.Count(m => !m.IsUser),
                AverageMessagesPerConversation = sessions.Count > 0 ? (double)messages.Count / sessions.Count : 0
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting domain stats");
            return new DomainStats { Domain = domain };
        }
    }

    public async Task<int> GetActiveUsersCountAsync(int minutesThreshold = 10, string? domain = null)
    {
        try
        {
            var thresholdTime = DateTime.UtcNow.AddMinutes(-minutesThreshold);
            
            var query = _context.ChatSessions
                .Where(s => s.IsActive && 
                           ((s.LastActivityAt.HasValue && s.LastActivityAt >= thresholdTime) ||
                            (!s.LastActivityAt.HasValue && s.CreatedAt >= thresholdTime)));

            if (!string.IsNullOrEmpty(domain))
            {
                query = query.Where(s => s.Domain == domain);
            }

            var activeUsersCount = await query.CountAsync();
            return activeUsersCount;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting active users count");
            return 0;
        }
    }

    private string NormalizeQuestion(string question)
    {
        if (string.IsNullOrWhiteSpace(question))
            return string.Empty;

        // Küçük harfe çevir, fazla boşlukları temizle
        var normalized = question.ToLowerInvariant().Trim();
        normalized = System.Text.RegularExpressions.Regex.Replace(normalized, @"\s+", " ");
        
        // Çok kısa soruları filtrele
        if (normalized.Length < 3)
            return string.Empty;

        return normalized;
    }
}

// Models
public class ConversationStats
{
    public int TotalConversations { get; set; }
    public int TotalMessages { get; set; }
    public int UserMessages { get; set; }
    public int BotMessages { get; set; }
    public double AverageMessagesPerConversation { get; set; }
    public int UniqueUsers { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
}

public class PopularQuestion
{
    public string Question { get; set; } = string.Empty;
    public int Count { get; set; }
    public DateTime LastAsked { get; set; }
}

public class DailyStats
{
    public DateTime Date { get; set; }
    public int ConversationCount { get; set; }
    public int MessageCount { get; set; }
}

public class HourlyStats
{
    public int Hour { get; set; }
    public int ConversationCount { get; set; }
}

public class DomainStats
{
    public string Domain { get; set; } = string.Empty;
    public int TotalConversations { get; set; }
    public int TotalMessages { get; set; }
    public int UserMessages { get; set; }
    public int BotMessages { get; set; }
    public double AverageMessagesPerConversation { get; set; }
}

