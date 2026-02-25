using System.Text.Json;
using ChatBot.Server.Data;
using ChatBot.Share.DTOs;
using ChatBot.Share.Enums;
using Microsoft.EntityFrameworkCore;

namespace ChatBot.Server.Services;

public class VectorSearchService(ChatbotDbContext db, ILogger<VectorSearchService> logger)
{
    /// <summary>
    /// Finds the top-K most similar chunks to the query embedding using cosine similarity.
    /// We fetch all Ready chunks (optionally filtered by documentId) and rank in C#.
    /// For SQL Server 2025 this could be replaced with VECTOR_DISTANCE() for better performance.
    /// </summary>
    public async Task<List<DocumentChunkResult>> SearchAsync(
        float[] queryEmbedding,
        int topK = 5,
        Guid? documentId = null)
    {
        // Fetch chunks for ready documents only
        var query = db.DocumentChunks
            .Include(c => c.Document)
            .Where(c => c.Document.Status == DocumentStatus.Ready);

        if (documentId.HasValue)
            query = query.Where(c => c.DocumentId == documentId.Value);

        var chunks = await query
            .Select(c => new
            {
                c.Id,
                c.DocumentId,
                c.Document.FileName,
                c.ChunkText,
                c.PageNumber,
                c.EmbeddingJson
            })
            .ToListAsync();

        logger.LogInformation("Searching {Count} chunks for similarity", chunks.Count);

        // Compute cosine similarity in memory
        var results = chunks
            .Select(c =>
            {
                float[]? embedding = null;
                try
                {
                    embedding = JsonSerializer.Deserialize<float[]>(c.EmbeddingJson);
                }
                catch { /* skip malformed */ }

                double score = embedding is not null
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
            .OrderByDescending(r => r.SimilarityScore)
            .Take(topK)
            .ToList();

        return results;
    }

    // ── Cosine similarity ──────────────────────────────────────────────────

    private static double CosineSimilarity(float[] a, float[] b)
    {
        if (a.Length != b.Length) return 0;

        double dot  = 0, normA = 0, normB = 0;
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