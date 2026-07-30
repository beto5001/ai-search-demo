namespace AzureBlobSearch.Application;

public static class SearchHighlightPolicy
{
    private const string EmphasisTag = "<em>";

    public static IReadOnlyList<string> OrderByMatchDensity(
        IEnumerable<string> highlights,
        int maximum = 3) =>
        highlights
            .Where(highlight => !string.IsNullOrWhiteSpace(highlight))
            .Distinct(StringComparer.Ordinal)
            .OrderByDescending(CountMatches)
            .Take(maximum)
            .ToArray();

    private static int CountMatches(string highlight)
    {
        var count = 0;
        var position = 0;

        while ((position = highlight.IndexOf(
                   EmphasisTag,
                   position,
                   StringComparison.OrdinalIgnoreCase)) >= 0)
        {
            count++;
            position += EmphasisTag.Length;
        }

        return count;
    }
}
