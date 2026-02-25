namespace ChatBot.Server.Models;

/// <summary>
/// EF Core entity — stores each text chunk and its embedding vector.
/// SQL Table: DocumentChunks
///
/// NOTE on vector storage:
/// If you are on SQL Server 2025 / Azure SQL, you can use the native VECTOR type.
/// For SQL Server 2019/2022 we store the embedding as a JSON string and 
/// compute cosine similarity in C# after fetching top candidates by document.
/// </summary>
public class DocumentChunk
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid DocumentId { get; set; }

    /// <summary>The raw text of this chunk sent to the embedding model.</summary>
    public string ChunkText { get; set; } = string.Empty;

    /// <summary>Which page this chunk was extracted from (PDF page number).</summary>
    public int PageNumber { get; set; }

    /// <summary>Position of this chunk within the document (0-based).</summary>
    public int ChunkIndex { get; set; }

    /// <summary>
    /// The embedding vector stored as a JSON float array.
    /// Example: "[0.123, -0.456, 0.789, ...]"
    /// We serialize/deserialize with System.Text.Json in VectorSearchService.
    /// </summary>
    public string EmbeddingJson { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // ── Navigation ─────────────────────────────────────────────────────────
    public Document Document { get; set; } = null!;
}