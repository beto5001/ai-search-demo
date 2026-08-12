namespace AzureBlobSearch.Application;

public static class ChatRequestPolicy
{
    public const int MaximumMessageLength = 2000;
    public const int MaximumHistoryMessages = 8;
    public const int MaximumRetrievedChunks = 5;
    public const int MaximumExcerptLength = 1600;

    public static IReadOnlyList<ChatTurn> ValidateAndTrim(ChatRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.Message))
        {
            throw new ChatValidationException("Digite uma pergunta para conversar com os arquivos.");
        }

        if (request.Message.Trim().Length > MaximumMessageLength)
        {
            throw new ChatValidationException(
                $"A pergunta deve ter no máximo {MaximumMessageLength} caracteres.");
        }

        if (request.History is null)
        {
            throw new ChatValidationException("O histórico da conversa é obrigatório.");
        }

        var history = request.History
            .Where(turn => !string.IsNullOrWhiteSpace(turn.Content))
            .TakeLast(MaximumHistoryMessages)
            .Select(turn => turn with
            {
                Content = turn.Content.Trim()[..Math.Min(turn.Content.Trim().Length, MaximumMessageLength)]
            })
            .ToArray();

        return history;
    }

    public static string CreateExcerpt(string content)
    {
        var normalized = string.Join(
            " ",
            content.Split(
                [' ', '\r', '\n', '\t'],
                StringSplitOptions.RemoveEmptyEntries));

        return normalized.Length <= MaximumExcerptLength
            ? normalized
            : $"{normalized[..MaximumExcerptLength].TrimEnd()}…";
    }
}

public sealed class ChatValidationException(string message) : Exception(message);
