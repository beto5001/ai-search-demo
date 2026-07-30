using AzureBlobSearch.Application;

namespace AzureBlobSearch.Tests;

public sealed class SearchHighlightPolicyTests
{
    [Fact]
    public void OrderByMatchDensity_PutsMostExplanatoryFragmentFirst()
    {
        string[] highlights =
        [
            "<em>Ajustada</em> uma permissão sem relação com o assunto.",
            "<em>Ajustes</em> em <em>Folha</em> de <em>Pagamento</em>."
        ];

        var result = SearchHighlightPolicy.OrderByMatchDensity(highlights);

        Assert.Equal(highlights[1], result[0]);
    }

    [Fact]
    public void OrderByMatchDensity_RemovesDuplicatesAndLimitsOutput()
    {
        string[] highlights = ["<em>um</em>", "<em>um</em>", "<em>dois</em>", "<em>três</em>"];

        var result = SearchHighlightPolicy.OrderByMatchDensity(highlights, maximum: 2);

        Assert.Equal(2, result.Count);
        Assert.Equal(2, result.Distinct(StringComparer.Ordinal).Count());
    }
}
