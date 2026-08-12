using Azure.Search.Documents;
using Azure.Search.Documents.Models;
using AzureBlobSearch.Application;

namespace AzureBlobSearch.Infrastructure;

public sealed class AzureDocumentRetrievalService(SearchClient searchClient)
    : IDocumentRetrievalService
{
    private const int CandidateCount = 30;

    public async Task<IReadOnlyList<RetrievedChunk>> RetrieveAsync(
        string query,
        int maximumResults,
        CancellationToken cancellationToken)
    {
        var options = new SearchOptions
        {
            Size = CandidateCount,
            QueryType = SearchQueryType.Simple,
            SearchMode = SearchMode.Any
        };
        var vectorQuery = new VectorizableTextQuery(query)
        {
            KNearestNeighborsCount = CandidateCount
        };
        vectorQuery.Fields.Add(SearchBootstrapper.VectorFieldName);
        options.VectorSearch = new VectorSearchOptions();
        options.VectorSearch.Queries.Add(vectorQuery);
        options.Select.Add("DocumentId");
        options.Select.Add("FileName");
        options.Select.Add("Content");

        var response = await searchClient.SearchAsync<SearchDocument>(
            query,
            options,
            cancellationToken);
        var results = new List<RetrievedChunk>(maximumResults);

        await foreach (var result in response.Value.GetResultsAsync())
        {
            var documentId = GetString(result.Document, "DocumentId");
            var fileName = GetString(result.Document, "FileName");
            var content = GetString(result.Document, "Content");

            if (string.IsNullOrWhiteSpace(documentId)
                || string.IsNullOrWhiteSpace(fileName)
                || string.IsNullOrWhiteSpace(content))
            {
                continue;
            }

            results.Add(new RetrievedChunk(documentId, fileName, content, result.Score));
            if (results.Count >= maximumResults)
            {
                break;
            }
        }

        return results;
    }

    private static string GetString(SearchDocument document, string fieldName) =>
        document.TryGetValue(fieldName, out var value) ? value?.ToString() ?? string.Empty : string.Empty;
}
