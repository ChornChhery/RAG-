namespace ChatBot.Share.DTOs;

/// <summary>
/// A retrieved document chunk from the vector similarity search.
/// Sent to the client so the UI can show "Sources used" under an assistant answer.
/// </summary>
public class DocumentChunkResult
{
    public Guid DocumentId { get; set; }

    public string DocumentName { get; set; } = string.Empty;

    /// <summary>The actual text of this chunk used as context.</summary>
    public string ChunkText { get; set; } = string.Empty;

    /// <summary>Which page this chunk came from (for PDFs).</summary>
    public int PageNumber { get; set; }

    /// <summary>
    /// Cosine similarity score (0.0 – 1.0). 
    /// Higher = more relevant to the question.
    /// </summary>
    public double SimilarityScore { get; set; }
}