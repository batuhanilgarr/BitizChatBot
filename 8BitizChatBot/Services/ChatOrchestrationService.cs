using BitizChatBot.Models.DTOs;
using BitizChatBot.Models;
using BitizChatBot.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http.Extensions;
using System.Text.Json;

namespace BitizChatBot.Services;

public class ChatOrchestrationService : IChatOrchestrationService
{
    private readonly ILlmService _llmService;
    private readonly IExternalApiService _externalApiService;
    private readonly IAdminSettingsService _settingsService;
    private readonly IDomainAppearanceService _domainAppearanceService;
    private readonly ApplicationDbContext _context;
    private readonly ILogger<ChatOrchestrationService> _logger;
    private readonly IHttpContextAccessor? _httpContextAccessor;
    private readonly ISecurityService _securityService;
    private readonly bool _useMemoryForContext;
    
    // In-memory conversation contexts (fallback when UseMemoryForContext = true)
    private static readonly Dictionary<string, ConversationContext> _conversationContexts = new();
    private static readonly object _contextLock = new object();

    public ChatOrchestrationService(
        ILlmService llmService,
        IExternalApiService externalApiService,
        IAdminSettingsService settingsService,
        IDomainAppearanceService domainAppearanceService,
        ApplicationDbContext context,
        ILogger<ChatOrchestrationService> logger,
        ISecurityService securityService,
        IConfiguration configuration,
        IHttpContextAccessor? httpContextAccessor = null)
    {
        _llmService = llmService;
        _externalApiService = externalApiService;
        _settingsService = settingsService;
        _domainAppearanceService = domainAppearanceService;
        _context = context;
        _logger = logger;
        _securityService = securityService;
        _httpContextAccessor = httpContextAccessor;
        _useMemoryForContext = configuration.GetValue<bool>("Database:UseMemoryForContext", true);
    }

    public async Task<ChatResponse> ProcessMessageAsync(string userMessage, string? sessionId = null)
    {
        ChatResponse? response = null;
        try
        {
            // Security: Input validation and sanitization
            if (!_securityService.IsValidInput(userMessage))
            {
                return new ChatResponse { Message = "Mesajınız çok uzun veya geçersiz. Lütfen 400 karakterden kısa bir mesaj gönderin." };
            }

            if (_securityService.ContainsSpam(userMessage))
            {
                _logger.LogWarning("Spam detected in message from session {SessionId}", sessionId);
                return new ChatResponse { Message = "Mesajınız spam içeriyor gibi görünüyor. Lütfen geçerli bir soru sorun." };
            }

            // Sanitize input
            userMessage = _securityService.SanitizeInput(userMessage);

            // Ensure session exists in database
            sessionId = await EnsureSessionAsync(sessionId);

            // Security: Validate session security
            var session = await _context.ChatSessions.FindAsync(sessionId);
            var clientIp = _securityService.GetClientIpAddress(_httpContextAccessor?.HttpContext);
            var userAgent = _securityService.GetUserAgent(_httpContextAccessor?.HttpContext);
            _securityService.ValidateSessionSecurity(sessionId, clientIp, userAgent, session);

            // Save user message to database
            await SaveMessageAsync(sessionId, userMessage, isUser: true);

            // Check for simple greetings and common questions first
            var simpleResponse = await GetSimpleResponseAsync(userMessage);
            if (simpleResponse != null)
            {
                // Ensure session exists
                sessionId = await EnsureSessionAsync(sessionId);
                
                // Save user message to database
                await SaveMessageAsync(sessionId, userMessage, isUser: true);
                
                // Clear context on greeting
                if (sessionId != null)
                {
                    await ClearContextAsync(sessionId);
                }
                response = new ChatResponse { Message = simpleResponse };
                if (!string.IsNullOrEmpty(sessionId))
                {
                    await SaveMessageAsync(sessionId, simpleResponse, isUser: false);
                }
                return response;
            }

            // Get or create conversation context
            var context = await GetOrCreateContextAsync(sessionId);
            
            // WhatsApp follow-up: waiting for consent?
            if (context.AwaitingWhatsAppConsent)
            {
                var lower = userMessage.Trim().ToLowerInvariant();
                if (IsAffirmative(lower))
                {
                    context.AwaitingWhatsAppConsent = false;
                    context.AwaitingWhatsAppPhone = true;
                    await SaveContextAsync(context);
                    return new ChatResponse { Message = "Telefon numaranızı başında 0 olmadan yazın, bayi listesini WhatsApp ile ileteyim." };
                }
                else if (IsNegative(lower))
                {
                    context.AwaitingWhatsAppConsent = false;
                    context.AwaitingWhatsAppPhone = false;
                    context.LastDealerSummary = null;
                    await SaveContextAsync(context);
                    // continue
                }
            }

            // WhatsApp follow-up: waiting for phone?
            if (context.AwaitingWhatsAppPhone)
            {
                var digits = new string(userMessage.Where(char.IsDigit).ToArray());
                if (digits.Length < 10 || digits.Length > 13)
                {
                    return new ChatResponse { Message = "Geçerli bir telefon numarası girin (örnek: 5301234567 veya +905301234567)." };
                }

                context.AwaitingWhatsAppPhone = false;
                var summary = context.LastDealerSummary ?? "Bayi listesi hazır.";
                context.LastDealerSummary = null;
                await SaveContextAsync(context);

                return new ChatResponse
                {
                    Message = $"Teşekkürler. Bayi listesini {digits} numarasına WhatsApp ile ilettim.\n\n{summary}"
                };
            }

            var settings = await _settingsService.GetSettingsAsync();
            var intentResult = await _llmService.DetectIntentAsync(userMessage, settings.SystemPrompt, context);

            // Merge detected parameters with context
            MergeParameters(context, intentResult);
            await SaveContextAsync(context);

            // If clarification is needed, return the clarification message
            if (intentResult.RequiresClarification && !string.IsNullOrEmpty(intentResult.ClarificationMessage))
            {
                response = new ChatResponse { Message = intentResult.ClarificationMessage };
                await SaveMessageAsync(sessionId, intentResult.ClarificationMessage, isUser: false);
                return response;
            }

            // Route to appropriate API based on intent
            switch (intentResult.Intent)
            {
                case IntentType.DealerSearchByLocation:
                    context.CurrentIntent = null; // Clear context after successful search
                    await SaveContextAsync(context);
                    response = await HandleDealerSearchByLocation(intentResult, sessionId ?? string.Empty);
                    break;
                
                case IntentType.DealerSearchByCityDistrict:
                    context.CurrentIntent = null; // Clear context after successful search
                    await SaveContextAsync(context);
                    response = await HandleDealerSearchByCityDistrict(intentResult, sessionId ?? string.Empty);
                    break;
                
                case IntentType.TireSearch:
                    response = await HandleTireSearch(intentResult, context);
                    await SaveContextAsync(context);
                    break;

                case IntentType.GeneralQuestion:
                    // Genel soru ise, mevcut bağlamdan bağımsız olarak LLM'e yönlendir
                    var llmGeneralResponse = await _llmService.GenerateResponseAsync(
                        userMessage,
                        settings.SystemPrompt,
                        settings.Temperature,
                        settings.MaxTokens);
                    response = new ChatResponse { Message = llmGeneralResponse };
                    break;
                
                case IntentType.Unknown:
                default:
                    // Sadece intent bilinmiyorsa mevcut lastik arama bağlamını devam ettir
                    if (context.CurrentIntent == IntentType.TireSearch)
                    {
                        response = await HandleTireSearch(intentResult, context);
                    }
                    else
                    {
                        // Diğer durumlarda LLM'e sor
                        var llmFallbackResponse = await _llmService.GenerateResponseAsync(
                            userMessage,
                            settings.SystemPrompt,
                            settings.Temperature,
                            settings.MaxTokens);
                        response = new ChatResponse { Message = llmFallbackResponse };
                    }
                    break;
            }

            // Save bot response to database
            if (response != null && !string.IsNullOrEmpty(sessionId))
            {
                await SaveMessageAsync(sessionId, response.Message, isUser: false, response: response);
            }

            return response ?? new ChatResponse { Message = "Üzgünüm, bir hata oluştu." };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing chat message");
            var errorResponse = new ChatResponse { Message = $"Üzgünüm, bir hata oluştu: {ex.Message}" };
            
            // Save error message if we have a session
            if (sessionId != null)
            {
                try
                {
                    await SaveMessageAsync(sessionId, errorResponse.Message, isUser: false, errorMessage: ex.Message);
                }
                catch
                {
                    // Ignore save errors
                }
            }
            
            return errorResponse;
        }
    }

    private async Task<string> EnsureSessionAsync(string? sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            sessionId = Guid.NewGuid().ToString();
        }

        var session = await _context.ChatSessions.FindAsync(sessionId);
        if (session == null)
        {
            var domain = _httpContextAccessor?.HttpContext?.Request.Headers["Referer"].ToString();
            if (string.IsNullOrEmpty(domain) && _httpContextAccessor?.HttpContext != null)
            {
                var uri = new Uri(_httpContextAccessor.HttpContext.Request.GetDisplayUrl());
                domain = uri.Host;
            }

            var userAgent = _httpContextAccessor?.HttpContext?.Request.Headers["User-Agent"].ToString();
            if (!string.IsNullOrEmpty(userAgent) && userAgent.Length > 500)
            {
                userAgent = userAgent.Substring(0, 500);
            }

            session = new ChatSession
            {
                SessionId = sessionId,
                Domain = domain,
                UserAgent = userAgent,
                IpAddress = _httpContextAccessor?.HttpContext?.Connection.RemoteIpAddress?.ToString(),
                CreatedAt = DateTime.UtcNow,
                LastActivityAt = DateTime.UtcNow,
                IsActive = true
            };

            _context.ChatSessions.Add(session);
            await _context.SaveChangesAsync();
        }
        else
        {
            session.LastActivityAt = DateTime.UtcNow;
            _context.ChatSessions.Update(session);
            await _context.SaveChangesAsync();
        }

        return sessionId;
    }

    private async Task SaveMessageAsync(string sessionId, string content, bool isUser, ChatResponse? response = null, string? errorMessage = null)
    {
        try
        {
            var message = new ChatMessageEntity
            {
                SessionId = sessionId,
                IsUser = isUser,
                Content = content,
                ErrorMessage = errorMessage,
                Timestamp = DateTime.UtcNow
            };

            if (response != null)
            {
                if (response.Dealers != null && response.Dealers.Any())
                {
                    message.DealersJson = JsonSerializer.Serialize(response.Dealers);
                }
                if (response.Tires != null && response.Tires.Any())
                {
                    message.TiresJson = JsonSerializer.Serialize(response.Tires);
                }
            }

            _context.ChatMessages.Add(message);
            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving chat message to database");
        }
    }

    private async Task<ConversationContext> GetOrCreateContextAsync(string? sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            sessionId = Guid.NewGuid().ToString();
        }

        // Memory kullanılıyorsa memory'den al
        if (_useMemoryForContext)
        {
            return GetOrCreateContextMemory(sessionId);
        }

        // Database'den context'i yükle
        var entity = await _context.ConversationContexts.FindAsync(sessionId);
        
        if (entity == null)
        {
            // Yeni context oluştur
            entity = new ConversationContextEntity
            {
                SessionId = sessionId,
                LastActivity = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow
            };
            _context.ConversationContexts.Add(entity);
            await _context.SaveChangesAsync();
        }
        else
        {
            // LastActivity'yi güncelle
            entity.LastActivity = DateTime.UtcNow;
            _context.ConversationContexts.Update(entity);
            await _context.SaveChangesAsync();
        }

        // Entity'den model'e dönüştür
        return MapToConversationContext(entity);
    }

    private ConversationContext GetOrCreateContextMemory(string? sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            sessionId = Guid.NewGuid().ToString();
        }

        lock (_contextLock)
        {
            if (!_conversationContexts.TryGetValue(sessionId, out var context))
            {
                context = new ConversationContext { SessionId = sessionId };
                _conversationContexts[sessionId] = context;
            }
            
            // Clean old contexts (older than 30 minutes)
            var cutoff = DateTime.UtcNow.AddMinutes(-30);
            var keysToRemove = _conversationContexts
                .Where(kvp => kvp.Value.LastActivity < cutoff)
                .Select(kvp => kvp.Key)
                .ToList();
            
            foreach (var key in keysToRemove)
            {
                _conversationContexts.Remove(key);
            }
            
            context.LastActivity = DateTime.UtcNow;
            return context;
        }
    }

    private ConversationContext MapToConversationContext(ConversationContextEntity entity)
    {
        var context = new ConversationContext
        {
            SessionId = entity.SessionId,
            CurrentIntent = !string.IsNullOrEmpty(entity.CurrentIntent) 
                ? Enum.Parse<IntentType>(entity.CurrentIntent) 
                : null,
            Brand = entity.Brand,
            Model = entity.Model,
            Year = entity.Year,
            Season = entity.Season,
            BrandModelInvalidAttempts = entity.BrandModelInvalidAttempts,
            AwaitingWhatsAppConsent = entity.AwaitingWhatsAppConsent,
            AwaitingWhatsAppPhone = entity.AwaitingWhatsAppPhone,
            LastDealerSummary = entity.LastDealerSummary,
            LastActivity = entity.LastActivity
        };

        // CollectedParameters JSON'dan deserialize et
        if (!string.IsNullOrEmpty(entity.CollectedParametersJson))
        {
            try
            {
                context.CollectedParameters = JsonSerializer.Deserialize<Dictionary<string, string>>(
                    entity.CollectedParametersJson) ?? new Dictionary<string, string>();
            }
            catch
            {
                context.CollectedParameters = new Dictionary<string, string>();
            }
        }

        return context;
    }

    private async Task SaveContextAsync(ConversationContext context)
    {
        // Memory kullanılıyorsa kaydetme gerekmez (zaten memory'de)
        if (_useMemoryForContext)
        {
            // Memory'de zaten güncel, sadece LastActivity'yi güncelle
            lock (_contextLock)
            {
                if (_conversationContexts.TryGetValue(context.SessionId, out var existing))
                {
                    existing.LastActivity = DateTime.UtcNow;
                }
            }
            return;
        }

        // Database'e kaydet
        var entity = await _context.ConversationContexts.FindAsync(context.SessionId);
        
        if (entity == null)
        {
            entity = new ConversationContextEntity
            {
                SessionId = context.SessionId,
                CreatedAt = DateTime.UtcNow
            };
            _context.ConversationContexts.Add(entity);
        }

        // Model'den entity'ye dönüştür
        entity.CurrentIntent = context.CurrentIntent?.ToString();
        entity.Brand = context.Brand;
        entity.Model = context.Model;
        entity.Year = context.Year;
        entity.Season = context.Season;
        entity.BrandModelInvalidAttempts = context.BrandModelInvalidAttempts;
        entity.AwaitingWhatsAppConsent = context.AwaitingWhatsAppConsent;
        entity.AwaitingWhatsAppPhone = context.AwaitingWhatsAppPhone;
        entity.LastDealerSummary = context.LastDealerSummary;
        entity.LastActivity = context.LastActivity;

        // CollectedParameters'ı JSON'a serialize et
        if (context.CollectedParameters != null && context.CollectedParameters.Any())
        {
            entity.CollectedParametersJson = JsonSerializer.Serialize(context.CollectedParameters);
        }

        _context.ConversationContexts.Update(entity);
        await _context.SaveChangesAsync();
    }

    public async Task ClearContextAsync(string sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
            return;

        if (_useMemoryForContext)
        {
            lock (_contextLock)
            {
                _conversationContexts.Remove(sessionId);
            }
            return;
        }

        var entity = await _context.ConversationContexts.FindAsync(sessionId);
        if (entity != null)
        {
            _context.ConversationContexts.Remove(entity);
            await _context.SaveChangesAsync();
        }
    }

    public void ClearContext(string sessionId)
    {
        // Synchronous wrapper for backward compatibility
        _ = ClearContextAsync(sessionId);
    }

    private void MergeParameters(ConversationContext context, IntentDetectionResult intent)
    {
        // If we detect a tire search intent, set it in context
        if (intent.Intent == IntentType.TireSearch)
        {
            context.CurrentIntent = IntentType.TireSearch;
        }

        // Merge parameters from intent detection
        foreach (var param in intent.Parameters)
        {
            if (!string.IsNullOrWhiteSpace(param.Value))
            {
                // Always keep latest value for collected parameters
                context.CollectedParameters[param.Key] = param.Value;
                
                // Also set specific properties for tire search (latest answer overrides previous)
                if (param.Key == "brand")
                {
                    context.Brand = param.Value;
                }
                else if (param.Key == "model")
                {
                    context.Model = param.Value;
                }
                else if (param.Key == "year")
                {
                    context.Year = param.Value;
                }
                else if (param.Key == "season")
                {
                    context.Season = param.Value;
                }
            }
        }
    }

    private async Task<string?> GetSimpleResponseAsync(string userMessage)
    {
        var settings = await _settingsService.GetSettingsAsync();
        var lowerMessage = userMessage.ToLowerInvariant().Trim();

        // Get domain-specific appearance settings if available
        DomainAppearance? domainAppearance = null;
        try
        {
            if (_httpContextAccessor?.HttpContext != null)
            {
                var referer = _httpContextAccessor.HttpContext.Request.Headers["Referer"].ToString();
                if (!string.IsNullOrEmpty(referer))
                {
                    var uri = new Uri(referer);
                    var query = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(uri.Query);
                    var domain = query.ContainsKey("domain") ? query["domain"].ToString() : uri.Host;
                    
                    if (!string.IsNullOrEmpty(domain))
                    {
                        domainAppearance = await _domainAppearanceService.GetAsync(domain);
                    }
                }
            }
        }
        catch
        {
            // If domain lookup fails, use global settings
        }

        // Helper function to get response, preferring domain-specific if available
        string GetResponse(Func<AdminSettings, string> globalGetter, Func<DomainAppearance, string> domainGetter)
        {
            if (domainAppearance != null && !string.IsNullOrWhiteSpace(domainGetter(domainAppearance)))
            {
                return domainGetter(domainAppearance);
            }
            return globalGetter(settings);
        }

        // Greetings
        if (lowerMessage.Contains("merhaba") || lowerMessage.Contains("selam") || lowerMessage.Contains("selamun aleyküm") || 
            lowerMessage == "hi" || lowerMessage == "hello" || lowerMessage == "hey")
        {
            return GetResponse(s => s.GreetingResponse, d => d.GreetingResponse);
        }

        // How are you
        if (lowerMessage.Contains("nasılsın") || lowerMessage.Contains("nasılsınız") || 
            lowerMessage.Contains("how are you") || lowerMessage.Contains("how's it going"))
        {
            return GetResponse(s => s.HowAreYouResponse, d => d.HowAreYouResponse);
        }

        // Who are you / What are you
        if (lowerMessage.Contains("kimsin") || lowerMessage.Contains("kimsiniz") || 
            lowerMessage.Contains("sen kimsin") || lowerMessage.Contains("siz kimsiniz") ||
            lowerMessage.Contains("who are you") || lowerMessage.Contains("what are you"))
        {
            return GetResponse(s => s.WhoAreYouResponse, d => d.WhoAreYouResponse);
        }

        // What can you do
        if (lowerMessage.Contains("ne yapabilirsin") || lowerMessage.Contains("ne yapabilirsiniz") ||
            lowerMessage.Contains("what can you do") || lowerMessage.Contains("what do you do"))
        {
            return GetResponse(s => s.WhatCanYouDoResponse, d => d.WhatCanYouDoResponse);
        }

        // Thanks
        if (lowerMessage.Contains("teşekkür") || lowerMessage.Contains("sağol") || lowerMessage.Contains("sağ ol") ||
            lowerMessage.Contains("sagol") || lowerMessage.Contains("sag ol") ||
            lowerMessage == "saol" || lowerMessage == "sagol" || lowerMessage == "sağol" ||
            lowerMessage.Contains("thanks") || lowerMessage.Contains("thank you") ||
            lowerMessage.Contains("teşekkürler") || lowerMessage.Contains("teşekkür ederim") ||
            lowerMessage.Contains("teşekkürler") || lowerMessage.Contains("eyvallah") ||
            lowerMessage.Contains("müteşekkir") || lowerMessage.Contains("minnettar"))
        {
            return GetResponse(s => s.ThanksResponse, d => d.ThanksResponse);
        }

        // Goodbye
        if (lowerMessage.Contains("güle güle") || lowerMessage.Contains("hoşça kal") || lowerMessage.Contains("bay bay") ||
            lowerMessage.Contains("bye") || lowerMessage.Contains("goodbye") || lowerMessage.Contains("see you"))
        {
            return GetResponse(s => s.GoodbyeResponse, d => d.GoodbyeResponse);
        }

        // Eğer doğrudan eşleşme yoksa, kısa ve genel sorular için LLM ile sınıflandırma yap
        // Böylece "ne işe yarıyorsun" gibi varyasyonlar da doğru hazır cevaba yönlenebilir.
        if (userMessage.Length <= 120)
        {
            var category = await ClassifySimpleIntentAsync(userMessage);
            switch (category)
            {
                case "greeting":
                    return GetResponse(s => s.GreetingResponse, d => d.GreetingResponse);
                case "how_are_you":
                    return GetResponse(s => s.HowAreYouResponse, d => d.HowAreYouResponse);
                case "who_are_you":
                    return GetResponse(s => s.WhoAreYouResponse, d => d.WhoAreYouResponse);
                case "what_can_you_do":
                    return GetResponse(s => s.WhatCanYouDoResponse, d => d.WhatCanYouDoResponse);
                case "thanks":
                    return GetResponse(s => s.ThanksResponse, d => d.ThanksResponse);
                case "goodbye":
                    return GetResponse(s => s.GoodbyeResponse, d => d.GoodbyeResponse);
            }
        }

        return null; // No simple response found, continue with normal processing
    }

    /// <summary>
    /// Kısa kullanıcı mesajlarını basit kategorilere ayırmak için LLM tabanlı sınıflandırma.
    /// Dönen değerler: greeting, how_are_you, who_are_you, what_can_you_do, thanks, goodbye, none
    /// </summary>
    private async Task<string> ClassifySimpleIntentAsync(string userMessage)
    {
        try
        {
            var lowerMessage = userMessage.ToLowerInvariant().Trim();
            
            // Fallback: Eğer mesajda açıkça teşekkür ifadesi varsa, LLM çağrısı yapmadan direkt "thanks" döndür
            if (lowerMessage.Contains("teşekkür") || lowerMessage.Contains("sağol") || lowerMessage.Contains("sağ ol") ||
                lowerMessage.Contains("sagol") || lowerMessage.Contains("sag ol") ||
                lowerMessage == "saol" || lowerMessage == "sagol" || lowerMessage == "sağol" ||
                lowerMessage.Contains("thanks") || lowerMessage.Contains("thank you") ||
                lowerMessage.Contains("teşekkürler") || lowerMessage.Contains("teşekkür ederim") ||
                lowerMessage.Contains("eyvallah") || lowerMessage.Contains("müteşekkir") ||
                lowerMessage.Contains("minnettar"))
            {
                return "thanks";
            }
            
            var classifierPrompt =
                "Sen bir sınıflandırma asistanısın. Kullanıcının aşağıdaki mesajını oku ve hangi kategoriye ait olduğunu belirle:\n" +
                "- greeting (selamlaşma)\n" +
                "- how_are_you (nasılsın soruları)\n" +
                "- who_are_you (kimsin / neysin soruları)\n" +
                "- what_can_you_do (\"ne yapabilirsin\", \"ne işe yarıyorsun\" gibi yetenek soruları)\n" +
                "- thanks (teşekkür ifadeleri: teşekkür, sağol, sagol, thanks, thank you, eyvallah)\n" +
                "- goodbye (veda cümleleri)\n" +
                "- none (hiçbiri değil)\n\n" +
                "SADECE bu etiketlerden birini, küçük harflerle ve ek açıklama yazmadan döndür.";

            var response = await _llmService.GenerateResponseAsync(
                userMessage,
                classifierPrompt,
                temperature: 0.0,
                maxTokens: 10);

            var normalized = response.Trim().ToLowerInvariant();

            // İlk kelimeyi al (olur da model fazladan bir şey yazarsa)
            var firstToken = normalized.Split(new[] { ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries)
                                       .FirstOrDefault() ?? "none";

            return firstToken switch
            {
                "greeting" => "greeting",
                "how_are_you" => "how_are_you",
                "who_are_you" => "who_are_you",
                "what_can_you_do" => "what_can_you_do",
                "thanks" => "thanks",
                "goodbye" => "goodbye",
                _ => "none"
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to classify simple intent for message: {Message}", userMessage);
            
            // Fallback: Eğer hata alırsak ve mesaj teşekkür içeriyorsa, "thanks" döndür
            var lowerMessage = userMessage.ToLowerInvariant().Trim();
            if (lowerMessage.Contains("teşekkür") || lowerMessage.Contains("sağol") || lowerMessage.Contains("sağ ol") ||
                lowerMessage.Contains("sagol") || lowerMessage.Contains("sag ol") ||
                lowerMessage == "saol" || lowerMessage == "sagol" || lowerMessage == "sağol" ||
                lowerMessage.Contains("thanks") || lowerMessage.Contains("thank you") ||
                lowerMessage.Contains("teşekkürler") || lowerMessage.Contains("teşekkür ederim") ||
                lowerMessage.Contains("eyvallah") || lowerMessage.Contains("müteşekkir") ||
                lowerMessage.Contains("minnettar"))
            {
                return "thanks";
            }
            
            return "none";
        }
    }

    private string CapitalizeFirstLetter(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return text;
        
        // Handle Turkish characters
        var turkishChars = new Dictionary<char, char>
        {
            { 'ı', 'I' }, { 'i', 'İ' }, { 'ğ', 'Ğ' }, { 'ü', 'Ü' },
            { 'ş', 'Ş' }, { 'ö', 'Ö' }, { 'ç', 'Ç' }
        };
        
        var firstChar = text[0];
        if (turkishChars.TryGetValue(char.ToLowerInvariant(firstChar), out var turkishUpper))
        {
            return turkishUpper + text.Substring(1).ToLowerInvariant();
        }
        
        return char.ToUpperInvariant(firstChar) + text.Substring(1).ToLowerInvariant();
    }

    private bool IsAffirmative(string input)
    {
        var norm = NormalizeForMatch(input);
        var positives = new[]
        {
            "evet","gonder","gonderin","gonderin","yolla","olur","tamam","ok","okey",
            "isterim","tabii","tabi","elbette","aynendir","aynen","tesekkurler","teşekkürler"
        };
        return positives.Any(p => norm.Contains(p) || LevenshteinDistance(norm, p) <= 1);
    }

    private bool IsNegative(string input)
    {
        var norm = NormalizeForMatch(input);
        var negatives = new[]
        {
            "hayir","hayır","istemiyorum","yok","gerek yok","kapat","no","olmaz","hayda"
        };
        return negatives.Any(n => norm.Contains(n) || LevenshteinDistance(norm, n) <= 1);
    }

    private string NormalizeForMatch(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        var normalized = text.Normalize(System.Text.NormalizationForm.FormD);
        var sb = new System.Text.StringBuilder(normalized.Length);
        foreach (var ch in normalized)
        {
            var uc = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(ch);
            if (uc != System.Globalization.UnicodeCategory.NonSpacingMark)
            {
                var c = ch switch
                {
                    'ı' or 'İ' => 'i',
                    _ => ch
                };
                sb.Append(char.ToLowerInvariant(c));
            }
        }
        return sb.ToString().Normalize(System.Text.NormalizationForm.FormC);
    }

    private int LevenshteinDistance(string s, string t)
    {
        if (string.IsNullOrEmpty(s)) return t.Length;
        if (string.IsNullOrEmpty(t)) return s.Length;
        var n = s.Length;
        var m = t.Length;
        var d = new int[n + 1, m + 1];
        for (int i = 0; i <= n; i++) d[i, 0] = i;
        for (int j = 0; j <= m; j++) d[0, j] = j;
        for (int i = 1; i <= n; i++)
        {
            for (int j = 1; j <= m; j++)
            {
                var cost = s[i - 1] == t[j - 1] ? 0 : 1;
                d[i, j] = Math.Min(
                    Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                    d[i - 1, j - 1] + cost);
            }
        }
        return d[n, m];
    }

    private string BuildDealerSummary(List<DealerDto> dealers)
    {
        if (dealers == null || dealers.Count == 0) return string.Empty;
        var take = Math.Min(5, dealers.Count);
        var lines = dealers.Take(take)
            .Select(d =>
            {
                var distance = d.Distance.HasValue ? $"{d.Distance.Value:F2} km" : "";
                return $"- {d.FullName}{(string.IsNullOrWhiteSpace(distance) ? "" : $" ({distance})")}";
            });
        return "Bayi listesi:\n" + string.Join("\n", lines);
    }

    private async Task<ChatResponse> HandleDealerSearchByLocation(IntentDetectionResult intent, string sessionId)
    {
        // Try to extract latitude and longitude from parameters or user message
        double? latitude = null;
        double? longitude = null;

        // Check parameters first
        if (intent.Parameters.TryGetValue("latitude", out var latStr) && double.TryParse(latStr, out var lat))
        {
            latitude = lat;
        }
        if (intent.Parameters.TryGetValue("longitude", out var longStr) && double.TryParse(longStr, out var lon))
        {
            longitude = lon;
        }

        // If not in parameters, try to extract from user message
        if (!latitude.HasValue || !longitude.HasValue)
        {
            var userText = intent.UserMessage ?? string.Empty;
            // Look for patterns like "Latitude 41.0082, Longitude 28.9784" or "41.0082, 28.9784"
            var latMatch = System.Text.RegularExpressions.Regex.Match(userText, @"(?:latitude|lat|enlem)[\s:]*([+-]?\d+\.?\d*)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            var lonMatch = System.Text.RegularExpressions.Regex.Match(userText, @"(?:longitude|long|lng|boylam)[\s:]*([+-]?\d+\.?\d*)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            
            if (latMatch.Success && double.TryParse(latMatch.Groups[1].Value, out var extractedLat))
            {
                latitude = extractedLat;
            }
            if (lonMatch.Success && double.TryParse(lonMatch.Groups[1].Value, out var extractedLon))
            {
                longitude = extractedLon;
            }

            // Try pattern like "41.0082, 28.9784"
            if (!latitude.HasValue || !longitude.HasValue)
            {
                var coordMatch = System.Text.RegularExpressions.Regex.Match(userText, @"([+-]?\d+\.?\d*)[\s,]+([+-]?\d+\.?\d*)");
                if (coordMatch.Success)
                {
                    if (double.TryParse(coordMatch.Groups[1].Value, out var coord1) && 
                        double.TryParse(coordMatch.Groups[2].Value, out var coord2))
                    {
                        // Assume first is lat, second is lon (typical format)
                        if (!latitude.HasValue) latitude = coord1;
                        if (!longitude.HasValue) longitude = coord2;
                    }
                }
            }
        }

        if (!latitude.HasValue || !longitude.HasValue)
        {
            return new ChatResponse { Message = "Konum bilgileriniz eksik görünüyor. Lütfen konum butonuna tıklayın veya enlem ve boylam bilgilerinizi paylaşın (örnek: Latitude 41.0082, Longitude 28.9784)." };
        }

        var response = await _externalApiService.SearchDealersByLocationAsync(latitude.Value, longitude.Value);

        if (!response.Success || response.Dealers.Count == 0)
        {
            return new ChatResponse { Message = response.Message ?? "Yakınınızda bir bayi bulunamadı." };
        }

        var resultMessage = response.Message ?? $"{response.Dealers.Count} adet bayi bulundu";
        var summary = BuildDealerSummary(response.Dealers);

        var context = await GetOrCreateContextAsync(sessionId);
        context.AwaitingWhatsAppConsent = true;
        context.LastDealerSummary = summary;
        await SaveContextAsync(context);

        return new ChatResponse 
        { 
            Message = $"{resultMessage}\n\nBayi listesini WhatsApp'ınıza göndermemi ister misiniz? (Evet/Hayır)",
            Dealers = response.Dealers 
        };
    }

    private async Task<ChatResponse> HandleDealerSearchByCityDistrict(IntentDetectionResult intent, string sessionId)
    {
        if (!intent.Parameters.TryGetValue("city", out var city) || string.IsNullOrWhiteSpace(city))
        {
            return new ChatResponse { Message = "Lütfen şehir adını belirtin." };
        }

        intent.Parameters.TryGetValue("district", out var district);
        
        // Capitalize first letter of city and district for API
        city = CapitalizeFirstLetter(city);
        district = !string.IsNullOrWhiteSpace(district) ? CapitalizeFirstLetter(district) : string.Empty;

        var response = await _externalApiService.SearchDealersByCityDistrictAsync(city, district);

        if (!response.Success || response.Dealers.Count == 0)
        {
            return new ChatResponse { Message = response.Message ?? $"{(string.IsNullOrEmpty(district) ? city : $"{city} {district}")} bölgesinde bayi bulunamadı." };
        }

        var resultMessage = response.Message ?? $"{response.Dealers.Count} adet bayi bulundu";
        var summary = BuildDealerSummary(response.Dealers);

        var context = await GetOrCreateContextAsync(sessionId);
        context.AwaitingWhatsAppConsent = true;
        context.LastDealerSummary = summary;
        await SaveContextAsync(context);

        return new ChatResponse 
        { 
            Message = $"{resultMessage}\n\nBayi listesini WhatsApp'ınıza göndermemi ister misiniz? (Evet/Hayır)",
            Dealers = response.Dealers 
        };
    }

    private async Task<ChatResponse> HandleTireSearch(IntentDetectionResult intent, ConversationContext context)
    {
        // Try to get brand from intent parameters or context
        var brand = intent.Parameters.TryGetValue("brand", out var b) && !string.IsNullOrWhiteSpace(b) 
            ? b 
            : context.Brand;

        // Try to get model from intent parameters or context
        var model = intent.Parameters.TryGetValue("model", out var m) && !string.IsNullOrWhiteSpace(m) 
            ? m 
            : context.Model;

        // Try to get year from intent parameters or context
        var yearStr = intent.Parameters.TryGetValue("year", out var y) && !string.IsNullOrWhiteSpace(y) 
            ? y 
            : context.Year;

        // Try to get season from intent parameters or context
        var season = intent.Parameters.TryGetValue("season", out var s) && !string.IsNullOrWhiteSpace(s) 
            ? s 
            : context.Season;

        // Update context with collected values
        if (!string.IsNullOrWhiteSpace(brand) && string.IsNullOrWhiteSpace(context.Brand))
            context.Brand = brand;
        if (!string.IsNullOrWhiteSpace(model) && string.IsNullOrWhiteSpace(context.Model))
            context.Model = model;
        if (!string.IsNullOrWhiteSpace(yearStr) && string.IsNullOrWhiteSpace(context.Year))
            context.Year = yearStr;
        if (!string.IsNullOrWhiteSpace(season) && string.IsNullOrWhiteSpace(context.Season))
            context.Season = season;

        // Check what's missing and ask for it
        if (string.IsNullOrWhiteSpace(brand))
        {
            return new ChatResponse { Message = "Araç markasını belirtir misiniz? (Örnek: Toyota, Ford, BMW)" };
        }

        if (string.IsNullOrWhiteSpace(model))
        {
            var collectedInfo = string.IsNullOrWhiteSpace(brand) ? "" : $"Marka: {brand}. ";
            return new ChatResponse { Message = $"{collectedInfo}Araç modelini belirtir misiniz? (Örnek: Corolla, Focus, 3 Series)" };
        }

        // Brand/Model validation (without year)
        var validation = await _externalApiService.ValidateBrandModelAsync(brand, model);
        if (validation.IsMismatch)
        {
            context.BrandModelInvalidAttempts++;
            context.Model = null; // force re-entry
            await SaveContextAsync(context);
            if (context.BrandModelInvalidAttempts >= 3)
            {
                context.CurrentIntent = null;
                context.Brand = null;
                context.Model = null;
                context.Year = null;
                context.Season = null;
                context.CollectedParameters.Clear();
                context.BrandModelInvalidAttempts = 0;
                await SaveContextAsync(context);
                return new ChatResponse { Message = "Marka / model bilgisi yanlış girildi, lütfen tekrar deneyiniz." };
            }

            var warning = !string.IsNullOrWhiteSpace(validation.Message)
                ? FormatApiErrorMessage(validation.Message)
                : "Girdiğiniz marka/model eşleşmedi. Lütfen doğru marka ve modeli girin.";

            return new ChatResponse
            {
                Message = $"{warning}\nLütfen model bilgisini yeniden girin."
            };
        }
        else
        {
            context.BrandModelInvalidAttempts = 0;
            await SaveContextAsync(context);
        }

        if (string.IsNullOrWhiteSpace(yearStr) || !int.TryParse(yearStr, out var year))
        {
            var collectedInfo = $"Marka: {brand}, Model: {model}. ";
            return new ChatResponse { Message = $"{collectedInfo}Araç yılını belirtir misiniz? (Örnek: 2020, 2021)" };
        }

        // Normalize season
        season = season?.ToLowerInvariant() ?? "all season";
        if (season.Contains("yaz") || season.Contains("summer"))
            season = "summer";
        else if (season.Contains("kış") || season.Contains("winter"))
            season = "winter";
        else
            season = "all season";

        // All required info collected, perform search
        _logger.LogInformation("Searching tires: Brand={Brand}, Model={Model}, Year={Year}, Season={Season}", 
            brand, model, year, season);

        var response = await _externalApiService.SearchTiresAsync(brand, model, year, season);

        // Clear context after successful search
        context.CurrentIntent = null;
        context.Brand = null;
        context.Model = null;
        context.Year = null;
        context.Season = null;
        context.CollectedParameters.Clear();
        await SaveContextAsync(context);

        if (!response.Success || response.Tires.Count == 0)
        {
            var errorMessage = response.Message ?? $"{year} {brand} {model} için {season} lastik bulunamadı.";
            return new ChatResponse { Message = FormatApiErrorMessage(errorMessage) };
        }

        return new ChatResponse 
        { 
            Message = response.Message ?? $"{response.Tires.Count} adet lastik bulundu",
            Tires = response.Tires 
        };
    }

    private string FormatApiErrorMessage(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return message;

        var formatted = message.Trim();
        
        // İlk harfi büyük yap
        if (formatted.Length > 0)
        {
            formatted = char.ToUpper(formatted[0]) + formatted.Substring(1);
        }

        // Tüm marka isimlerini düzelt (case-insensitive)
        // LlmService'deki marka listesini kullan
        var vehicleBrands = new[] { 
            "TOYOTA", "DACIA", "FORD", "BMW", "MERCEDES", "MERCEDES-BENZ", "AUDI", "VOLKSWAGEN", "RENAULT", 
            "PEUGEOT", "CITROEN", "OPEL", "FIAT", "HYUNDAI", "KIA", "NISSAN", "HONDA", "MAZDA", "SUBARU", 
            "SUZUKI", "MITSUBISHI", "VOLVO", "SKODA", "SEAT", "CHEVROLET", "JEEP", "LAND ROVER", "MINI", 
            "ALFA ROMEO", "LEXUS", "INFINITI", "ACURA", "GENESIS", "BENTLEY", "PORSCHE", "FERRARI", 
            "LAMBORGHINI", "MASERATI", "ASTON MARTIN", "JAGUAR", "ROLLS-ROYCE", "TESLA", "BYD", "GEELY"
        };
        
        foreach (var brand in vehicleBrands)
        {
            var brandProper = FormatBrandName(brand);
            // Case-insensitive replace with word boundaries
            formatted = System.Text.RegularExpressions.Regex.Replace(
                formatted, 
                $@"\b{System.Text.RegularExpressions.Regex.Escape(brand)}\b", 
                brandProper, 
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }

        // Tüm model isimlerini düzelt (case-insensitive)
        var vehicleModels = new[] { 
            "COROLLA", "DUSTER", "FOCUS", "FIESTA", "GOLF", "PASSAT", "POLO", "CIVIC", "ACCORD", "CR-V", 
            "ELANTRA", "SONATA", "RIO", "CERATO", "SENTRA", "ALTIMA", "MAZDA3", "MAZDA6", "IMPREZA", 
            "OUTBACK", "YARIS", "CAMRY", "RAV4", "HILUX", "SANDERO", "LOGAN", "CLIO", "MEGANE", "ASTRA", 
            "CORSA", "TIGUAN", "A3", "A4", "A6", "3 SERIES", "5 SERIES", "C-CLASS", "E-CLASS", "CIVIC",
            "CR-V", "HR-V", "PRIUS", "AURIS", "AVENSIS", "COROLLA VERSO", "LAND CRUISER", "PRADO"
        };
        
        foreach (var model in vehicleModels)
        {
            var modelProper = FormatModelName(model);
            // Case-insensitive replace with word boundaries
            formatted = System.Text.RegularExpressions.Regex.Replace(
                formatted, 
                $@"\b{System.Text.RegularExpressions.Regex.Escape(model)}\b", 
                modelProper, 
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }

        // Noktalama işaretlerinden sonra boşluk ekle (yoksa)
        formatted = System.Text.RegularExpressions.Regex.Replace(formatted, @"([.!?;])([^\s])", "$1 $2");

        return formatted;
    }

    private string FormatBrandName(string brand)
    {
        if (string.IsNullOrWhiteSpace(brand))
            return brand;

        // Özel durumlar
        if (brand.Equals("BMW", StringComparison.OrdinalIgnoreCase))
            return "BMW";
        if (brand.Equals("MERCEDES-BENZ", StringComparison.OrdinalIgnoreCase) || brand.Equals("MERCEDES", StringComparison.OrdinalIgnoreCase))
            return "Mercedes-Benz";
        if (brand.Contains(" "))
        {
            // Çok kelimeli markalar (ALFA ROMEO, LAND ROVER, etc.)
            var parts = brand.Split(' ');
            return string.Join(" ", parts.Select(p => 
                p.Length > 0 ? (char.ToUpper(p[0]) + p.Substring(1).ToLowerInvariant()) : p));
        }
        if (brand.Contains("-"))
        {
            var parts = brand.Split('-');
            return string.Join("-", parts.Select(p => 
                p.Length > 0 ? (char.ToUpper(p[0]) + p.Substring(1).ToLowerInvariant()) : p));
        }

        // Normal marka isimleri: İlk harf büyük, geri kalan küçük
        return char.ToUpper(brand[0]) + brand.Substring(1).ToLowerInvariant();
    }

    private string FormatModelName(string model)
    {
        if (string.IsNullOrWhiteSpace(model))
            return model;

        // Özel durumlar (sayı içeren modeller)
        if (model.Contains("SERIES") || model.Contains("CLASS"))
        {
            var parts = model.Split(' ');
            return string.Join(" ", parts.Select(p => 
                p.Length > 0 ? (char.ToUpper(p[0]) + p.Substring(1).ToLowerInvariant()) : p));
        }

        // Tire içeren modeller (CR-V, A3, A4, etc.)
        if (model.Contains("-"))
        {
            var parts = model.Split('-');
            return string.Join("-", parts.Select(p => 
                p.Length > 0 ? (char.ToUpper(p[0]) + (p.Length > 1 ? p.Substring(1).ToUpperInvariant() : "")) : p));
        }

        // Normal model isimleri: İlk harf büyük, geri kalan küçük
        return char.ToUpper(model[0]) + model.Substring(1).ToLowerInvariant();
    }

}

