namespace ChatBot.Share.Constants;

/// <summary>
/// SignalR method name constants.
/// Server calls these to push data → Client listens to these exact names.
/// A typo here = silent failure, so always use these constants on BOTH sides.
/// </summary>
public static class HubMethods
{
    // ── Server → Client (Server calls these to push to browser) ──────────
    
    /// <summary>Fired for each streamed token chunk from the LLM.</summary>
    public const string ReceiveToken = "ReceiveToken";

    /// <summary>Fired once when the LLM finishes generating the full response.</summary>
    public const string ChatComplete = "ChatComplete";

    /// <summary>Fired if an error occurs during RAG or LLM processing.</summary>
    public const string ReceiveError = "ReceiveError";

    // ── Client → Server (Client invokes these on the Hub) ─────────────────

    /// <summary>Client sends a chat message to the server to trigger RAG + LLM.</summary>
    public const string SendMessage = "SendMessage";
}