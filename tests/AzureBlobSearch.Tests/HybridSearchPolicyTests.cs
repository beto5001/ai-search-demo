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

    [Fact]
    public void KeepRelevantResults_RemovesVectorOnlyTailAfterStrongHybridMatch()
    {
        double?[] scores = [0.033, 0.031, 0.0165, 0.015];

        var results = HybridSearchPolicy.KeepRelevantResults(scores, score => score);

        Assert.Equal([0.033, 0.031], results);
    }

    [Fact]
    public void KeepRelevantResults_LimitsSemanticOnlyResults()
    {
        double?[] scores = [0.018, 0.017, 0.016, 0.015, 0.014, 0.013];

        var results = HybridSearchPolicy.KeepRelevantResults(scores, score => score);

        Assert.Equal(5, results.Count);
    }
}
