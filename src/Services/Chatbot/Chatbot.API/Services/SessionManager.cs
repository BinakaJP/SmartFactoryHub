using System.Collections.Concurrent;
using Chatbot.API.Models;

namespace Chatbot.API.Services;

public sealed class SessionManager : ISessionManager
{
    private readonly ConcurrentDictionary<Guid, ChatSession> _sessions = new();
    private readonly int _timeoutMinutes;

    public SessionManager(IConfiguration config)
    {
        _timeoutMinutes = config.GetValue("Chatbot:SessionTimeoutMinutes", 30);
    }

    public ChatSession GetOrCreate(Guid? sessionId, string userId, string userRole)
    {
        PurgeExpired();

        if (sessionId.HasValue && _sessions.TryGetValue(sessionId.Value, out var existing))
        {
            existing.LastActivityAt = DateTime.UtcNow;
            return existing;
        }

        var session = new ChatSession
        {
            UserId = userId,
            UserRole = userRole
        };
        _sessions[session.SessionId] = session;
        return session;
    }

    public ChatSession? Get(Guid sessionId)
    {
        _sessions.TryGetValue(sessionId, out var session);
        return session;
    }

    public void Remove(Guid sessionId) => _sessions.TryRemove(sessionId, out _);

    private void PurgeExpired()
    {
        var cutoff = DateTime.UtcNow.AddMinutes(-_timeoutMinutes);
        foreach (var (key, session) in _sessions)
        {
            if (session.LastActivityAt < cutoff)
                _sessions.TryRemove(key, out _);
        }
    }
}
