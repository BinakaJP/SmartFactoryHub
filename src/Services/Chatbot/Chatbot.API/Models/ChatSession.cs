using Microsoft.SemanticKernel.ChatCompletion;

namespace Chatbot.API.Models;

public sealed class ChatSession
{
    public Guid SessionId { get; init; } = Guid.NewGuid();
    public string UserId { get; init; } = string.Empty;
    public string UserRole { get; init; } = "Viewer";
    public ChatHistory History { get; } = new();
    public DateTime LastActivityAt { get; set; } = DateTime.UtcNow;
}
