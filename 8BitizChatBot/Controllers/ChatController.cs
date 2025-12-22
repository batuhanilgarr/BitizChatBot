using Microsoft.AspNetCore.Mvc;
using BitizChatBot.Services;

namespace BitizChatBot.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ChatController : ControllerBase
{
    private readonly IChatOrchestrationService _chatService;
    private readonly ILogger<ChatController> _logger;

    public ChatController(IChatOrchestrationService chatService, ILogger<ChatController> logger)
    {
        _chatService = chatService;
        _logger = logger;
    }

    [HttpPost("message")]
    public async Task<IActionResult> SendMessage([FromBody] ChatMessageRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request?.Message))
            {
                return BadRequest(new { error = "Message is required" });
            }

            var response = await _chatService.ProcessMessageAsync(request.Message);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing chat message");
            return StatusCode(500, new { error = "An error occurred while processing your message" });
        }
    }

    public class ChatMessageRequest
    {
        public string Message { get; set; } = string.Empty;
    }
}

