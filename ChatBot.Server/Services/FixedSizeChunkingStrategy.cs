namespace ChatBot.Server.Services;

/// <summary>
/// Fixed-size chunking strategy: splits text into fixed-size chunks with overlap.
/// Simple and fast, but may split sentences in the middle.
/// </summary>
public class FixedSizeChunkingStrategy : IChunkingStrategy
{
    private const int ChunkSize = 500;
    private const int ChunkOverlap = 100;

    public List<(string Text, int Index)> ChunkText(string text, int pageNumber)
    {
        var chunks = new List<(string, int)>();
        int index = 0;
        int start = 0;

        while (start < text.Length)
        {
            int end = Math.Min(start + ChunkSize, text.Length);
            string chunk = text[start..end].Trim();

            if (!string.IsNullOrWhiteSpace(chunk))
                chunks.Add((chunk, index++));

            start += ChunkSize - ChunkOverlap;
        }

        return chunks;
    }
}
