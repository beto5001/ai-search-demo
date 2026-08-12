using System.Text.RegularExpressions;

namespace AzureBlobSearch.Application;

public static partial class ChatCitationPolicy
{
    public static (string Answer, bool Grounded) Validate(
        string answer,
        IReadOnlyList<ChatCitation> citations)
    {
        if (answer.StartsWith(DocumentChatService.RefusalMessage, StringComparison.OrdinalIgnoreCase))
        {
            return (DocumentChatService.RefusalMessage, false);
        }

        var validIds = citations.Select(citation => citation.Id).ToHashSet();
        var matches = CitationPattern().Matches(answer);
        var hasValidCitation = matches.Any(match =>
            int.TryParse(
                match.Groups["id"].Value,
                System.Globalization.CultureInfo.InvariantCulture,
                out var id)
            && validIds.Contains(id));

        if (!hasValidCitation)
        {
            return (DocumentChatService.RefusalMessage, false);
        }

        var normalized = CitationPattern().Replace(answer, match =>
        {
            var parsed = int.TryParse(
                match.Groups["id"].Value,
                System.Globalization.CultureInfo.InvariantCulture,
                out var id);
            return parsed && validIds.Contains(id) ? match.Value : string.Empty;
        });

        return (normalized.Trim(), true);
    }

    [GeneratedRegex(@"\[(?<id>\d+)\]", RegexOptions.CultureInvariant)]
    private static partial Regex CitationPattern();
}
