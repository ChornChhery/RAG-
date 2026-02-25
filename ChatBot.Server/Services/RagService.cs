using ChatBot.Share.DTOs;
using ChatBot.Share.Enums;
using Microsoft.Extensions.AI;

namespace ChatBot.Server.Services;

public class RagService(
    EmbeddingService embeddingService,
    VectorSearchService vectorSearch,
    IChatClient chatClient,
    ILogger<RagService> logger)
{
    /// <summary>
    /// Full RAG pipeline with LLM fallback:
    /// - If relevant chunks found → RAG answer (context + LLM)
    /// - If no chunks found → pure LLM answer
    /// Streams tokens back via the onToken callback.
    /// </summary>
    public async Task<List<DocumentChunkResult>> StreamAnswerAsync(
        ChatRequest request,
        Func<string, Task> onToken,
        CancellationToken cancellationToken = default)
    {
        List<DocumentChunkResult> chunks = [];

        // ── Step 1: Try RAG — embed question and search for relevant chunks ──
        try
        {
            logger.LogInformation("Embedding question for RAG search: {Q}", request.Question);
            var queryEmbedding = await embeddingService.EmbedQueryAsync(request.Question);

            chunks = await vectorSearch.SearchAsync(
                queryEmbedding, request.TopK, request.DocumentId);

            logger.LogInformation("Retrieved {Count} chunks from vector search", chunks.Count);
        }
        catch (Exception ex)
        {
            // If embedding/search fails, log and fall through to pure LLM
            logger.LogWarning(ex, "RAG search failed — falling back to pure LLM response");
            chunks = [];
        }

        // ── Step 2: Build the system prompt based on whether we have context ──
        string systemPrompt;

        if (chunks.Count > 0)
        {
            // RAG mode — include document context
            var context = string.Join("\n\n---\n\n",
                chunks.Select((c, i) =>
                    $"[Source {i + 1}: {c.DocumentName}, Page {c.PageNumber}]\n{c.ChunkText}"));

            systemPrompt = $"""
                You are a helpful assistant. Answer the user's question based on the document context provided below.
                If the context is relevant, use it to give a grounded answer and mention the source.
                If the context does not contain enough information, answer from your general knowledge and say so.

                DOCUMENT CONTEXT:
                {context}
                """;

            logger.LogInformation("Using RAG mode with {Count} chunks as context", chunks.Count);
        }
        else
        {
            // Pure LLM mode — no document context available
            systemPrompt = """
                You are a helpful assistant. Answer the user's question clearly and helpfully 
                using your general knowledge. No document context is available for this question.
                """;

            logger.LogInformation("No chunks found — using pure LLM mode");
        }

        // ── Step 3: Build message list ─────────────────────────────────────
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, systemPrompt)
        };

        // Add conversation history (last 10 turns)
        foreach (var h in request.History.TakeLast(10))
        {
            var role = h.Role == MessageRole.User ? ChatRole.User : ChatRole.Assistant;
            messages.Add(new ChatMessage(role, h.Content));
        }

        // Add the current question
        messages.Add(new ChatMessage(ChatRole.User, request.Question));

        // ── Step 4: Stream LLM response ────────────────────────────────────
        logger.LogInformation("Sending to LLM with {MsgCount} messages", messages.Count);

        try
        {
            await foreach (var update in chatClient.GetStreamingResponseAsync(
                messages, cancellationToken: cancellationToken))
            {
                var token = update.Text ?? string.Empty;
                if (!string.IsNullOrEmpty(token))
                    await onToken(token);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "LLM streaming failed");
            throw;
        }

        return chunks;
    }
}