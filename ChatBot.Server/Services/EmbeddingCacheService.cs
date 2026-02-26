using ChatBot.Server.Data;
using ChatBot.Share.Enums;
using Microsoft.EntityFrameworkCore;
using System.Collections.Concurrent;
using System.Text.Json;

namespace ChatBot.Server.Services;

/// <summary>
/// In-memory cache for document chunk embeddings.
///
/// Strategy:
///   - Upload   → add only new document's chunks to cache (no full reload)
///   - Delete   → remove only that document's chunks from cache
///   - Restart  → cache is empty, first query triggers full load from DB
///   - Query    → always reads from RAM, never touches DB after first load
///
/// Memory estimate:
///   1,000  chunks × 1024 floats × 4 bytes ≈ 4 MB
///   10,000 chunks × 1024 floats × 4 bytes ≈ 40 MB
///   50,000 chunks × 1024 floats × 4 bytes ≈ 200 MB
/// </summary>
public class EmbeddingCacheService(
    IServiceScopeFactory scopeFactory,
    ILogger<EmbeddingCacheService> logger)
{
    // Thread-safe dictionary: chunkId → cached chunk
    private readonly ConcurrentDictionary<Guid, CachedChunk> _cache = new();

    // Track whether the cache has been fully loaded from DB at least once
    private bool _isLoaded = false;

    // Lock to prevent multiple simultaneous full-loads on startup
    private readonly SemaphoreSlim _loadLock = new(1, 1);

    // ── Public API ─────────────────────────────────────────────────────────

    /// <summary>
    /// Returns all cached chunks. Triggers a full DB load if cache is empty.
    /// After first load, always returns from RAM instantly.
    /// </summary>
    public async Task<List<CachedChunk>> GetAllChunksAsync()
    {
        if (!_isLoaded)
            await EnsureLoadedAsync();

        return [.. _cache.Values];
    }

    /// <summary>
    /// Called after a document finishes embedding (Status = Ready).
    /// Adds only the new document's chunks to the cache — no full reload needed.
    /// </summary>
    public async Task AddDocumentAsync(Guid documentId)
    {
        logger.LogInformation("Cache: adding chunks for new document {Id}", documentId);

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ChatbotDbContext>();

        var chunks = await db.DocumentChunks
            .Include(c => c.Document)
            .AsNoTracking()
            .Where(c => c.DocumentId == documentId &&
                        c.Document.Status == DocumentStatus.Ready)
            .ToListAsync();

        int added = 0;
        foreach (var chunk in chunks)
        {
            float[]? embedding = null;
            try
            {
                if (!string.IsNullOrWhiteSpace(chunk.EmbeddingJson))
                    embedding = JsonSerializer.Deserialize<float[]>(chunk.EmbeddingJson);
            }
            catch (Exception ex)
            {
                logger.LogWarning("Cache: failed to deserialize chunk {Id}: {Err}", chunk.Id, ex.Message);
            }

            if (embedding is null || embedding.Length == 0) continue;

            _cache[chunk.Id] = new CachedChunk
            {
                ChunkId      = chunk.Id,
                DocumentId   = chunk.DocumentId,
                DocumentName = chunk.Document.FileName,
                ChunkText    = chunk.ChunkText,
                PageNumber   = chunk.PageNumber,
                Embedding    = embedding
            };
            added++;
        }

        logger.LogInformation(
            "Cache: added {Added} chunks for document {Id}. Total cached: {Total}",
            added, documentId, _cache.Count);
    }

    /// <summary>
    /// Called when a document is deleted.
    /// Removes only that document's chunks — rest of cache is untouched.
    /// </summary>
    public void RemoveDocument(Guid documentId)
    {
        var toRemove = _cache.Values
            .Where(c => c.DocumentId == documentId)
            .Select(c => c.ChunkId)
            .ToList();

        foreach (var id in toRemove)
            _cache.TryRemove(id, out _);

        logger.LogInformation(
            "Cache: removed {Count} chunks for document {Id}. Total cached: {Total}",
            toRemove.Count, documentId, _cache.Count);
    }

    /// <summary>
    /// Returns cache statistics for logging/debugging.
    /// </summary>
    public CacheStats GetStats() => new()
    {
        TotalChunks     = _cache.Count,
        TotalDocuments  = _cache.Values.Select(c => c.DocumentId).Distinct().Count(),
        IsLoaded        = _isLoaded,
        EstimatedMemoryMb = _cache.Count * 1024 * 4 / (1024.0 * 1024.0)
    };

    // ── Internal full load ─────────────────────────────────────────────────

    /// <summary>
    /// Full load from DB — only runs once on first query after app start.
    /// Uses a semaphore to prevent multiple simultaneous loads.
    /// </summary>
    private async Task EnsureLoadedAsync()
    {
        await _loadLock.WaitAsync();
        try
        {
            // Double-check after acquiring lock (another thread may have loaded)
            if (_isLoaded) return;

            logger.LogInformation("Cache: first query detected — loading all chunks from DB...");

            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ChatbotDbContext>();

            var chunks = await db.DocumentChunks
                .Include(c => c.Document)
                .AsNoTracking()
                .Where(c => c.Document.Status == DocumentStatus.Ready)
                .ToListAsync();

            int loaded = 0, skipped = 0;
            foreach (var chunk in chunks)
            {
                float[]? embedding = null;
                try
                {
                    if (!string.IsNullOrWhiteSpace(chunk.EmbeddingJson))
                        embedding = JsonSerializer.Deserialize<float[]>(chunk.EmbeddingJson);
                }
                catch (Exception ex)
                {
                    logger.LogWarning("Cache: failed to deserialize chunk {Id}: {Err}", chunk.Id, ex.Message);
                    skipped++;
                    continue;
                }

                if (embedding is null || embedding.Length == 0)
                {
                    skipped++;
                    continue;
                }

                _cache[chunk.Id] = new CachedChunk
                {
                    ChunkId      = chunk.Id,
                    DocumentId   = chunk.DocumentId,
                    DocumentName = chunk.Document.FileName,
                    ChunkText    = chunk.ChunkText,
                    PageNumber   = chunk.PageNumber,
                    Embedding    = embedding
                };
                loaded++;
            }

            _isLoaded = true;

            var stats = GetStats();
            logger.LogInformation(
                "Cache: loaded {Loaded} chunks ({Skipped} skipped). " +
                "Documents: {Docs}. Estimated RAM: {Mb:F1} MB",
                loaded, skipped, stats.TotalDocuments, stats.EstimatedMemoryMb);
        }
        finally
        {
            _loadLock.Release();
        }
    }
    public async Task WarmUpAsync()
    {
        await EnsureLoadedAsync();
    }
}

/// <summary>
/// A single chunk stored in the embedding cache.
/// Embedding is already deserialized to float[] — no JSON parsing at query time.
/// </summary>
public class CachedChunk
{
    public Guid   ChunkId      { get; set; }
    public Guid   DocumentId   { get; set; }
    public string DocumentName { get; set; } = string.Empty;
    public string ChunkText    { get; set; } = string.Empty;
    public int    PageNumber   { get; set; }
    public float[] Embedding   { get; set; } = [];
}

/// <summary>Cache statistics for monitoring.</summary>
public class CacheStats
{
    public int    TotalChunks       { get; set; }
    public int    TotalDocuments    { get; set; }
    public bool   IsLoaded          { get; set; }
    public double EstimatedMemoryMb { get; set; }
}