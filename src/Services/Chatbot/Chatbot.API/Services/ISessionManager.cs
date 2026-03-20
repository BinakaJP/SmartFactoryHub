using Chatbot.API.Models;

namespace Chatbot.API.Services;

public interface ISessionManager
{
    ChatSession GetOrCreate(Guid? sessionId, string userId, string userRole);
    ChatSession? Get(Guid sessionId);
    void Remove(Guid sessionId);
}
