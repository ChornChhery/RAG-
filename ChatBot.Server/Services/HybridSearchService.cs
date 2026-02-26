using ChatBot.Share.DTOs;

namespace ChatBot.Server.Services;

/// <summary>
/// Hybrid search combining Vector (cosine similarity) + BM25 (keyword) retrieval.
/// Uses weighted score fusion: final = α × vectorScore + (1 - α) × bm25Score
/// Default: 70% vector + 30% BM25 (configurable via appsettings.json).
///
/// All chunk data is read from EmbeddingCacheService (RAM) — zero DB calls at query time.
/// </summary>
public class HybridSearchService(
    EmbeddingCacheService cache,
    BM25Service bm25Service,
    IConfiguration configuration,
    ILogger<HybridSearchService> logger)
{
    private double VectorWeight => configuration.GetValue<double>("HybridSearch:VectorWeight", 0.7);
    private double Bm25Weight   => 1.0 - VectorWeight;
    private double MinThreshold => configuration.GetValue<double>("HybridSearch:MinSimilarityThreshold", 0.3);

    public async Task<List<DocumentChunkResult>> SearchAsync(
        float[] queryEmbedding,
        string query,
        int topK = 5,
        Guid? documentId = null)
    {
        var stats = cache.GetStats();
        logger.LogInformation(
            "Hybrid search — cache: {Chunks} chunks / {Docs} docs / {Mb:F1} MB | " +
            "VectorWeight: {V:F2} BM25Weight: {B:F2}",
            stats.TotalChunks, stats.TotalDocuments, stats.EstimatedMemoryMb,
            VectorWeight, Bm25Weight);

        // ── Step 1: Get all chunks from cache (RAM, no DB) ─────────────────
        var allChunks = await cache.GetAllChunksAsync();

        // Filter by document if requested
        if (documentId.HasValue)
            allChunks = allChunks.Where(c => c.DocumentId == documentId.Value).ToList();

        if (allChunks.Count == 0)
        {
            logger.LogWarning("Hybrid search: no cached chunks found.");
            return [];
        }

        // ── Step 2: Map to DocumentChunkResult for BM25 scoring ───────────
        var chunkResults = allChunks.Select(c => new DocumentChunkResult
        {
            DocumentId      = c.DocumentId,
            DocumentName    = c.DocumentName,
            ChunkText       = c.ChunkText,
            PageNumber      = c.PageNumber,
            SimilarityScore = 0
        }).ToList();

        // ── Step 3: Vector scores (cosine similarity, pure math in RAM) ────
        var vectorScores = allChunks
            .Select(c => CosineSimilarity(queryEmbedding, c.Embedding))
            .ToList();

        // Normalize vector scores to [0, 1]
        var maxVector = vectorScores.Count > 0 ? vectorScores.Max() : 1.0;
        if (maxVector > 0)
            vectorScores = vectorScores.Select(s => s / maxVector).ToList();

        // ── Step 4: BM25 scores (keyword matching in RAM) ──────────────────
        var bm25Scored = bm25Service.Score(chunkResults, query);

        // ── Step 5: Weighted fusion ────────────────────────────────────────
        var hybridResults = chunkResults
            .Select((chunk, i) =>
            {
                var vectorScore = vectorScores.ElementAtOrDefault(i);
                var bm25Score   = bm25Scored.ElementAtOrDefault(i).Score;
                var hybridScore = VectorWeight * vectorScore + Bm25Weight * bm25Score;

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
            "Hybrid search: {Count}/{Total} chunks above threshold {T}. " +
            "Top score: {Score:F4}",
            hybridResults.Count, allChunks.Count, MinThreshold,
            hybridResults.FirstOrDefault()?.SimilarityScore ?? 0);

        return hybridResults;
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