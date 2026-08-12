using AzureBlobSearch.Application;

namespace AzureBlobSearch.Tests;

public sealed class ChatRequestPolicyTests
{
    [Fact]
    public void ValidateAndTrim_KeepsOnlyLastEightMessages()
    {
        var history = Enumerable.Range(1, 10)
            .Select(number => new ChatTurn(ChatRole.User, $"Mensagem {number}"))
            .ToArray();

        var result = ChatRequestPolicy.ValidateAndTrim(new ChatRequest("Pergunta", history));

        Assert.Equal(8, result.Count);
        Assert.Equal("Mensagem 3", result[0].Content);
        Assert.Equal("Mensagem 10", result[^1].Content);
    }

    [Fact]
    public void ValidateAndTrim_RejectsOversizedQuestion()
    {
        var request = new ChatRequest(
            new string('x', ChatRequestPolicy.MaximumMessageLength + 1),
            []);

        Assert.Throws<ChatValidationException>(() => ChatRequestPolicy.ValidateAndTrim(request));
    }

    [Fact]
    public void CreateExcerpt_NormalizesAndTruncatesContent()
    {
        var content = $"  primeira\n\tsegunda   {new string('x', 2000)}";

        var result = ChatRequestPolicy.CreateExcerpt(content);

        Assert.StartsWith("primeira segunda", result, StringComparison.Ordinal);
        Assert.EndsWith("…", result, StringComparison.Ordinal);
        Assert.True(result.Length <= ChatRequestPolicy.MaximumExcerptLength + 1);
    }
}
