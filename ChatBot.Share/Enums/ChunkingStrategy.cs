namespace ChatBot.Share.Enums;

/// <summary>
/// Available text chunking strategies for document processing in RAG pipeline.
/// </summary>
public enum ChunkingStrategy
{
    /// <summary>
    /// Fixed-size chunks (500 chars) with 100 char overlap.
    /// Fast, simple, but may split sentences.
    /// </summary>
    FixedSize = 0,

    /// <summary>
    /// Content-aware chunking: splits on sentence boundaries, respects markdown structure.
    /// Better for maintaining semantic coherence within chunks.
    /// </summary>
    ContentAware = 1,

    /// <summary>
    /// Semantic chunking: maximizes semantic relatedness within chunks.
    /// Produces chunks that better preserve context and meaning.
    /// </summary>
    Semantic = 2
}
