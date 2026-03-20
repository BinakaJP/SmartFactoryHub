using Chatbot.API.Dtos;
using Chatbot.API.Models;

namespace Chatbot.API.Services;

public interface IChatService
{
    Task<SendMessageResponse> SendMessageAsync(
        string message,
        ChatSession session,
        CancellationToken cancellationToken = default);
}
