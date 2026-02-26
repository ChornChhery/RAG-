using ChatBot.Server.Services;
using ChatBot.Share.DTOs;
using ChatBot.Share.Enums;
using Microsoft.AspNetCore.Mvc;

namespace ChatBot.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DocumentsController(
    DocumentService documentService,
    EmbeddingService embeddingService,
    ILogger<DocumentsController> logger) : ControllerBase
{
    private static readonly string[] AllowedTypes =
    [
        "application/pdf",
        "text/plain",
        "text/markdown"
    ];

    // GET /api/documents
    [HttpGet]
    public async Task<ActionResult<List<DocumentDto>>> GetAll()
        => Ok(await documentService.GetAllAsync());

    // GET /api/documents/{id}
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<DocumentDto>> GetById(Guid id)
    {
        var doc = await documentService.GetByIdAsync(id);
        return doc is null ? NotFound() : Ok(doc);
    }

    // POST /api/documents/upload
    [HttpPost("upload")]
    [RequestSizeLimit(50 * 1024 * 1024)] // 50 MB max
    public async Task<ActionResult<UploadResponse>> Upload(IFormFile file, [FromQuery] ChunkingStrategy strategy = ChunkingStrategy.FixedSize)
    {
        if (file is null || file.Length == 0)
            return BadRequest(UploadResponse.Fail("unknown", "No file provided."));

        if (!AllowedTypes.Contains(file.ContentType))
            return BadRequest(UploadResponse.Fail(file.FileName,
                $"File type '{file.ContentType}' is not supported. Allowed: PDF, TXT, MD"));

        try
        {
            // 1. Create document record
            var doc = await documentService.CreateAsync(
                file.FileName, file.ContentType, file.Length);

            // 2. Process embedding in the background (don't await — return immediately)
            var fileBytes = new byte[file.Length];
            using var ms  = new MemoryStream(fileBytes);
            await file.CopyToAsync(ms);

            _ = Task.Run(async () =>
            {
                using var stream = new MemoryStream(fileBytes);
                await embeddingService.ProcessDocumentAsync(doc.Id, stream, file.ContentType, strategy);
            });

            logger.LogInformation("Upload accepted: {File} ({Id})", file.FileName, doc.Id);
            return Ok(UploadResponse.Ok(doc.Id, file.FileName));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Upload failed for {File}", file.FileName);
            return StatusCode(500, UploadResponse.Fail(file.FileName, ex.Message));
        }
    }

    // DELETE /api/documents/{id}
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var deleted = await documentService.DeleteAsync(id);
        return deleted ? NoContent() : NotFound();
    }
}