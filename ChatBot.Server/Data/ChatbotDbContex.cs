using ChatBot.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace ChatBot.Server.Data;

public class ChatbotDbContext(DbContextOptions<ChatbotDbContext> options) : DbContext(options)
{
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<DocumentChunk> DocumentChunks => Set<DocumentChunk>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ── Document ───────────────────────────────────────────────────────
        modelBuilder.Entity<Document>(entity =>
        {
            entity.HasKey(d => d.Id);
            entity.Property(d => d.FileName).IsRequired().HasMaxLength(512);
            entity.Property(d => d.ContentType).HasMaxLength(128);
            entity.Property(d => d.Status).HasConversion<string>();

            // One document → many chunks
            entity.HasMany(d => d.Chunks)
                  .WithOne(c => c.Document)
                  .HasForeignKey(c => c.DocumentId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // ── DocumentChunk ──────────────────────────────────────────────────
        modelBuilder.Entity<DocumentChunk>(entity =>
        {
            entity.HasKey(c => c.Id);
            entity.Property(c => c.ChunkText).IsRequired();

            // EmbeddingJson can be large — store as nvarchar(max)
            entity.Property(c => c.EmbeddingJson)
                  .HasColumnType("nvarchar(max)")
                  .IsRequired();

            // Index to fetch all chunks for a document efficiently
            entity.HasIndex(c => c.DocumentId);
        });
    }
}