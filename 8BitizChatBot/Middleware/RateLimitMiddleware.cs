using System.Collections.Concurrent;
using System.Net;

namespace BitizChatBot.Middleware;

public class RateLimitMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RateLimitMiddleware> _logger;
    private static readonly ConcurrentDictionary<string, RateLimitInfo> _rateLimitCache = new();

    // Rate limit ayarları
    private const int MaxRequestsPerMinute = 30;
    private const int MaxRequestsPerHour = 200;
    private readonly TimeSpan _windowSize = TimeSpan.FromMinutes(1);

    public RateLimitMiddleware(RequestDelegate next, ILogger<RateLimitMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Admin endpoint'lerini bypass et
        if (context.Request.Path.StartsWithSegments("/admin") || 
            context.Request.Path.StartsWithSegments("/api/admin"))
        {
            await _next(context);
            return;
        }

        // Sadece chat endpoint'lerini rate limit'le
        if (!context.Request.Path.StartsWithSegments("/api/chat"))
        {
            await _next(context);
            return;
        }

        var clientId = GetClientIdentifier(context);
        var rateLimitInfo = _rateLimitCache.GetOrAdd(clientId, _ => new RateLimitInfo());

        bool shouldBlock = false;
        string? errorMessage = null;

        lock (rateLimitInfo)
        {
            var now = DateTime.UtcNow;
            
            // Eski request'leri temizle
            rateLimitInfo.Requests.RemoveAll(r => r < now - _windowSize);
            rateLimitInfo.HourlyRequests.RemoveAll(r => r < now - TimeSpan.FromHours(1));

            // Rate limit kontrolü
            if (rateLimitInfo.Requests.Count >= MaxRequestsPerMinute)
            {
                _logger.LogWarning("Rate limit exceeded for client {ClientId}: {Count} requests in last minute", 
                    clientId, rateLimitInfo.Requests.Count);
                shouldBlock = true;
                errorMessage = "Çok fazla istek gönderdiniz. Lütfen bir dakika bekleyin.";
            }
            else if (rateLimitInfo.HourlyRequests.Count >= MaxRequestsPerHour)
            {
                _logger.LogWarning("Hourly rate limit exceeded for client {ClientId}: {Count} requests in last hour", 
                    clientId, rateLimitInfo.HourlyRequests.Count);
                shouldBlock = true;
                errorMessage = "Saatlik istek limitiniz aşıldı. Lütfen daha sonra tekrar deneyin.";
            }
            else
            {
                // Request'i kaydet
                rateLimitInfo.Requests.Add(now);
                rateLimitInfo.HourlyRequests.Add(now);
                rateLimitInfo.LastRequest = now;
            }
        }

        // Lock dışında await kullan
        if (shouldBlock)
        {
            context.Response.StatusCode = (int)HttpStatusCode.TooManyRequests;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(
                System.Text.Json.JsonSerializer.Serialize(new 
                { 
                    error = errorMessage 
                }));
            return;
        }

        await _next(context);
    }

    private string GetClientIdentifier(HttpContext context)
    {
        // IP adresini al
        var ip = context.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
        
        // X-Forwarded-For header'ını kontrol et
        var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwardedFor))
        {
            var ips = forwardedFor.Split(',');
            ip = ips[0].Trim();
        }

        // Session ID varsa ekle (daha iyi tracking için)
        var sessionId = context.Request.Headers["X-Session-Id"].FirstOrDefault();
        if (!string.IsNullOrEmpty(sessionId))
        {
            return $"{ip}:{sessionId}";
        }

        return ip;
    }

    private class RateLimitInfo
    {
        public List<DateTime> Requests { get; set; } = new();
        public List<DateTime> HourlyRequests { get; set; } = new();
        public DateTime LastRequest { get; set; } = DateTime.UtcNow;
    }
}

