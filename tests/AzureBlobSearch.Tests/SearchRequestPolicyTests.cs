using AzureBlobSearch.Application;

namespace AzureBlobSearch.Tests;

public sealed class SearchRequestPolicyTests
{
    [Fact]
    public void Validate_AcceptsValidRequest()
    {
        SearchRequestPolicy.Validate("contrato", 1, 20);
    }

    [Theory]
    [InlineData("", 1, 20)]
    [InlineData("texto", 0, 20)]
    [InlineData("texto", 1, 0)]
    [InlineData("texto", 1, 101)]
    public void Validate_RejectsInvalidRequest(string query, int page, int pageSize)
    {
        Assert.Throws<SearchValidationException>(() =>
            SearchRequestPolicy.Validate(query, page, pageSize));
    }
}

