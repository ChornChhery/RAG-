using System.Text.Json;
using ChatBot.Server.Data;
using ChatBot.Share.DTOs;
using Microsoft.EntityFrameworkCore;

namespace ChatBot.Server.Services;

public class VectorSearchService(ChatbotDbContext db, ILogger<VectorSearchService> logger)
{
    // Minimum similarity score to be considered relevant (0.0 - 1.0)
    private const double MinSimilarityThreshold = 0.7;

    public async Task<List<DocumentChunkResult>> SearchAsync(
        float[] queryEmbedding,
        int topK = 5,
        Guid? documentId = null)
    {
        // Fetch all chunks — filter only by documentId if specified
        // NOTE: We do NOT filter by Status here via EF because Status is stored
        // as a string "Ready" in SQL but EF enum comparison can sometimes fail.
        // We filter in memory instead to be safe.
        var query = db.DocumentChunks
            .Include(c => c.Document)
            .AsNoTracking();

        if (documentId.HasValue)
            query = query.Where(c => c.DocumentId == documentId.Value);

        var chunks = await query
            .Select(c => new
            {
                c.Id,
                c.DocumentId,
                c.Document.FileName,
                c.Document.Status,
                c.ChunkText,
                c.PageNumber,
                c.EmbeddingJson
            })
            .ToListAsync();

        // Filter to only Ready documents in memory (avoids enum/string mismatch)
        var readyChunks = chunks
            .Where(c => c.Status == ChatBot.Share.Enums.DocumentStatus.Ready)
            .ToList();

        logger.LogInformation(
            "Searching {Count} chunks from Ready documents (total chunks in DB: {Total})",
            readyChunks.Count, chunks.Count);

        if (readyChunks.Count == 0)
        {
            logger.LogWarning("No Ready chunks found. Check document status in DB.");
            return [];
        }

        // Compute cosine similarity in memory
        var results = readyChunks
            .Select(c =>
            {
                float[]? embedding = null;
                try
                {
                    if (!string.IsNullOrWhiteSpace(c.EmbeddingJson))
                        embedding = JsonSerializer.Deserialize<float[]>(c.EmbeddingJson);
                }
                catch (Exception ex)
                {
                    logger.LogWarning("Failed to deserialize embedding for chunk {Id}: {Err}", c.Id, ex.Message);
                }

                double score = embedding is not null && embedding.Length > 0
                    ? CosineSimilarity(queryEmbedding, embedding)
                    : 0;

                return new DocumentChunkResult
                {
                    DocumentId      = c.DocumentId,
                    DocumentName    = c.FileName,
                    ChunkText       = c.ChunkText,
                    PageNumber      = c.PageNumber,
                    SimilarityScore = score
                };
            })
            .Where(r => r.SimilarityScore >= MinSimilarityThreshold)
            .OrderByDescending(r => r.SimilarityScore)
            .Take(topK)
            .ToList();

        logger.LogInformation(
            "Found {Count} chunks above similarity threshold {Threshold}",
            results.Count, MinSimilarityThreshold);

        if (results.Count > 0)
        {
            logger.LogInformation(
                "Top chunk score: {Score:F4} from '{Doc}'",
                results[0].SimilarityScore, results[0].DocumentName);
        }

        return results;
    }

    // ── Cosine similarity ──────────────────────────────────────────────────
    private static double CosineSimilarity(float[] a, float[] b)
    {
        if (a.Length != b.Length) return 0;

        double dot = 0, normA = 0, normB = 0;
        for (int i = 0; i < a.Length; i++)
        {
            dot   += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }

        double denom = Math.Sqrt(normA) * Math.Sqrt(normB);
        return denom == 0 ? 0 : dot / denom;
    }
}