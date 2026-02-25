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
    /// Full RAG pipeline:
    /// 1. Embed the user question
    /// 2. Search for relevant chunks
    /// 3. Build a grounded prompt
    /// 4. Stream LLM response token by token via the callback
    /// </summary>
    public async Task<List<DocumentChunkResult>> StreamAnswerAsync(
        ChatRequest request,
        Func<string, Task> onToken,
        CancellationToken cancellationToken = default)
    {
        // ── Step 1: Embed the question ─────────────────────────────────────
        logger.LogInformation("Embedding question: {Q}", request.Question);
        var queryEmbedding = await embeddingService.EmbedQueryAsync(request.Question);

        // ── Step 2: Retrieve relevant chunks ──────────────────────────────
        var chunks = await vectorSearch.SearchAsync(
            queryEmbedding, request.TopK, request.DocumentId);

        logger.LogInformation("Retrieved {Count} chunks", chunks.Count);

        // ── Step 3: Build context string ───────────────────────────────────
        var context = chunks.Count > 0
            ? string.Join("\n\n---\n\n",
                chunks.Select((c, i) =>
                    $"[Source {i + 1}: {c.DocumentName}, Page {c.PageNumber}]\n{c.ChunkText}"))
            : "No relevant document context found.";

        // ── Step 4: Build message list ─────────────────────────────────────
        var systemPrompt = $"""
            You are a helpful assistant that answers questions based on the provided document context.
            Always base your answers on the context below. If the context does not contain enough 
            information to answer, say so clearly.

            CONTEXT:
            {context}
            """;

        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, systemPrompt)
        };

        // Add conversation history (last 10 turns to stay within context window)
        foreach (var h in request.History.TakeLast(10))
        {
            var role = h.Role == MessageRole.User ? ChatRole.User : ChatRole.Assistant;
            messages.Add(new ChatMessage(role, h.Content));
        }

        // Add the current question
        messages.Add(new ChatMessage(ChatRole.User, request.Question));

        // ── Step 5: Stream LLM response ────────────────────────────────────
        await foreach (var update in chatClient.GetStreamingResponseAsync(
            messages, cancellationToken: cancellationToken))
        {
            var token = update.Text ?? string.Empty;
            if (!string.IsNullOrEmpty(token))
                await onToken(token);
        }

        return chunks;
    }
}