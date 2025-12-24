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
app.UseAntiforgery();
app.UseSession();

// Allow iframe embedding for /embed endpoint
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/embed"))
    {
        // Remove X-Frame-Options to allow iframe embedding from any domain
        context.Response.Headers.Remove("X-Frame-Options");
        // Set Content-Security-Policy to allow framing
        context.Response.Headers.Add("Content-Security-Policy", "frame-ancestors *;");
    }
    await next();
});

// Security Middleware
app.UseMiddleware<RateLimitMiddleware>();

app.MapRazorComponents<BitizChatBot.Components.App>()
    .AddInteractiveServerRenderMode();

app.MapControllers();

// Initialize database (create default admin user if needed)
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var userService = scope.ServiceProvider.GetRequiredService<IUserService>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    await DbInitializer.InitializeAsync(context, userService, logger);
}

app.Run();

