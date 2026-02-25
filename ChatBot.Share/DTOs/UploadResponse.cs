namespace ChatBot.Share.DTOs;

/// <summary>
/// Server response after a file upload POST to DocumentsController.
/// </summary>
public class UploadResponse
{
    public bool Success { get; set; }

    /// <summary>The new document's ID if upload succeeded.</summary>
    public Guid? DocumentId { get; set; }

    /// <summary>The original filename.</summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>Human-readable result message.</summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>Error details if Success is false.</summary>
    public string? ErrorDetail { get; set; }

    // ── Static factory helpers ─────────────────────────────────────────────

    public static UploadResponse Ok(Guid documentId, string fileName) => new()
    {
        Success    = true,
        DocumentId = documentId,
        FileName   = fileName,
        Message    = $"'{fileName}' uploaded successfully. Embedding in progress..."
    };

    public static UploadResponse Fail(string fileName, string error) => new()
    {
        Success     = false,
        FileName    = fileName,
        Message     = $"Failed to upload '{fileName}'.",
        ErrorDetail = error
    };
}