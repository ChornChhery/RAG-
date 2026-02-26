using ChatBot.Share.DTOs;
using ChatBot.Share.Enums;
using Microsoft.Extensions.AI;

namespace ChatBot.Server.Services;

public class RagService(
    EmbeddingService embeddingService,
    HybridSearchService hybridSearch,
    IChatClient chatClient,
    ILogger<RagService> logger)
{
    /// <summary>
    /// Full RAG pipeline with Hybrid Search (Vector + BM25) and LLM fallback:
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

        // ── Step 1: Hybrid search — embed question + BM25 keyword search ───
        try
        {
            logger.LogInformation("Embedding question for hybrid search: {Q}", request.Question);
            var queryEmbedding = await embeddingService.EmbedQueryAsync(request.Question);

            chunks = await hybridSearch.SearchAsync(
                queryEmbedding,
                request.Question,   // raw text for BM25
                request.TopK,
                request.DocumentId);

            logger.LogInformation("Hybrid search returned {Count} chunks", chunks.Count);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Hybrid search failed — falling back to pure LLM response");
            chunks = [];
        }

        // ── Step 2: Build system prompt ────────────────────────────────────
        string systemPrompt;

        if (chunks.Count > 0 && chunks[0].SimilarityScore >= 0.60)
        {
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

        foreach (var h in request.History.TakeLast(10))
        {
            var role = h.Role == MessageRole.User ? ChatRole.User : ChatRole.Assistant;
            messages.Add(new ChatMessage(role, h.Content));
        }

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