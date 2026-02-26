using System.Text.RegularExpressions;

namespace ChatBot.Server.Services;

/// <summary>
/// Semantic chunking strategy with multilingual support.
///
/// Language handling:
///   - English  → sentence splitting on .!? + uppercase lookahead
///   - Thai     → splits on Thai sentence-ending particles and punctuation (฿ ๆ ฯ \n)
///   - Khmer    → splits on Khmer sentence terminator (។) and newlines
///   - Mixed    → detects dominant script, applies appropriate splitter
///
/// Topic continuity is measured by keyword/n-gram overlap between adjacent sentences.
/// </summary>
public class SemanticChunkingStrategy : IChunkingStrategy
{
    private const int TargetChunkSize = 600;
    private const int MinChunkSize    = 150;
    private const int MaxChunkSize    = 1200;

    // English sentence boundary
    private static readonly Regex EnglishSentencePattern = new(
        @"(?<=[.!?])\s+(?=[A-Z])|(?<=[.!?])\n",
        RegexOptions.Compiled);

    // Thai sentence boundary: newline or ๆ/ฯ (repetition/abbreviation markers)
    private static readonly Regex ThaiSentencePattern = new(
        @"\n+|(?<=[\u0E46\u0E2F])\s*",
        RegexOptions.Compiled);

    // Khmer sentence boundary: ។ (Khmer full stop U+17D4) or newline
    private static readonly Regex KhmerSentencePattern = new(
        @"(?<=\u17D4)\s*|\n+",
        RegexOptions.Compiled);

    // Topic-change indicators (English)
    private static readonly string[] TopicBreakers =
    [
        "However,", "Nevertheless,", "On the other hand,", "In contrast,",
        "Despite", "Although", "Meanwhile,", "Furthermore,", "Additionally,",
        "Next,", "Finally,"
    ];

    public List<(string Text, int Index)> ChunkText(string text, int pageNumber)
    {
        var chunks = new List<(string, int)>();
        int index  = 0;

        var script    = DetectDominantScript(text);
        var sentences = script switch
        {
            ScriptType.Thai  => SplitSentences(text, ThaiSentencePattern),
            ScriptType.Khmer => SplitSentences(text, KhmerSentencePattern),
            _                => ExtractEnglishSentences(text)
        };

        if (sentences.Count == 0) return chunks;

        var semanticChunks = GroupSentencesBySemantic(sentences, script);

        foreach (var chunk in semanticChunks)
        {
            var chunkText = string.Join(script == ScriptType.Latin ? " " : "", chunk).Trim();
            if (!string.IsNullOrWhiteSpace(chunkText) && chunkText.Length >= MinChunkSize)
                chunks.Add((chunkText, index++));
        }

        return chunks;
    }

    // ── Sentence splitting ─────────────────────────────────────────────────

    private static List<string> ExtractEnglishSentences(string text)
    {
        text = Regex.Replace(text, @"\s+", " ");
        return EnglishSentencePattern.Split(text)
            .Select(s => s.Trim())
            .Where(s => !string.IsNullOrWhiteSpace(s) && s.Length > 5)
            .ToList();
    }

    private static List<string> SplitSentences(string text, Regex pattern)
    {
        return pattern.Split(text)
            .Select(s => s.Trim())
            .Where(s => !string.IsNullOrWhiteSpace(s) && s.Length > 2)
            .ToList();
    }

    // ── Semantic grouping ──────────────────────────────────────────────────

    private static List<List<string>> GroupSentencesBySemantic(
        List<string> sentences, ScriptType script)
    {
        var chunks       = new List<List<string>>();
        var currentChunk = new List<string>();
        var currentSize  = 0;

        for (int i = 0; i < sentences.Count; i++)
        {
            var sentence     = sentences[i];
            var sentenceSize = sentence.Length;

            bool isTopicBreak = script == ScriptType.Latin &&
                TopicBreakers.Any(b => sentence.StartsWith(b, StringComparison.OrdinalIgnoreCase));

            if (currentSize + sentenceSize > MaxChunkSize && currentChunk.Count > 0)
            {
                chunks.Add(currentChunk);
                currentChunk = [];
                currentSize  = 0;
            }

            if (isTopicBreak && currentChunk.Count > 0 && currentSize > MinChunkSize)
            {
                chunks.Add(currentChunk);
                currentChunk = [];
                currentSize  = 0;
            }

            currentChunk.Add(sentence);
            currentSize += sentenceSize + 1;

            if (currentSize >= TargetChunkSize && currentChunk.Count >= 2)
            {
                if (i + 1 < sentences.Count)
                {
                    var coherence = CalculateCoherence(sentence, sentences[i + 1], script);
                    if (coherence < 0.3)
                    {
                        chunks.Add(currentChunk);
                        currentChunk = [];
                        currentSize  = 0;
                    }
                }
                else
                {
                    chunks.Add(currentChunk);
                    currentChunk = [];
                    currentSize  = 0;
                }
            }
        }

        if (currentChunk.Count > 0)
            chunks.Add(currentChunk);

        return chunks;
    }

    // ── Coherence via token overlap ────────────────────────────────────────

    private static double CalculateCoherence(string s1, string s2, ScriptType script)
    {
        var t1 = BM25Service.TokenizeMultilingual(s1);
        var t2 = BM25Service.TokenizeMultilingual(s2);

        if (t1.Count == 0 || t2.Count == 0) return 0;

        var overlap  = t1.Intersect(t2, StringComparer.OrdinalIgnoreCase).Count();
        var maxWords = Math.Max(t1.Count, t2.Count);
        return (double)overlap / maxWords;
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