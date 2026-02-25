using ChatBot.Share.Enums;

namespace ChatBot.Server.Models;

/// <summary>
/// EF Core entity — stores metadata for each uploaded document.
/// SQL Table: Documents
/// </summary>
public class Document
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string FileName { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty;

    public long FileSizeBytes { get; set; }

    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

    public DocumentStatus Status { get; set; } = DocumentStatus.Uploading;

    public string? ErrorMessage { get; set; }

    // ── Navigation ─────────────────────────────────────────────────────────
    public ICollection<DocumentChunk> Chunks { get; set; } = [];
}