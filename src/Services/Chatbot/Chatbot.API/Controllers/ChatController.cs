using Chatbot.API.Dtos;
using Chatbot.API.Services;
using Microsoft.AspNetCore.Mvc;

namespace Chatbot.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class ChatController : ControllerBase
{
    private readonly IChatService _chatService;
    private readonly ISessionManager _sessionManager;
    private readonly ILogger<ChatController> _logger;

    public ChatController(
        IChatService chatService,
        ISessionManager sessionManager,
        ILogger<ChatController> logger)
    {
        _chatService = chatService;
        _sessionManager = sessionManager;
        _logger = logger;
    }

    /// <summary>Send a chat message and receive a response.</summary>
    [HttpPost("message")]
    [ProducesResponseType(typeof(SendMessageResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SendMessage(
        [FromBody] SendMessageRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
            return BadRequest("Message cannot be empty.");

        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                     ?? User.FindFirst("sub")?.Value
                     ?? "anonymous";
        var userRole = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value
                       ?? User.FindFirst("role")?.Value
                       ?? "Viewer";

        var session = _sessionManager.GetOrCreate(request.SessionId, userId, userRole);

        var response = await _chatService.SendMessageAsync(request.Message, session, cancellationToken);
        return Ok(response);
    }

    /// <summary>Get the message history for a chat session.</summary>
    [HttpGet("session/{sessionId}/history")]
    [ProducesResponseType(typeof(SessionHistoryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult GetHistory(Guid sessionId)
    {
        var session = _sessionManager.Get(sessionId);
        if (session is null)
            return NotFound($"Session {sessionId} not found or has expired.");

        var messages = session.History
            .Where(m => m.Role != Microsoft.SemanticKernel.ChatCompletion.AuthorRole.System)
            .Select(m => new ChatMessageDto(m.Role.ToString(), m.Content ?? string.Empty))
            .ToArray();

        return Ok(new SessionHistoryResponse(sessionId, messages));
    }

    /// <summary>Delete a chat session and its history.</summary>
    [HttpDelete("session/{sessionId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult DeleteSession(Guid sessionId)
    {
        var session = _sessionManager.Get(sessionId);
        if (session is null)
            return NotFound($"Session {sessionId} not found or has expired.");

        _sessionManager.Remove(sessionId);
        return NoContent();
    }
}
