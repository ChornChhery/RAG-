using System.Text.Json;
using ChatBot.Server.Data;
using ChatBot.Server.Models;
using ChatBot.Share.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace ChatBot.Server.Services;

public class EmbeddingService(
    IServiceScopeFactory scopeFactory,
    IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator,
    EmbeddingCacheService cache,
    ILogger<EmbeddingService> logger)
{
    public async Task ProcessDocumentAsync(
        Guid documentId,
        Stream fileStream,
        string contentType,
        ChunkingStrategy strategy = ChunkingStrategy.FixedSize)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ChatbotDbContext>();

        var document = await db.Documents.FindAsync(documentId);
        if (document is null)
        {
            logger.LogWarning("Document {Id} not found for processing", documentId);
            return;
        }

        try
        {
            document.Status = DocumentStatus.Processing;
            await db.SaveChangesAsync();

            logger.LogInformation(
                "Starting processing for document {Id} ({Type}) strategy={Strategy}",
                documentId, contentType, strategy);

            // 1. Extract raw text
            var pages = ExtractPages(fileStream, contentType);
            logger.LogInformation("Extracted {Count} pages from document {Id}", pages.Count, documentId);

            if (pages.Count == 0 || pages.All(p => string.IsNullOrWhiteSpace(p.Text)))
                throw new InvalidOperationException("No text could be extracted from the document.");

            // 2. Chunk text
            var chunks = new List<DocumentChunk>();
            var chunkingStrategy = GetChunkingStrategy(strategy);

            foreach (var (text, pageNumber) in pages)
            {
                var pageChunks = chunkingStrategy.ChunkText(text, pageNumber);
                chunks.AddRange(pageChunks.Select(c => new DocumentChunk
                {
                    DocumentId     = documentId,
                    ChunkText      = c.Text,
                    PageNumber     = pageNumber,
                    ChunkIndex     = c.Index,
                    ChunkingMethod = strategy.ToString()
                }));
            }

            logger.LogInformation("Created {Count} chunks for document {Id}", chunks.Count, documentId);

            // 3. Generate embeddings in batches of 10
            const int batchSize = 10;
            for (int i = 0; i < chunks.Count; i += batchSize)
            {
                var batch  = chunks.Skip(i).Take(batchSize).ToList();
                var texts  = batch.Select(c => c.ChunkText).ToList();

                logger.LogInformation(
                    "Embedding batch {Batch}/{Total} for document {DocId}",
                    i / batchSize + 1,
                    (int)Math.Ceiling(chunks.Count / (double)batchSize),
                    documentId);

                var embeddings = await embeddingGenerator.GenerateAsync(texts);

                for (int j = 0; j < batch.Count; j++)
                {
                    var vector = embeddings[j].Vector.ToArray();
                    batch[j].EmbeddingJson = JsonSerializer.Serialize(vector);
                }
            }

            // 4. Save to DB
            await db.DocumentChunks.AddRangeAsync(chunks);
            document.Status = DocumentStatus.Ready;
            await db.SaveChangesAsync();

            logger.LogInformation(
                "Document {Id} fully processed: {Count} chunks saved",
                documentId, chunks.Count);

            // 5. ── Add to cache (partial update — no full reload) ──────────
            await cache.AddDocumentAsync(documentId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to process document {Id}", documentId);

            var doc = await db.Documents.FindAsync(documentId);
            if (doc is not null)
            {
                doc.Status       = DocumentStatus.Failed;
                doc.ErrorMessage = ex.Message;
                await db.SaveChangesAsync();
            }
        }
    }

    // ── Embed a single query string ────────────────────────────────────────
    public async Task<float[]> EmbedQueryAsync(string query)
    {
        logger.LogInformation("Embedding query: {Q}", query);
        var result = await embeddingGenerator.GenerateAsync([query]);
        var vector = result[0].Vector.ToArray();
        logger.LogInformation("Query embedded — vector length: {Len}", vector.Length);
        return vector;
    }

    // ── Chunking strategy selector ─────────────────────────────────────────
    private static IChunkingStrategy GetChunkingStrategy(ChunkingStrategy strategy) =>
        strategy switch
        {
            ChunkingStrategy.ContentAware => new ContentAwareChunkingStrategy(),
            ChunkingStrategy.Semantic     => new SemanticChunkingStrategy(),
            _                             => new FixedSizeChunkingStrategy()
        };

    // ── Text extraction ────────────────────────────────────────────────────
    private static List<(string Text, int PageNumber)> ExtractPages(Stream stream, string contentType)
    {
        if (contentType.Contains("pdf", StringComparison.OrdinalIgnoreCase))
            return ExtractFromPdf(stream);

        using var reader = new StreamReader(stream);
        var text = reader.ReadToEnd();
        return [(text, 1)];
    }

    private static List<(string Text, int PageNumber)> ExtractFromPdf(Stream stream)
    {
        var result = new List<(string, int)>();
        using var pdf = PdfDocument.Open(stream);
        foreach (Page page in pdf.GetPages())
        {
            var text = string.Join(" ", page.GetWords().Select(w => w.Text));
            if (!string.IsNullOrWhiteSpace(text))
                result.Add((text, page.Number));
        }
        return result;
    }
}