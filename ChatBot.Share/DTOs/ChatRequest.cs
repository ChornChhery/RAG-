namespace ChatBot.Share.DTOs;

/// <summary>
/// What the client sends to the ChatHub when the user submits a question.
/// </summary>
public class ChatRequest
{
    /// <summary>The user's question text.</summary>
    public string Question { get; set; } = string.Empty;

    /// <summary>
    /// Optional: limit RAG context to a specific document.
    /// If null or empty, all documents are searched.
    /// </summary>
    public Guid? DocumentId { get; set; }

    /// <summary>How many top similar chunks to retrieve for context. Default 5.</summary>
    public int TopK { get; set; } = 5;

    /// <summary>Full conversation history so the LLM has multi-turn context.</summary>
    public List<ChatMessageDto> History { get; set; } = [];
}