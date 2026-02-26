namespace ChatBot.Server.Evaluators;

/// <summary>
/// F1 Token-Level Score Evaluator.
///
/// What it measures:
///   Balances PRECISION (no wrong tokens added) and
///   RECALL (no reference tokens missed).
///
///   Precision = matching tokens / total generated tokens
///   Recall    = matching tokens / total reference tokens
///   F1        = 2 × (Precision × Recall) / (Precision + Recall)
///
/// Multilingual support:
///   - English → word tokenization
///   - Thai    → character trigrams (approximation)
///   - Khmer   → character trigrams (approximation)
///   For Thai/Khmer: trust LLM Judge score more than F1.
///
/// Score range: 0.0 → 1.0
///   > 0.7   Excellent — right tokens, minimal extras or missing
///   0.5-0.7 Good      — mostly correct tokens
///   0.3-0.5 Fair      — partial token overlap
///   < 0.3   Poor      — very different from reference
///
/// Best use: standard QA metric — used in SQuAD benchmark.
/// Measures whether the right FACTS appear regardless of phrasing.
/// </summary>
public class F1Evaluator
{
    public double Compute(string generated, string reference)
    {
        if (string.IsNullOrWhiteSpace(generated) || string.IsNullOrWhiteSpace(reference))
            return 0.0;

        var genTokens = ChatBot.Server.Services.BM25Service.TokenizeMultilingual(generated);
        var refTokens = ChatBot.Server.Services.BM25Service.TokenizeMultilingual(reference);

        if (genTokens.Count == 0 || refTokens.Count == 0)
            return 0.0;

        var refCounts = refTokens
            .GroupBy(t => t, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);

        var matchCounts  = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        int commonTokens = 0;

        foreach (var token in genTokens)
        {
            if (!refCounts.ContainsKey(token)) continue;
            matchCounts.TryGetValue(token, out int already);
            if (already < refCounts[token])
            {
                commonTokens++;
                matchCounts[token] = already + 1;
            }
        }

        if (commonTokens == 0) return 0.0;

        double precision = (double)commonTokens / genTokens.Count;
        double recall    = (double)commonTokens / refTokens.Count;

        return 2.0 * (precision * recall) / (precision + recall);
    }
}