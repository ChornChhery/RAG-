using ChatBot.Share.Enums;

namespace ChatBot.Share.DTOs;

/// <summary>
/// Represents a single message in the chat conversation.
/// Used by both the server to build history and the client to render the UI.
/// </summary>
public class ChatMessageDto
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    public MessageRole Role { get; set; }

    /// <summary>The full message content. On the client this may grow token-by-token during streaming.</summary>
    public string Content { get; set; } = string.Empty;

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>True while the assistant message is still being streamed in.</summary>
    public bool IsStreaming { get; set; } = false;

    /// <summary>Optional: which document(s) were used as context for this answer.</summary>
    public List<string> SourceDocuments { get; set; } = [];
}