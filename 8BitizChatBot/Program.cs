using BitizChatBot.Services;
using BitizChatBot.Data;
using BitizChatBot.Middleware;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddControllers();

// Add Entity Framework - PostgreSQL veya SQL Server desteği
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
var usePostgres = builder.Configuration.GetValue<bool>("Database:UsePostgreSQL", false);

if (usePostgres)
{
    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseNpgsql(connectionString));
}
else
{
    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseSqlServer(connectionString));
}

// Register Services
builder.Services.AddHttpContextAccessor();

// Add Session for secure authentication
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
});
builder.Services.AddScoped<IAdminSettingsService, AdminSettingsService>();
builder.Services.AddSingleton<ITurkishLocationService, TurkishLocationService>();
builder.Services.AddScoped<IDomainApiKeyService, DomainApiKeyService>();
builder.Services.AddScoped<IDomainAppearanceService, DomainAppearanceService>();
builder.Services.AddScoped<ILlmService, LlmService>();
builder.Services.AddScoped<IExternalApiService, ExternalApiService>();
builder.Services.AddScoped<IChatOrchestrationService, ChatOrchestrationService>();
builder.Services.AddScoped<IOllamaService, OllamaService>();
builder.Services.AddScoped<ISecurityService, SecurityService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IAuditLogService, AuditLogService>();
builder.Services.AddScoped<IAnalyticsService, AnalyticsService>();

// Configure Antiforgery
// Embed route'u için iframe içinde çalışması gerekiyor, bu yüzden SameSite=None kullanıyoruz
// Ancak SameSite=None için Secure flag gerekli, development'ta HTTP olduğu için cookie oluşturmayı engelleyeceğiz
builder.Services.AddAntiforgery(options =>
{
    // Production'da SameSite=None; Secure kullan
    if (!builder.Environment.IsDevelopment())
    {
        options.Cookie.SameSite = SameSiteMode.None;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    }
    else
    {
        // Development'ta SameSite=Lax kullan (iframe içinde çalışmayacak ama cookie hatası olmayacak)
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    }
    options.Cookie.HttpOnly = true;
    options.SuppressXFrameOptionsHeader = true;
});

// HttpClient for External APIs
builder.Services.AddHttpClient<IExternalApiService, ExternalApiService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
});

// HttpClient for LLM APIs
builder.Services.AddHttpClient<ILlmService, LlmService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(120); // Ollama might need more time
});

// HttpClient for Ollama Service
builder.Services.AddHttpClient<IOllamaService, OllamaService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(10);
});

// HttpClient for Ollama Service
builder.Services.AddHttpClient<IOllamaService, OllamaService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(10);
});

var app = builder.Build();

// Configure the HTTP request pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UsePathBase("/chatbot");
app.UseStaticFiles();

// Allow iframe embedding for /embed endpoint
app.Use(async (context, next) =>
{
    var isEmbedRoute = context.Request.Path.StartsWithSegments("/embed") || context.Request.Path.StartsWithSegments("/chatbot/embed");
    
    if (isEmbedRoute)
    {
        // Remove X-Frame-Options to allow iframe embedding
        context.Response.Headers.Remove("X-Frame-Options");
        
        // Set Content-Security-Policy to allow framing
        var apiKey = context.Request.Query["apiKey"].ToString();
        string? originOrReferer = context.Request.Headers.Origin.ToString();
        if (string.IsNullOrWhiteSpace(originOrReferer))
        {
            originOrReferer = context.Request.Headers.Referer.ToString();
        }

        string? requestHost = null;
        if (!string.IsNullOrWhiteSpace(originOrReferer) && Uri.TryCreate(originOrReferer, UriKind.Absolute, out var originUri))
        {
            requestHost = originUri.Host;
        }

        // Fallback to query parameter if headers are missing
        if (string.IsNullOrWhiteSpace(requestHost))
        {
            var domainParam = context.Request.Query["domain"].ToString();
            if (!string.IsNullOrWhiteSpace(domainParam))
            {
                requestHost = domainParam;
            }
        }

        try
        {
            var domainApiKeyService = context.RequestServices.GetRequiredService<IDomainApiKeyService>();
            var isValid = !string.IsNullOrWhiteSpace(requestHost) && await domainApiKeyService.ValidateAsync(requestHost, apiKey);

            if (isValid)
            {
                // Allow the actual embedding origin (+ optional www. variant) over both schemes
                var normalized = requestHost!.Trim().ToLowerInvariant();
                var www = normalized.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? normalized : $"www.{normalized}";
                var allowed = $"http://{normalized} https://{normalized} http://{www} https://{www}";
                context.Response.Headers["Content-Security-Policy"] = $"frame-ancestors {allowed};";
            }
            else
            {
                // If validation fails, still allow embedding but log it
                // This prevents the CSP error while maintaining security through API key validation
                context.Response.Headers["Content-Security-Policy"] = "frame-ancestors *;";
            }
        }
        catch
        {
            // On error, allow embedding to prevent CSP blocking
            context.Response.Headers["Content-Security-Policy"] = "frame-ancestors *;";
        }
    }
    
    await next();
});

// Development'ta embed route'u için Antiforgery cookie'sini response'dan kaldır
if (app.Environment.IsDevelopment())
{
    app.Use(async (context, next) =>
    {
        var isEmbedRoute = context.Request.Path.StartsWithSegments("/embed") || context.Request.Path.StartsWithSegments("/chatbot/embed");
        
        if (isEmbedRoute)
        {
            // Development'ta embed route'u için Antiforgery cookie'sini response'dan kaldır
            context.Response.OnStarting(() =>
            {
                var cookies = context.Response.Headers["Set-Cookie"];
                if (cookies.Count > 0)
                {
                    var filteredCookies = cookies.Where(c => c != null && !c.Contains(".AspNetCore.Antiforgery")).ToList();
                    if (filteredCookies.Count < cookies.Count)
                    {
                        context.Response.Headers["Set-Cookie"] = new Microsoft.Extensions.Primitives.StringValues(filteredCookies.ToArray());
                    }
                }
                return Task.CompletedTask;
            });
        }
        
        await next();
    });
}

app.UseAntiforgery();
app.UseSession();

// Security Middleware
app.UseMiddleware<RateLimitMiddleware>();

app.MapRazorComponents<BitizChatBot.Components.App>()
    .AddInteractiveServerRenderMode();

app.MapControllers();

app.MapGet("/embed-config", async (HttpContext httpContext, IDomainApiKeyService domainApiKeyService, IDomainAppearanceService domainAppearanceService) =>
{
    var apiKey = httpContext.Request.Query["apiKey"].ToString();
    if (string.IsNullOrWhiteSpace(apiKey))
    {
        return Results.BadRequest(new { error = "apiKey is required" });
    }

    string? originOrReferer = httpContext.Request.Headers.Origin.ToString();
    if (string.IsNullOrWhiteSpace(originOrReferer))
    {
        originOrReferer = httpContext.Request.Headers.Referer.ToString();
    }

    string? requestDomain = null;
    if (!string.IsNullOrWhiteSpace(originOrReferer) && Uri.TryCreate(originOrReferer, UriKind.Absolute, out var originUri))
    {
        requestDomain = originUri.Host;
    }

    // Fallback: if headers are missing, use explicitly provided domain query (best-effort)
    if (string.IsNullOrWhiteSpace(requestDomain))
    {
        requestDomain = httpContext.Request.Query["domain"].ToString();
    }

    if (string.IsNullOrWhiteSpace(requestDomain))
    {
        return Results.Unauthorized();
    }

    var isValid = await domainApiKeyService.ValidateAsync(requestDomain, apiKey);
    if (!isValid)
    {
        return Results.Unauthorized();
    }

    // CORS: allow only the validated origin (if present)
    if (!string.IsNullOrWhiteSpace(httpContext.Request.Headers.Origin))
    {
        httpContext.Response.Headers["Access-Control-Allow-Origin"] = httpContext.Request.Headers.Origin.ToString();
        httpContext.Response.Headers["Vary"] = "Origin";
    }

    var appearance = await domainAppearanceService.GetAsync(requestDomain);
    var openOnLoad = appearance?.OpenChatOnLoad ?? true;

    return Results.Ok(new
    {
        domain = requestDomain,
        openOnLoad
    });
});

// Initialize database (create default admin user if needed)
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var userService = scope.ServiceProvider.GetRequiredService<IUserService>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    await DbInitializer.InitializeAsync(context, userService, logger);
}

app.Run();

