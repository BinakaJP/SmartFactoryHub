using Chatbot.API.Dtos;
using Chatbot.API.Models;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace Chatbot.API.Services;

public sealed class ChatService : IChatService
{
    private readonly Kernel _kernel;
    private readonly ILogger<ChatService> _logger;
    private readonly int _maxHistoryTurns;
    private readonly int _maxResponseTokens;
    private readonly bool _aiConfigured;

    private const string SystemPrompt = """
        You are an AI assistant for SmartFactory Hub, an industrial IoT monitoring platform.
        You help factory engineers and operators query equipment status, metrics, alerts, and predictive maintenance data.

        Guidelines:
        - Be concise and factual. Engineers need quick, actionable answers.
        - Always cite specific equipment IDs, metric values, and timestamps when available.
        - Use the available factory tools to answer questions — do not guess or fabricate data.
        - If a tool call fails, say so clearly and suggest the user check the relevant service.
        - For destructive actions (acknowledge alerts, update status), confirm the action was taken and include the affected ID.
        - If the user's role does not permit an action, explain politely and state what role is required.
        """;

    public ChatService(Kernel kernel, IConfiguration config, ILogger<ChatService> logger)
    {
        _kernel = kernel;
        _logger = logger;
        _maxHistoryTurns = config.GetValue("Chatbot:MaxHistoryTurns", 10);
        _maxResponseTokens = config.GetValue("Chatbot:MaxResponseTokens", 1000);
        _aiConfigured = !string.Equals(
            config.GetValue<string>("AiProvider"), "None", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<SendMessageResponse> SendMessageAsync(
        string message,
        ChatSession session,
        CancellationToken cancellationToken = default)
    {
        if (!_aiConfigured)
        {
            return new SendMessageResponse(
                session.SessionId,
                "AI service is not configured. Please set the AiProvider and corresponding API key in configuration.",
                [],
                DateTime.UtcNow);
        }

        // Seed system prompt on first message
        if (session.History.Count == 0)
            session.History.AddSystemMessage(SystemPrompt);

        session.History.AddUserMessage(message);
        TrimHistory(session.History);

        var settings = new OpenAIPromptExecutionSettings
        {
            FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(),
            MaxTokens = _maxResponseTokens
        };

        try
        {
            var chatCompletion = _kernel.GetRequiredService<IChatCompletionService>();
            var result = await chatCompletion.GetChatMessageContentAsync(
                session.History,
                settings,
                _kernel,
                cancellationToken);

            var reply = result.Content ?? string.Empty;
            session.History.Add(result);

            var toolsUsed = ExtractToolsUsed(session.History);

            _logger.LogInformation(
                "Chat response for session {SessionId}: {Length} chars, tools: [{Tools}]",
                session.SessionId, reply.Length, string.Join(", ", toolsUsed));

            return new SendMessageResponse(session.SessionId, reply, toolsUsed, DateTime.UtcNow);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling AI service for session {SessionId}", session.SessionId);
            // Remove the user message we just added so history stays clean
            if (session.History.Count > 0)
                session.History.RemoveAt(session.History.Count - 1);

            return new SendMessageResponse(
                session.SessionId,
                "I encountered an error while processing your request. Please try again.",
                [],
                DateTime.UtcNow);
        }
    }

    private void TrimHistory(ChatHistory history)
    {
        // Keep system message + last (maxTurns * 2) user+assistant messages
        var maxMessages = _maxHistoryTurns * 2 + 1;
        while (history.Count > maxMessages)
        {
            // Remove the oldest non-system message (index 1)
            if (history.Count > 1)
                history.RemoveAt(1);
            else
                break;
        }
    }

    private static string[] ExtractToolsUsed(ChatHistory history)
    {
        var tools = new HashSet<string>();
        foreach (var msg in history)
        {
            foreach (var item in msg.Items)
            {
                if (item is FunctionCallContent fcc)
                    tools.Add($"{fcc.PluginName}.{fcc.FunctionName}");
            }
        }
        return [.. tools];
    }
}
