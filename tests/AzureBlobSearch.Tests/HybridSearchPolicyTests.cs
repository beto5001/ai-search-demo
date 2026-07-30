using AzureBlobSearch.Application;

namespace AzureBlobSearch.Tests;

public sealed class HybridSearchPolicyTests
{
    [Theory]
    [InlineData(1, 10, 50)]
    [InlineData(1, 20, 100)]
    [InlineData(2, 10, 100)]
    [InlineData(int.MaxValue, 50, 100)]
    public void GetCandidateCount_ClampsCandidateWindow(
        int page,
        int pageSize,
        int expected)
    {
        var actual = HybridSearchPolicy.GetCandidateCount(page, pageSize);

        Assert.Equal(expected, actual);
    }
}
