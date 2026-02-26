namespace ChatBot.Server.Evaluators;

/// <summary>
/// BLEU (Bilingual Evaluation Understudy) Score Evaluator.
///
/// What it measures:
///   How many word sequences (n-grams) from the GENERATED answer
///   also appear in the REFERENCE answer.
///   Focuses on PRECISION — did the generated answer use the right phrases?
///
/// Multilingual support:
///   - English → word tokenization (space split + stop words)
///   - Thai    → character trigrams (no word boundaries in Thai script)
///   - Khmer   → character trigrams (no word boundaries in Khmer script)
///   Uses BM25Service.TokenizeMultilingual() for consistent tokenization.
///
/// Score range: 0.0 → 1.0
///   > 0.7   Excellent — generated answer closely matches reference phrases
///   0.4-0.7 Good      — most key phrases present
///   0.2-0.4 Fair      — some overlap but significant differences
///   < 0.2   Poor      — generated answer misses most reference content
/// </summary>
public class BLEUEvaluator
{
    private const int MaxNgramSize = 4; // BLEU-4 is the industry standard

    public double Compute(string generated, string reference)
    {
        if (string.IsNullOrWhiteSpace(generated) || string.IsNullOrWhiteSpace(reference))
            return 0.0;

        var genTokens = ChatBot.Server.Services.BM25Service.TokenizeMultilingual(generated);
        var refTokens = ChatBot.Server.Services.BM25Service.TokenizeMultilingual(reference);

        if (genTokens.Count == 0 || refTokens.Count == 0)
            return 0.0;

        // Brevity penalty — penalize very short generated answers
        double bp = genTokens.Count >= refTokens.Count
            ? 1.0
            : Math.Exp(1.0 - (double)refTokens.Count / genTokens.Count);

        double logSum = 0;
        int    validN = 0;

        for (int n = 1; n <= Math.Min(MaxNgramSize, Math.Min(genTokens.Count, refTokens.Count)); n++)
        {
            double precision = ComputeNgramPrecision(genTokens, refTokens, n);
            if (precision > 0)
            {
                logSum += Math.Log(precision);
                validN++;
            }
        }

        if (validN == 0) return 0.0;

        return bp * Math.Exp(logSum / validN);
    }

    private static double ComputeNgramPrecision(List<string> generated, List<string> reference, int n)
    {
        var genNgrams = GetNgrams(generated, n);
        var refNgrams = GetNgrams(reference, n);

        if (genNgrams.Count == 0) return 0.0;

        var refCounts = refNgrams
            .GroupBy(g => g, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);

        var matchCounts  = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        int clippedMatches = 0;

        foreach (var ngram in genNgrams)
        {
            if (!refCounts.ContainsKey(ngram)) continue;
            matchCounts.TryGetValue(ngram, out int already);
            if (already < refCounts[ngram])
            {
                clippedMatches++;
                matchCounts[ngram] = already + 1;
            }
        }

        return (double)clippedMatches / genNgrams.Count;
    }

    private static List<string> GetNgrams(List<string> tokens, int n)
    {
        var ngrams = new List<string>();
        for (int i = 0; i <= tokens.Count - n; i++)
            ngrams.Add(string.Join(" ", tokens.Skip(i).Take(n)));
        return ngrams;
    }
}