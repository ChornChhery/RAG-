using ChatBot.Server.Data;
using ChatBot.Share.DTOs;
using ChatBot.Share.Enums;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace ChatBot.Server.Services;

/// <summary>
/// Hybrid search combining Vector (cosine similarity) + BM25 (keyword) retrieval.
/// Uses weighted score fusion: final = α × vectorScore + (1 - α) × bm25Score
/// Default: 70% vector + 30% BM25 (configurable via appsettings.json).
///
/// Why hybrid?
/// - Vector search: great for semantic meaning, paraphrasing, concepts
/// - BM25: great for exact keywords, names, codes, technical terms
/// - Together: covers both semantic and lexical retrieval gaps
/// </summary>
public class HybridSearchService(
    ChatbotDbContext db,
    BM25Service bm25Service,
    IConfiguration configuration,
    ILogger<HybridSearchService> logger)
{
    // Weight for vector score (BM25 weight = 1 - VectorWeight)
    private double VectorWeight => configuration.GetValue<double>("HybridSearch:VectorWeight", 0.7);
    private double Bm25Weight   => 1.0 - VectorWeight;

    // Minimum final hybrid score to be included in results
    private double MinThreshold => configuration.GetValue<double>("HybridSearch:MinSimilarityThreshold", 0.3);

    public async Task<List<DocumentChunkResult>> SearchAsync(
        float[] queryEmbedding,
        string query,
        int topK = 5,
        Guid? documentId = null)
    {
        logger.LogInformation(
            "Hybrid search — VectorWeight: {V:F2}, BM25Weight: {B:F2}, TopK: {K}",
            VectorWeight, Bm25Weight, topK);

        // ── Step 1: Load all Ready chunks from DB ──────────────────────────
        var dbQuery = db.DocumentChunks
            .Include(c => c.Document)
            .AsNoTracking();

        if (documentId.HasValue)
            dbQuery = dbQuery.Where(c => c.DocumentId == documentId.Value);

        var allChunks = await dbQuery
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

        // Filter to Ready documents only (in-memory to avoid EF enum/string issues)
        var readyChunks = allChunks
            .Where(c => c.Status == DocumentStatus.Ready)
            .ToList();

        logger.LogInformation(
            "Loaded {Ready} Ready chunks (total in DB: {Total})",
            readyChunks.Count, allChunks.Count);

        if (readyChunks.Count == 0)
        {
            logger.LogWarning("No Ready chunks found for hybrid search.");
            return [];
        }

        // ── Step 2: Map to DocumentChunkResult for scoring ─────────────────
        var chunkResults = readyChunks
            .Select(c => new DocumentChunkResult
            {
                DocumentId      = c.DocumentId,
                DocumentName    = c.FileName,
                ChunkText       = c.ChunkText,
                PageNumber      = c.PageNumber,
                SimilarityScore = 0
            })
            .ToList();

        // ── Step 3: Vector scoring ─────────────────────────────────────────
        var vectorScores = ComputeVectorScores(readyChunks
            .Select(c => (c.Id, c.EmbeddingJson))
            .ToList(), queryEmbedding);

        // ── Step 4: BM25 scoring ───────────────────────────────────────────
        var bm25Scored = bm25Service.Score(chunkResults, query);
        var bm25Dict   = bm25Scored
            .Select((item, i) => (Index: i, Score: item.Score))
            .ToDictionary(x => x.Index, x => x.Score);

        // ── Step 5: Weighted fusion ────────────────────────────────────────
        var hybridResults = chunkResults
            .Select((chunk, i) =>
            {
                var vectorScore = vectorScores.ElementAtOrDefault(i);
                var bm25Score   = bm25Dict.GetValueOrDefault(i, 0.0);
                var hybridScore = VectorWeight * vectorScore + Bm25Weight * bm25Score;

                logger.LogDebug(
                    "Chunk {I}: vector={V:F4}, bm25={B:F4}, hybrid={H:F4} — '{Doc}'",
                    i, vectorScore, bm25Score, hybridScore, chunk.DocumentName);

                return new DocumentChunkResult
                {
                    DocumentId      = chunk.DocumentId,
                    DocumentName    = chunk.DocumentName,
                    ChunkText       = chunk.ChunkText,
                    PageNumber      = chunk.PageNumber,
                    SimilarityScore = hybridScore
                };
            })
            .Where(r => r.SimilarityScore >= MinThreshold)
            .OrderByDescending(r => r.SimilarityScore)
            .Take(topK)
            .ToList();

        logger.LogInformation(
            "Hybrid search returned {Count} results above threshold {T}",
            hybridResults.Count, MinThreshold);

        if (hybridResults.Count > 0)
        {
            logger.LogInformation(
                "Top hybrid result: score={S:F4} from '{Doc}'",
                hybridResults[0].SimilarityScore, hybridResults[0].DocumentName);
        }

        return hybridResults;
    }

    // ── Vector scoring ─────────────────────────────────────────────────────

    /// <summary>
    /// Computes normalized cosine similarity scores for all chunks.
    /// Returns scores in the same order as input chunks.
    /// </summary>
    private List<double> ComputeVectorScores(
        List<(Guid Id, string EmbeddingJson)> chunks,
        float[] queryEmbedding)
    {
        var rawScores = chunks.Select(c =>
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

            return embedding is not null && embedding.Length > 0
                ? CosineSimilarity(queryEmbedding, embedding)
                : 0.0;
        }).ToList();

        // Normalize vector scores to [0, 1]
        var max = rawScores.Count > 0 ? rawScores.Max() : 1.0;
        if (max == 0) return rawScores;

        return rawScores.Select(s => s / max).ToList();
    }

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