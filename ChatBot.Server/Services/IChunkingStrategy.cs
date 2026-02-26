namespace ChatBot.Server.Services;

/// <summary>
/// Interface for different text chunking strategies in the RAG pipeline.
/// </summary>
public interface IChunkingStrategy
{
    /// <summary>
    /// Chunks text into semantic units based on the strategy's algorithm.
    /// </summary>
    /// <param name="text">The text to chunk.</param>
    /// <param name="pageNumber">The page number (for document tracking).</param>
    /// <returns>List of tuples containing chunk text and index.</returns>
    List<(string Text, int Index)> ChunkText(string text, int pageNumber);
}
