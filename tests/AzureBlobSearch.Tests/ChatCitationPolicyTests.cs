using AzureBlobSearch.Application;

namespace AzureBlobSearch.Tests;

public sealed class ChatCitationPolicyTests
{
    private static readonly ChatCitation[] Citations =
    [
        new(1, "doc", "release.txt", "Trecho", 0.03)
    ];

    [Fact]
    public void Validate_AcceptsKnownCitationsAndRemovesUnknownOnes()
    {
        var result = ChatCitationPolicy.Validate("Resposta [1] e referência inválida [9].", Citations);

        Assert.True(result.Grounded);
        Assert.Equal("Resposta [1] e referência inválida .", result.Answer);
    }

    [Fact]
    public void Validate_RefusesAnswerWithoutKnownCitation()
    {
        var result = ChatCitationPolicy.Validate("Uma resposta sem fonte.", Citations);

        Assert.False(result.Grounded);
        Assert.Equal(DocumentChatService.RefusalMessage, result.Answer);
    }
}
