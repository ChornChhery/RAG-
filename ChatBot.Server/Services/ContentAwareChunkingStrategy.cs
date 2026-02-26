using System.Text.RegularExpressions;

namespace ChatBot.Server.Services;

/// <summary>
/// Content-aware chunking with multilingual support.
///
/// Language handling:
///   - English / Markdown → splits on headings, then paragraphs (\n\n)
///   - Thai               → splits on newlines (no markdown headings expected)
///   - Khmer              → splits on Khmer full stop (។) and newlines
///   - Mixed              → heading detection + paragraph grouping works
///                          regardless of content language
///
/// Structure recognition:
///   - Markdown headings (## Title) treated as section boundaries
///   - Paragraph breaks (\n\n) used as soft chunk boundaries
///   - Chunks sized between MinChunkSize and MaxChunkSize characters
/// </summary>
public class ContentAwareChunkingStrategy : IChunkingStrategy
{
    private const int TargetChunkSize = 500;
    private const int MinChunkSize    = 100;
    private const int MaxChunkSize    = 1000;

    private static readonly Regex MarkdownHeadingPattern = new(
        @"^#{1,6}\s+.+$",
        RegexOptions.Multiline | RegexOptions.Compiled);

    // Khmer full stop U+17D4
    private static readonly Regex KhmerSentenceEnd = new(
        @"\u17D4\s*",
        RegexOptions.Compiled);

    public List<(string Text, int Index)> ChunkText(string text, int pageNumber)
    {
        var chunks = new List<(string, int)>();
        int index  = 0;

        var script = DetectDominantScript(text);

        // Split into top-level sections based on script
        var sections = script switch
        {
            ScriptType.Khmer => SplitByKhmerSentences(text),
            ScriptType.Thai  => SplitByNewlines(text),
            _                => SplitByMarkdownHeadings(text)
        };

        foreach (var section in sections)
        {
            var sectionChunks = ChunkSection(section);
            foreach (var chunk in sectionChunks)
            {
                if (!string.IsNullOrWhiteSpace(chunk))
                    chunks.Add((chunk.Trim(), index++));
            }
        }

        return chunks;
    }

    // ── Section splitters ──────────────────────────────────────────────────

    /// <summary>Splits on markdown headings, preserving heading text in section.</summary>
    private static List<string> SplitByMarkdownHeadings(string text)
    {
        var sections      = new List<string>();
        var lines         = text.Split('\n');
        var currentSection = new List<string>();

        foreach (var line in lines)
        {
            if (MarkdownHeadingPattern.IsMatch(line) && currentSection.Count > 0)
            {
                sections.Add(string.Join('\n', currentSection));
                currentSection = [line];
            }
            else
            {
                currentSection.Add(line);
            }
        }

        if (currentSection.Count > 0)
            sections.Add(string.Join('\n', currentSection));

        return sections.Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
    }

    /// <summary>Splits Khmer text on sentence terminator ។</summary>
    private static List<string> SplitByKhmerSentences(string text)
    {
        return KhmerSentenceEnd.Split(text)
            .Select(s => s.Trim())
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToList();
    }

    /// <summary>Splits Thai text on newlines (primary structural boundary).</summary>
    private static List<string> SplitByNewlines(string text)
    {
        return text.Split('\n')
            .Select(s => s.Trim())
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToList();
    }

    // ── Paragraph grouping ─────────────────────────────────────────────────

    private static List<string> ChunkSection(string section)
    {
        var chunks = new List<string>();

        var paragraphs = section
            .Split(new[] { "\n\n", "\r\n\r\n" }, StringSplitOptions.None)
            .Select(p => p.Trim())
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .ToList();

        if (paragraphs.Count == 0) return chunks;

        var currentChunk = new List<string>();
        var currentSize  = 0;

        foreach (var paragraph in paragraphs)
        {
            var paragraphSize = paragraph.Length;

            if (currentSize + paragraphSize > MaxChunkSize && currentChunk.Count > 0)
            {
                chunks.Add(string.Join("\n\n", currentChunk));
                currentChunk = [];
                currentSize  = 0;
            }

            currentChunk.Add(paragraph);
            currentSize += paragraphSize + 2;

            if (currentSize >= TargetChunkSize && currentChunk.Count > 0)
            {
                chunks.Add(string.Join("\n\n", currentChunk));
                currentChunk = [];
                currentSize  = 0;
            }
        }

        if (currentChunk.Count > 0)
            chunks.Add(string.Join("\n\n", currentChunk));

        return chunks.Where(c => c.Length >= MinChunkSize).ToList();
    }

    // ── Script detection ───────────────────────────────────────────────────

    private static ScriptType DetectDominantScript(string text)
    {
        int thai = 0, khmer = 0, latin = 0;
        foreach (var c in text)
        {
            if (c >= '\u0E00' && c <= '\u0E7F') thai++;
            else if (c >= '\u1780' && c <= '\u17FF') khmer++;
            else if (c < '\u0250') latin++;
        }

        if (thai > khmer && thai > latin)  return ScriptType.Thai;
        if (khmer > thai && khmer > latin) return ScriptType.Khmer;
        return ScriptType.Latin;
    }

    private enum ScriptType { Latin, Thai, Khmer }
}