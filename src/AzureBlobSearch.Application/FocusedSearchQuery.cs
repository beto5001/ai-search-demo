using System.Text.RegularExpressions;

namespace AzureBlobSearch.Application;

public sealed partial record FocusedSearchQuery(
    string Original,
    string LexicalQuery,
    string? Subject,
    string? Focus)
{
    public bool IsFocused => Subject is not null && Focus is not null;

    public static FocusedSearchQuery Parse(string query)
    {
        var normalized = query.Trim();
        var match = FocusPattern().Match(normalized);

        if (!match.Success)
        {
            return new FocusedSearchQuery(normalized, normalized, null, null);
        }

        var subject = match.Groups["subject"].Value.Trim();
        var focus = match.Groups["focus"].Value.Trim().TrimEnd('.', '?', '!');

        if (string.IsNullOrWhiteSpace(subject) || string.IsNullOrWhiteSpace(focus))
        {
            return new FocusedSearchQuery(normalized, normalized, null, null);
        }

        var lexicalSubject = subject.Contains(' ', StringComparison.Ordinal)
            ? $"\"{EscapePhrase(subject)}\""
            : subject;

        return new FocusedSearchQuery(
            normalized,
            $"{lexicalSubject} {focus}",
            subject,
            focus);
    }

    private static string EscapePhrase(string value) =>
        value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);

    [GeneratedRegex(
        @"^(?<subject>.+?)\s+(?:com\s+foco\s+(?:em|no|na|nos|nas)|foco\s+(?:em|no|na|nos|nas))\s+(?<focus>.+)$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex FocusPattern();
}
