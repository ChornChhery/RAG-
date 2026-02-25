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
    ChatbotDbContext db,
    IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator,
    ILogger<EmbeddingService> logger)
{
    // ── Chunking settings ──────────────────────────────────────────────────
    private const int ChunkSize    = 500;  // characters per chunk
    private const int ChunkOverlap = 100;  // overlap between chunks

    // ── Main entry point called after file upload ──────────────────────────

    public async Task ProcessDocumentAsync(Guid documentId, Stream fileStream, string contentType)
    {
        var document = await db.Documents.FindAsync(documentId);
        if (document is null) return;

        try
        {
            document.Status = DocumentStatus.Processing;
            await db.SaveChangesAsync();

            // 1. Extract raw text from the file
            var pages = ExtractPages(fileStream, contentType);

            // 2. Chunk and embed each page
            var chunks = new List<DocumentChunk>();
            foreach (var (text, pageNumber) in pages)
            {
                var pageChunks = ChunkText(text, pageNumber);
                chunks.AddRange(pageChunks.Select(c => new DocumentChunk
                {
                    DocumentId = documentId,
                    ChunkText  = c.Text,
                    PageNumber = pageNumber,
                    ChunkIndex = c.Index
                }));
            }

            // 3. Generate embeddings in batches of 20
            const int batchSize = 20;
            for (int i = 0; i < chunks.Count; i += batchSize)
            {
                var batch = chunks.Skip(i).Take(batchSize).ToList();
                var texts = batch.Select(c => c.ChunkText).ToList();

                var embeddings = await embeddingGenerator.GenerateAsync(texts);

                for (int j = 0; j < batch.Count; j++)
                {
                    batch[j].EmbeddingJson = JsonSerializer.Serialize(
                        embeddings[j].Vector.ToArray());
                }

                logger.LogInformation(
                    "Embedded batch {Batch}/{Total} for document {DocId}",
                    i / batchSize + 1, (int)Math.Ceiling(chunks.Count / (double)batchSize), documentId);
            }

            // 4. Save all chunks
            await db.DocumentChunks.AddRangeAsync(chunks);
            document.Status = DocumentStatus.Ready;
            await db.SaveChangesAsync();

            logger.LogInformation(
                "Document {DocId} processed: {Count} chunks", documentId, chunks.Count);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to process document {DocId}", documentId);

            var doc = await db.Documents.FindAsync(documentId);
            if (doc is not null)
            {
                doc.Status       = DocumentStatus.Failed;
                doc.ErrorMessage = ex.Message;
                await db.SaveChangesAsync();
            }
        }
    }

    // ── Embed a query string for vector search ─────────────────────────────

    public async Task<float[]> EmbedQueryAsync(string query)
    {
        var result = await embeddingGenerator.GenerateAsync([query]);
        return result[0].Vector.ToArray();
    }

    // ── Text extraction ────────────────────────────────────────────────────

    private static List<(string Text, int PageNumber)> ExtractPages(Stream stream, string contentType)
    {
        if (contentType.Contains("pdf"))
            return ExtractFromPdf(stream);

        // Plain text fallback
        using var reader = new StreamReader(stream);
        return [(reader.ReadToEnd(), 1)];
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

    // ── Text chunking ──────────────────────────────────────────────────────

    private static List<(string Text, int Index)> ChunkText(string text, int pageNumber)
    {
        var chunks = new List<(string, int)>();
        int index  = 0;
        int start  = 0;

        while (start < text.Length)
        {
            int end    = Math.Min(start + ChunkSize, text.Length);
            string chunk = text[start..end].Trim();

            if (!string.IsNullOrWhiteSpace(chunk))
                chunks.Add((chunk, index++));

            start += ChunkSize - ChunkOverlap;
        }

        return chunks;
    }
}