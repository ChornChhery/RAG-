using ChatBot.Share.DTOs;
using System.Text;
using System.Text.RegularExpressions;

namespace ChatBot.Server.Services;

/// <summary>
/// Multilingual BM25 (Best Match 25) keyword-based ranking service.
///
/// Supports:
///   - English   → word tokenization (space/punctuation split + stop words)
///   - Thai      → character n-gram tokenization (Thai has no word boundaries)
///   - Khmer     → character n-gram tokenization (Khmer has no word boundaries)
///   - Mixed     → auto-detects script per segment, applies correct strategy
///
/// Why n-grams for Thai/Khmer?
///   Thai and Khmer scripts do not use spaces between words. Splitting on
///   whitespace produces single giant tokens that break BM25 entirely.
///   Character n-grams (trigrams) slice text into overlapping windows of
///   N characters which capture sub-word patterns and work universally
///   without needing a language-specific word segmenter.
/// </summary>
public class BM25Service(ILogger<BM25Service> logger)
{
    // ── BM25 tuning parameters (industry standard defaults) ────────────────
    private const double K1 = 1.5;  // Term frequency saturation
    private const double B  = 0.75; // Length normalization

    // ── N-gram size for non-space-delimited scripts ────────────────────────
    private const int NgramSize = 3; // trigrams

    // ── Unicode ranges ─────────────────────────────────────────────────────
    // Thai:  U+0E00–U+0E7F
    // Khmer: U+1780–U+17FF (main block) + U+19E0–U+19FF (symbols)
    private static bool IsThai(char c)  => c >= '\u0E00' && c <= '\u0E7F';
    private static bool IsKhmer(char c) => (c >= '\u1780' && c <= '\u17FF')
                                        || (c >= '\u19E0' && c <= '\u19FF');
    private static bool IsLatinOrNumeric(char c) => c < '\u0250';

    // ── English stop words ─────────────────────────────────────────────────
    private static readonly HashSet<string> EnglishStopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "a","an","the","and","or","but","in","on","at","to","for","of","with","by",
        "from","is","was","are","were","been","be","have","has","had","do","does",
        "did","will","would","could","should","may","might","must","can","this",
        "that","these","those","which","who","whom","what","when","where","why",
        "how","all","each","as","if","not","no","so","up","out","about","into",
        "than","then","its","it","we","you","he","she","they","i","my","your",
        "our","their","am","been","being","just","also","more","very","only",
        "over","such","after","before","between","during","same","other"
    };

    // ── Thai stop words ────────────────────────────────────────────────────
    private static readonly HashSet<string> ThaiStopWords = new()
    {
        "และ","หรือ","แต่","ใน","บน","ที่","ของ","กับ","โดย","จาก","เป็น","มี",
        "ได้","จะ","ให้","ว่า","ไม่","นี้","นั้น","เขา","เธอ","เรา","คุณ","ฉัน",
        "พวกเขา","มัน","ซึ่ง","เมื่อ","ถ้า","แล้ว","ก็","ยัง","อยู่","ทำ","มา",
        "ไป","อีก","แบบ","ต้อง","อาจ","ทั้ง","ทุก","บาง","หลาย","อื่น","เอง"
    };

    // ── Khmer stop words ───────────────────────────────────────────────────
    private static readonly HashSet<string> KhmerStopWords = new()
    {
        "និង","ឬ","ប៉ុន្តែ","នៅ","លើ","ក្នុង","របស់","ជាមួយ","ដោយ","ពី","គឺ",
        "មាន","បាន","នឹង","ទៅ","ថា","មិន","នេះ","នោះ","គាត់","នាង","យើង",
        "អ្នក","ខ្ញុំ","ពួកគេ","វា","ដែល","នៅពេល","បើ","បន្ទាប់","ក៏","ទេ"
    };

    /// <summary>
    /// Scores a list of chunks against a query using BM25.
    /// Returns normalized scores between 0 and 1.
    /// </summary>
    public List<(DocumentChunkResult Chunk, double Score)> Score(
        List<DocumentChunkResult> chunks,
        string query)
    {
        if (chunks.Count == 0 || string.IsNullOrWhiteSpace(query))
            return chunks.Select(c => (c, 0.0)).ToList();

        logger.LogInformation(
            "BM25 multilingual scoring {Count} chunks for query: {Q}", chunks.Count, query);

        // Step 1: Tokenize all chunks using language-aware tokenizer
        var tokenizedChunks = chunks
            .Select(c => (Chunk: c, Tokens: TokenizeMultilingual(c.ChunkText)))
            .ToList();

        if (tokenizedChunks.All(t => t.Tokens.Count == 0))
        {
            logger.LogWarning("BM25: all chunks tokenized to empty — check text content");
            return chunks.Select(c => (c, 0.0)).ToList();
        }

        // Step 2: Corpus statistics
        var avgDocLength = tokenizedChunks.Average(t => (double)t.Tokens.Count);
        var idfScores    = ComputeIDF(tokenizedChunks.Select(t => t.Tokens).ToList());

        // Step 3: Tokenize query
        var queryTokens = TokenizeMultilingual(query);
        logger.LogInformation(
            "BM25 query tokens ({Count}): {Tokens}",
            queryTokens.Count,
            string.Join(", ", queryTokens.Take(10)));

        // Step 4: Score each chunk
        var rawScores = tokenizedChunks
            .Select(t => (
                Chunk: t.Chunk,
                Score: ComputeBM25Score(t.Tokens, queryTokens, idfScores, avgDocLength)
            ))
            .ToList();

        // Step 5: Normalize to [0, 1]
        var maxScore = rawScores.Max(r => r.Score);
        if (maxScore == 0)
            return rawScores.Select(r => (r.Chunk, 0.0)).ToList();

        var normalized = rawScores
            .Select(r => (r.Chunk, Score: r.Score / maxScore))
            .ToList();

        var aboveZero = normalized.Count(r => r.Score > 0);
        logger.LogInformation("BM25: {Hit}/{Total} chunks scored above 0", aboveZero, chunks.Count);

        return normalized;
    }

    // ── BM25 Core ──────────────────────────────────────────────────────────

    private static double ComputeBM25Score(
        List<string> docTokens,
        List<string> queryTokens,
        Dictionary<string, double> idfScores,
        double avgDocLength)
    {
        var docLength = docTokens.Count;
        var termFreqs = docTokens
            .GroupBy(t => t, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => (double)g.Count(), StringComparer.OrdinalIgnoreCase);

        double score = 0;
        foreach (var term in queryTokens.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!idfScores.TryGetValue(term, out var idf)) continue;
            if (!termFreqs.TryGetValue(term, out var tf))  continue;

            double numerator   = tf * (K1 + 1);
            double denominator = tf + K1 * (1 - B + B * docLength / avgDocLength);
            score += idf * (numerator / denominator);
        }

        return score;
    }

    private static Dictionary<string, double> ComputeIDF(List<List<string>> tokenizedDocs)
    {
        var n  = tokenizedDocs.Count;
        var df = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var tokens in tokenizedDocs)
        {
            foreach (var term in tokens.Distinct(StringComparer.OrdinalIgnoreCase))
                df[term] = df.GetValueOrDefault(term) + 1;
        }

        return df.ToDictionary(
            kvp => kvp.Key,
            kvp => Math.Log((n - kvp.Value + 0.5) / (kvp.Value + 0.5) + 1),
            StringComparer.OrdinalIgnoreCase);
    }

    // ── Multilingual Tokenizer ─────────────────────────────────────────────

    /// <summary>
    /// Language-aware tokenizer:
    ///   - Detects Thai / Khmer / Latin segments within the same string
    ///   - Applies word tokenization for Latin (English)
    ///   - Applies character n-gram tokenization for Thai and Khmer
    ///   - Handles mixed-language text (e.g. English + Thai in same sentence)
    /// </summary>
    public static List<string> TokenizeMultilingual(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return [];

        var tokens       = new List<string>();
        var segment      = new StringBuilder();
        var segmentScript = DetectScript(text[0]);

        void FlushSegment(ScriptType script)
        {
            var seg = segment.ToString().Trim();
            if (string.IsNullOrWhiteSpace(seg)) return;

            var segTokens = script switch
            {
                ScriptType.Thai  => NgramTokenize(seg, NgramSize, ThaiStopWords),
                ScriptType.Khmer => NgramTokenize(seg, NgramSize, KhmerStopWords),
                _                => WordTokenize(seg, EnglishStopWords)
            };

            tokens.AddRange(segTokens);
            segment.Clear();
        }

        foreach (var ch in text)
        {
            var charScript = DetectScript(ch);

            // Flush segment when script changes
            if (charScript != segmentScript && segment.Length > 0)
            {
                FlushSegment(segmentScript);
                segmentScript = charScript;
            }

            segment.Append(ch);
        }

        if (segment.Length > 0)
            FlushSegment(segmentScript);

        return tokens;
    }

    /// <summary>
    /// Word-based tokenizer for space-delimited scripts (English, etc.)
    /// Lowercases, removes short tokens and stop words.
    /// </summary>
    private static List<string> WordTokenize(string text, HashSet<string> stopWords)
    {
        return Regex.Split(text.ToLowerInvariant(), @"[^\p{L}\p{N}]+")
            .Where(t => t.Length > 2 && !stopWords.Contains(t))
            .ToList();
    }

    /// <summary>
    /// Character n-gram tokenizer for scripts without word boundaries (Thai, Khmer).
    /// Produces overlapping character windows of size n.
    ///
    /// Example with n=3 on Thai "สวัสดีครับ":
    ///   → ["สวั", "วัส", "ัสด", "สดี", "ดีค", "ีคร", "คร�", "รับ"]
    ///
    /// This allows BM25 to match sub-word patterns even without a word segmenter.
    /// </summary>
    private static List<string> NgramTokenize(string text, int n, HashSet<string> stopWords)
    {
        var cleaned = new string(
            text.Where(c => !char.IsWhiteSpace(c) && !char.IsPunctuation(c)).ToArray());

        if (cleaned.Length == 0) return [];

        // Text shorter than n-gram: return whole string if not a stop word
        if (cleaned.Length < n)
            return !stopWords.Contains(cleaned) ? [cleaned] : [];

        var ngrams = new List<string>();
        for (int i = 0; i <= cleaned.Length - n; i++)
        {
            var ngram = cleaned.Substring(i, n);
            if (!stopWords.Contains(ngram))
                ngrams.Add(ngram);
        }

        return ngrams;
    }

    // ── Script Detection ───────────────────────────────────────────────────

    public enum ScriptType { Latin, Thai, Khmer, Other }

    private static ScriptType DetectScript(char c)
    {
        if (IsThai(c))  return ScriptType.Thai;
        if (IsKhmer(c)) return ScriptType.Khmer;
        if (IsLatinOrNumeric(c) || char.IsWhiteSpace(c) || char.IsPunctuation(c))
            return ScriptType.Latin;
        return ScriptType.Other;
    }
}