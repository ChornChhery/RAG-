namespace ChatBot.Server.Evaluators;

/// <summary>
/// GLEU (Google-BLEU) Score Evaluator.
///
/// What it measures:
///   A smoother variant of BLEU that penalizes BOTH:
///     - Generated content that doesn't appear in reference (bad precision)
///     - Reference content missing from generated answer (bad recall)
///   More balanced than BLEU — better for short answers and sentences.
///
/// Multilingual support:
///   - English → word tokenization
///   - Thai    → character trigrams
///   - Khmer   → character trigrams
///
/// Score range: 0.0 → 1.0
///   > 0.7   Excellent — answer is complete and accurate
///   0.4-0.7 Good      — mostly correct, minor omissions
///   0.2-0.4 Fair      — partially correct
///   < 0.2   Poor      — significant content missing or wrong
/// </summary>
public class GLEUEvaluator
{
    private const int MaxNgramSize = 4;

    public double Compute(string generated, string reference)
    {
        if (string.IsNullOrWhiteSpace(generated) || string.IsNullOrWhiteSpace(reference))
            return 0.0;

        var genTokens = ChatBot.Server.Services.BM25Service.TokenizeMultilingual(generated);
        var refTokens = ChatBot.Server.Services.BM25Service.TokenizeMultilingual(reference);

        if (genTokens.Count == 0 || refTokens.Count == 0)
            return 0.0;

        double totalPrecision = 0;
        double totalRecall    = 0;
        int    validN         = 0;

        for (int n = 1; n <= Math.Min(MaxNgramSize, Math.Min(genTokens.Count, refTokens.Count)); n++)
        {
            var (precision, recall) = ComputeNgramPrecisionRecall(genTokens, refTokens, n);
            if (precision > 0 || recall > 0)
            {
                totalPrecision += precision;
                totalRecall    += recall;
                validN++;
            }
        }

        if (validN == 0) return 0.0;

        double avgPrecision = totalPrecision / validN;
        double avgRecall    = totalRecall    / validN;

        // GLEU = min(precision, recall) — penalizes whichever is weaker
        return Math.Min(avgPrecision, avgRecall);
    }

    private static (double Precision, double Recall) ComputeNgramPrecisionRecall(
        List<string> generated, List<string> reference, int n)
    {
        var genNgrams = GetNgrams(generated, n);
        var refNgrams = GetNgrams(reference, n);

        if (genNgrams.Count == 0 || refNgrams.Count == 0) return (0, 0);

        var refCounts  = refNgrams
            .GroupBy(g => g, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);

        var matchCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        int matches = 0;

        foreach (var ngram in genNgrams)
        {
            if (!refCounts.ContainsKey(ngram)) continue;
            matchCounts.TryGetValue(ngram, out int already);
            if (already < refCounts[ngram])
            {
                matches++;
                matchCounts[ngram] = already + 1;
            }
        }

        return ((double)matches / genNgrams.Count, (double)matches / refNgrams.Count);
    }

    private static List<string> GetNgrams(List<string> tokens, int n)
    {
        var ngrams = new List<string>();
        for (int i = 0; i <= tokens.Count - n; i++)
            ngrams.Add(string.Join(" ", tokens.Skip(i).Take(n)));
        return ngrams;
    }
}