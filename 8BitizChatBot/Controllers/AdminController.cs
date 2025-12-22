using Microsoft.AspNetCore.Mvc;
using BitizChatBot.Models;
using BitizChatBot.Services;

namespace BitizChatBot.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AdminController : ControllerBase
{
    private readonly IAdminSettingsService _settingsService;
    private readonly ILogger<AdminController> _logger;

    public AdminController(IAdminSettingsService settingsService, ILogger<AdminController> logger)
    {
        _settingsService = settingsService;
        _logger = logger;
    }

    [HttpGet("settings")]
    public async Task<IActionResult> GetSettings()
    {
        try
        {
            var settings = await _settingsService.GetSettingsAsync();
            // Don't expose the API key in the response for security
            var safeSettings = new
            {
                settings.Id,
                settings.LlmProvider,
                settings.ModelName,
                ApiKey = settings.ApiKey.Length > 0 ? "***" : string.Empty,
                settings.SystemPrompt,
                settings.Temperature,
                settings.MaxTokens,
                settings.UpdatedAt
            };
            return Ok(safeSettings);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving admin settings");
            return StatusCode(500, new { error = "Failed to retrieve settings" });
        }
    }

    [HttpPost("settings")]
    public async Task<IActionResult> SaveSettings([FromBody] AdminSettings settings)
    {
        try
        {
            if (settings == null)
            {
                return BadRequest(new { error = "Settings are required" });
            }

            await _settingsService.SaveSettingsAsync(settings);
            return Ok(new { message = "Settings saved successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving admin settings");
            return StatusCode(500, new { error = "Failed to save settings" });
        }
    }
}

