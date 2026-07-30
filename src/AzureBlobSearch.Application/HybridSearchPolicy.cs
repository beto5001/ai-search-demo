namespace AzureBlobSearch.Application;

public static class HybridSearchPolicy
{
    public const int MinimumCandidateCount = 50;
    public const int MaximumCandidateCount = 100;
    private const int CandidateMultiplier = 5;

    public static int GetCandidateCount(int page, int pageSize)
    {
        var requested = (long)page * pageSize * CandidateMultiplier;
        return (int)Math.Clamp(
            requested,
            MinimumCandidateCount,
            MaximumCandidateCount);
    }
}
