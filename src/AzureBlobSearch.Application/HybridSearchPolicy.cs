namespace AzureBlobSearch.Application;

public static class HybridSearchPolicy
{
    public const int MinimumCandidateCount = 50;
    public const int MaximumCandidateCount = 100;
    public const int MaximumVisibleResults = 10;
    public const int MaximumSemanticOnlyResults = 5;
    public const double HybridScoreBoundary = 0.025;
    public const double StrongResultRatio = 0.72;
    private const int CandidateMultiplier = 5;

    public static int GetCandidateCount(int page, int pageSize)
    {
        var requested = (long)page * pageSize * CandidateMultiplier;
        return (int)Math.Clamp(
            requested,
            MinimumCandidateCount,
            MaximumCandidateCount);
    }

    public static IReadOnlyList<T> KeepRelevantResults<T>(
        IReadOnlyList<T> orderedResults,
        Func<T, double?> scoreSelector)
    {
        if (orderedResults.Count == 0)
        {
            return [];
        }

        var topScore = scoreSelector(orderedResults[0]) ?? 0;
        if (topScore < HybridScoreBoundary)
        {
            return orderedResults.Take(MaximumSemanticOnlyResults).ToArray();
        }

        var minimumScore = topScore * StrongResultRatio;
        return orderedResults
            .Where(result => (scoreSelector(result) ?? 0) >= minimumScore)
            .Take(MaximumVisibleResults)
            .ToArray();
    }
}
