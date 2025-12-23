using BitizChatBot.Services;
using BitizChatBot.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddControllers();

// Add Entity Framework
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));

// Register Services
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IAdminSettingsService, AdminSettingsService>();
builder.Services.AddSingleton<ITurkishLocationService, TurkishLocationService>();
builder.Services.AddScoped<IDomainApiKeyService, DomainApiKeyService>();
builder.Services.AddScoped<IDomainAppearanceService, DomainAppearanceService>();
builder.Services.AddScoped<ILlmService, LlmService>();
builder.Services.AddScoped<IExternalApiService, ExternalApiService>();
builder.Services.AddScoped<IChatOrchestrationService, ChatOrchestrationService>();
builder.Services.AddScoped<IOllamaService, OllamaService>();

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
app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<BitizChatBot.Components.App>()
    .AddInteractiveServerRenderMode();

app.MapControllers();

app.Run();

