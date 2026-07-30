using AzureBlobSearch.Application;

namespace AzureBlobSearch.Tests;

public sealed class FocusedSearchQueryTests
{
    [Theory]
    [InlineData(
        "folha de pagamento com foco em ajustes",
        "\"folha de pagamento\" ajustes",
        "folha de pagamento",
        "ajustes")]
    [InlineData(
        "PIX COM FOCO NAS correções?",
        "PIX correções",
        "PIX",
        "correções")]
    public void Parse_SeparatesSubjectAndFocus(
        string query,
        string expectedLexicalQuery,
        string expectedSubject,
        string expectedFocus)
    {
        var result = FocusedSearchQuery.Parse(query);

        Assert.True(result.IsFocused);
        Assert.Equal(expectedLexicalQuery, result.LexicalQuery);
        Assert.Equal(expectedSubject, result.Subject);
        Assert.Equal(expectedFocus, result.Focus);
    }

    [Fact]
    public void Parse_PreservesRegularQuery()
    {
        var result = FocusedSearchQuery.Parse("  ajustes folha de pagamento  ");

        Assert.False(result.IsFocused);
        Assert.Equal("ajustes folha de pagamento", result.Original);
        Assert.Equal(result.Original, result.LexicalQuery);
    }
}
