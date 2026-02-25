using ChatBot.Share.Enums;

namespace ChatBot.Share.DTOs;

/// <summary>
/// Represents an uploaded document shown in the Rag.razor page document list.
/// </summary>
public class DocumentDto
{
    public Guid Id { get; set; }

    public string FileName { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty;

    /// <summary>File size in bytes.</summary>
    public long FileSizeBytes { get; set; }

    public DateTime UploadedAt { get; set; }

    public DocumentStatus Status { get; set; }

    /// <summary>How many text chunks this document was split into.</summary>
    public int ChunkCount { get; set; }

    /// <summary>If Status is Failed, this describes what went wrong.</summary>
    public string? ErrorMessage { get; set; }

    // ── Computed helpers for the UI ────────────────────────────────────────

    public string FileSizeDisplay => FileSizeBytes switch
    {
        < 1024 => $"{FileSizeBytes} B",
        < 1024 * 1024 => $"{FileSizeBytes / 1024.0:F1} KB",
        _ => $"{FileSizeBytes / (1024.0 * 1024):F1} MB"
    };

    public string StatusDisplay => Status switch
    {
        DocumentStatus.Uploading   => "⏫ Uploading...",
        DocumentStatus.Processing  => "⚙️ Processing...",
        DocumentStatus.Ready       => "✅ Ready",
        DocumentStatus.Failed      => "❌ Failed",
        _ => "Unknown"
    };
}