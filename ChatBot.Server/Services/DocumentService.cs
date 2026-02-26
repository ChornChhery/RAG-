using ChatBot.Server.Data;
using ChatBot.Server.Models;
using ChatBot.Share.DTOs;
using ChatBot.Share.Enums;
using Microsoft.EntityFrameworkCore;

namespace ChatBot.Server.Services;

public class DocumentService(ChatbotDbContext db, ILogger<DocumentService> logger)
{
    public async Task<List<DocumentDto>> GetAllAsync()
    {
        return await db.Documents
            .OrderByDescending(d => d.UploadedAt)
            .Select(d => new DocumentDto
            {
                Id              = d.Id,
                FileName        = d.FileName,
                ContentType     = d.ContentType,
                FileSizeBytes   = d.FileSizeBytes,
                UploadedAt      = d.UploadedAt,
                Status          = d.Status,
                ChunkCount      = d.Chunks.Count,
                ChunkingMethod  = d.Chunks.FirstOrDefault() != null ? d.Chunks.First().ChunkingMethod : "FixedSize",
                ErrorMessage    = d.ErrorMessage
            })
            .ToListAsync();
    }

    public async Task<DocumentDto?> GetByIdAsync(Guid id)
    {
        var d = await db.Documents
            .Include(x => x.Chunks)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (d is null) return null;

        return new DocumentDto
        {
            Id              = d.Id,
            FileName        = d.FileName,
            ContentType     = d.ContentType,
            FileSizeBytes   = d.FileSizeBytes,
            UploadedAt      = d.UploadedAt,
            Status          = d.Status,
            ChunkCount      = d.Chunks.Count,
            ChunkingMethod  = d.Chunks.FirstOrDefault() != null ? d.Chunks.First().ChunkingMethod : "FixedSize",
            ErrorMessage    = d.ErrorMessage
        };
    }

    public async Task<Document> CreateAsync(string fileName, string contentType, long fileSize)
    {
        var doc = new Document
        {
            FileName      = fileName,
            ContentType   = contentType,
            FileSizeBytes = fileSize,
            Status        = DocumentStatus.Uploading
        };
        db.Documents.Add(doc);
        await db.SaveChangesAsync();
        return doc;
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var doc = await db.Documents.FindAsync(id);
        if (doc is null) return false;

        db.Documents.Remove(doc);
        await db.SaveChangesAsync();
        logger.LogInformation("Deleted document {Id}", id);
        return true;
    }
}