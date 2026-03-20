namespace Chatbot.API.Dtos;

public sealed record SendMessageRequest(
    string Message,
    Guid? SessionId = null
);

public sealed record SendMessageResponse(
    Guid SessionId,
    string Reply,
    string[] ToolsUsed,
    DateTime Timestamp
);

public sealed record ChatMessageDto(
    string Role,
    string Content,
    DateTime? Timestamp = null
);

public sealed record SessionHistoryResponse(
    Guid SessionId,
    ChatMessageDto[] Messages
);
