using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using BitizChatBot.Models;

namespace BitizChatBot.Services;

public interface ISecurityService
{
    string SanitizeInput(string input);
    bool IsValidInput(string input, int maxLength = 400);
    bool ContainsSpam(string input);
    bool ContainsProfanity(string input);
    string GetClientIpAddress(HttpContext? httpContext);
    string GetUserAgent(HttpContext? httpContext);
    bool ValidateSessionSecurity(string sessionId, string? ipAddress, string? userAgent, ChatSession? session);
}

public class SecurityService : ISecurityService
{
    private readonly ILogger<SecurityService> _logger;
    private static readonly HashSet<string> _profanityWords = new(StringComparer.OrdinalIgnoreCase)
    {
        // Türkçe küfür kelimeleri (örnek - gerçek projede daha kapsamlı olmalı)
        // Bu liste örnek amaçlıdır, gerçek projede daha kapsamlı bir filtre kullanılmalı
    };

    private static readonly Regex _spamPatterns = new Regex(
        @"(https?://[^\s]+|www\.[^\s]+|@[^\s]+|#\w+)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase
    );

    public SecurityService(ILogger<SecurityService> logger)
    {
        _logger = logger;
    }

    public string SanitizeInput(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return string.Empty;

        // HTML tag'lerini temizle
        input = Regex.Replace(input, "<.*?>", string.Empty);
        
        // Potansiyel script injection'ları temizle
        input = input.Replace("<script", "", StringComparison.OrdinalIgnoreCase);
        input = input.Replace("javascript:", "", StringComparison.OrdinalIgnoreCase);
        input = input.Replace("onerror=", "", StringComparison.OrdinalIgnoreCase);
        input = input.Replace("onclick=", "", StringComparison.OrdinalIgnoreCase);
        
        // Trim ve normalize whitespace
        input = Regex.Replace(input, @"\s+", " ");
        return input.Trim();
    }

    public bool IsValidInput(string input, int maxLength = 400)
    {
        if (string.IsNullOrWhiteSpace(input))
            return false;

        if (input.Length > maxLength)
            return false;

        // Sadece çok uzun tekrarlayan karakterler kontrolü
        if (Regex.IsMatch(input, @"(.)\1{20,}"))
            return false;

        return true;
    }

    public bool ContainsSpam(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return false;

        // URL/email pattern kontrolü
        var spamMatches = _spamPatterns.Matches(input);
        if (spamMatches.Count > 3) // 3'ten fazla link/mention varsa spam olabilir
            return true;

        // Tekrarlayan karakterler
        if (Regex.IsMatch(input, @"(.)\1{10,}"))
            return true;

        return false;
    }

    public bool ContainsProfanity(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return false;

        var words = input.Split(new[] { ' ', '\t', '\n', '\r', '.', ',', '!', '?', ';', ':' }, 
            StringSplitOptions.RemoveEmptyEntries);
        
        return words.Any(word => _profanityWords.Contains(word));
    }

    public string GetClientIpAddress(HttpContext? httpContext)
    {
        if (httpContext == null)
            return "Unknown";

        // X-Forwarded-For header'ını kontrol et (proxy/load balancer arkasında)
        var forwardedFor = httpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwardedFor))
        {
            var ips = forwardedFor.Split(',');
            return ips[0].Trim();
        }

        // X-Real-IP header'ını kontrol et
        var realIp = httpContext.Request.Headers["X-Real-IP"].FirstOrDefault();
        if (!string.IsNullOrEmpty(realIp))
            return realIp;

        // Direct connection IP
        return httpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
    }

    public string GetUserAgent(HttpContext? httpContext)
    {
        if (httpContext == null)
            return "Unknown";

        return httpContext.Request.Headers["User-Agent"].ToString() ?? "Unknown";
    }

    public bool ValidateSessionSecurity(string sessionId, string? ipAddress, string? userAgent, ChatSession? session)
    {
        if (session == null)
            return true; // Yeni session, kontrol gerekmez

        // IP adresi kontrolü (değişmişse uyarı ver ama engelleme)
        if (!string.IsNullOrEmpty(session.IpAddress) && 
            !string.IsNullOrEmpty(ipAddress) &&
            session.IpAddress != ipAddress)
        {
            _logger.LogWarning("Session {SessionId} IP mismatch: Original={OriginalIp}, Current={CurrentIp}", 
                sessionId, session.IpAddress, ipAddress);
            // Production'da daha sıkı kontrol yapılabilir
        }

        // User-Agent kontrolü (değişmişse uyarı ver)
        if (!string.IsNullOrEmpty(session.UserAgent) && 
            !string.IsNullOrEmpty(userAgent) &&
            session.UserAgent != userAgent)
        {
            _logger.LogWarning("Session {SessionId} User-Agent mismatch: Original={OriginalUA}, Current={CurrentUA}", 
                sessionId, session.UserAgent, userAgent);
        }

        return true;
    }
}

