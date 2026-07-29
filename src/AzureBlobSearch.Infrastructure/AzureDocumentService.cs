using Azure;
using Azure.Search.Documents;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Indexes.Models;
using Azure.Search.Documents.Models;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using AzureBlobSearch.Application;
using Microsoft.Extensions.Options;

namespace AzureBlobSearch.Infrastructure;

public sealed class AzureDocumentService(
    BlobServiceClient blobServiceClient,
    SearchClient searchClient,
    SearchIndexClient searchIndexClient,
    SearchIndexerClient searchIndexerClient,
    IOptions<AzureServicesOptions> options)
    : IDocumentService, IDocumentSearchService, IReadinessService
{
    private readonly AzureServicesOptions _options = options.Value;

    public async Task<UploadAccepted> UploadAsync(
        UploadDocument document,
        string statusUrl,
        CancellationToken cancellationToken)
    {
        DocumentUploadPolicy.Validate(
            document.FileName,
            document.ContentType,
            document.Length,
            _options.MaximumUploadBytes);

        var documentId = Guid.NewGuid().ToString("N");
        var safeFileName = DocumentUploadPolicy.SanitizeFileName(document.FileName);
        var blobName = $"{DateTimeOffset.UtcNow:yyyy/MM/dd}/{documentId}/{safeFileName}";
        var container = blobServiceClient.GetBlobContainerClient(_options.ContainerName);
        var blob = container.GetBlobClient(blobName);

        await blob.UploadAsync(
            document.Content,
            new BlobUploadOptions
            {
                HttpHeaders = new BlobHttpHeaders { ContentType = document.ContentType },
                Metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["documentid"] = documentId,
                    ["originalfilename"] = safeFileName
                },
                Conditions = new BlobRequestConditions { IfNoneMatch = ETag.All }
            },
            cancellationToken);

        try
        {
            await searchIndexerClient.RunIndexerAsync(_options.IndexerName, cancellationToken);
        }
        catch (RequestFailedException exception) when (exception.Status == 409)
        {
            // Uma execução já está em andamento; ela também encontrará o blob recém-enviado.
        }

        return new UploadAccepted(
            documentId,
            safeFileName,
            statusUrl.Replace("{documentId}", documentId, StringComparison.Ordinal));
    }

    public async Task<DocumentStatus> GetStatusAsync(
        string documentId,
        CancellationToken cancellationToken)
    {
        try
        {
            await searchClient.GetDocumentAsync<SearchDocument>(
                documentId,
                cancellationToken: cancellationToken);

            return new DocumentStatus(documentId, DocumentState.Indexed, null, null);
        }
        catch (RequestFailedException exception) when (exception.Status == 404)
        {
            var indexerStatus = await searchIndexerClient.GetIndexerStatusAsync(
                _options.IndexerName,
                cancellationToken);
            var lastResult = indexerStatus.Value.LastResult;
            var statusText = lastResult?.Status.ToString();
            var failed = statusText?.Contains("Failure", StringComparison.OrdinalIgnoreCase) == true;
            var error = failed
                ? lastResult is not null && lastResult.Errors.Count > 0
                    ? lastResult.Errors[0].ErrorMessage
                    : "A última execução do indexador falhou."
                : null;

            return new DocumentStatus(
                documentId,
                failed ? DocumentState.Failed : DocumentState.Pending,
                error,
                lastResult?.EndTime ?? lastResult?.StartTime);
        }
    }

    public async Task<SearchPage> SearchAsync(
        string query,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        SearchRequestPolicy.Validate(query, page, pageSize);

        var searchOptions = new SearchOptions
        {
            IncludeTotalCount = true,
            Skip = (page - 1) * pageSize,
            Size = pageSize,
            QueryType = SearchQueryType.Simple,
            SearchMode = SearchMode.Any
        };
        searchOptions.HighlightFields.Add("content");
        searchOptions.Select.Add("id");
        searchOptions.Select.Add("fileName");
        searchOptions.Select.Add("contentType");
        searchOptions.Select.Add("size");
        searchOptions.Select.Add("lastModified");

        var response = await searchClient.SearchAsync<SearchDocument>(
            query.Trim(),
            searchOptions,
            cancellationToken);

        var hits = new List<SearchHit>();
        await foreach (var result in response.Value.GetResultsAsync())
        {
            var document = result.Document;
            hits.Add(new SearchHit(
                GetValue<string>(document, "id") ?? string.Empty,
                GetValue<string>(document, "fileName") ?? "sem-nome",
                GetValue<string>(document, "contentType"),
                GetValue<long?>(document, "size"),
                GetValue<DateTimeOffset?>(document, "lastModified"),
                result.Score,
                result.Highlights.TryGetValue("content", out var highlights)
                    ? highlights.ToArray()
                    : []));
        }

        return new SearchPage(
            query.Trim(),
            page,
            pageSize,
            response.Value.TotalCount ?? hits.Count,
            hits);
    }

    public async Task CheckAsync(CancellationToken cancellationToken)
    {
        var container = blobServiceClient.GetBlobContainerClient(_options.ContainerName);
        await container.GetPropertiesAsync(cancellationToken: cancellationToken);
        await searchIndexClient.GetIndexAsync(_options.IndexName, cancellationToken);
    }

    private static T? GetValue<T>(SearchDocument document, string name)
    {
        if (!document.TryGetValue(name, out var value) || value is null)
        {
            return default;
        }

        if (value is T typed)
        {
            return typed;
        }

        return (T?)Convert.ChangeType(
            value,
            Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T),
            System.Globalization.CultureInfo.InvariantCulture);
    }
}
