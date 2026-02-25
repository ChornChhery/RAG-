namespace ChatBot.Share.DTOs;

/// <summary>
/// A single streamed chunk pushed from the server to the client via SignalR.
/// The client appends each token to the current assistant message content.
/// </summary>
public class StreamToken
{
    /// <summary>The partial text token from the LLM.</summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>True when this is the final token — streaming is done.</summary>
    public bool IsFinal { get; set; } = false;

    /// <summary>Unique ID matching the assistant message being built on the client.</summary>
    public string MessageId { get; set; } = string.Empty;
}