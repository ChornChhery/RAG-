namespace ChatBot.Share.DTOs;

/// <summary>
/// Request to evaluate RAG answer quality.
/// No reference answer needed — the system auto-generates one from retrieved chunks.
/// </summary>
public class EvaluationRequest
{
    /// <summary>The question to ask the RAG system.</summary>
    public string Question { get; set; } = string.Empty;

    /// <summary>
    /// Optional document ID to restrict search to a single document.
    /// If null, searches all documents.
    /// </summary>
    public Guid? DocumentId { get; set; }

    /// <summary>Number of chunks to retrieve (default 5).</summary>
    public int TopK { get; set; } = 5;
}