using Azure;
using Azure.Search.Documents;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Indexes.Models;
using Azure.Search.Documents.Models;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using AzureBlobSearch.Application;
using Microsoft.Extensions.Options;
using System.IO.Compression;

namespace AzureBlobSearch.Infrastructure;

public sealed class AzureDocumentService(
    BlobServiceClient blobServiceClient,
    SearchClient searchClient,
    SearchIndexClient searchIndexClient,
    SearchIndexerClient searchIndexerClient,
    IOptions<AzureServicesOptions> options)
    : IDocumentService, IBatchDocumentService, IDocumentSearchService, IReadinessService
{
    private readonly AzureServicesOptions _options = options.Value;

    public async Task<UploadAccepted> UploadAsync(
        UploadDocument document,
        string statusUrl,
        CancellationToken cancellationToken)
    {
        var accepted = await UploadBlobAsync(document, statusUrl, cancellationToken);
        await RunIndexerAsync(cancellationToken);
        return accepted;
    }

    public async Task<BatchAccepted> UploadBatchAsync(
        UploadBatch batch,
        string statusUrl,
        CancellationToken cancellationToken)
    {
        BatchUploadPolicy.ValidateArchive(batch.FileName, batch.Length);

        await using var archiveBuffer = new MemoryStream(
            capacity: checked((int)batch.Length));
        await batch.Content.CopyToAsync(archiveBuffer, cancellationToken);
        archiveBuffer.Position = 0;

        using var archive = new ZipArchive(archiveBuffer, ZipArchiveMode.Read, leaveOpen: false);
        var entries = archive.Entries
            .Where(entry => !BatchUploadPolicy.ShouldIgnore(entry.FullName, entry.Name))
            .ToArray();

        BatchUploadPolicy.ValidateEntryCount(entries.Length);

        long expandedBytes = 0;
        foreach (var entry in entries)
        {
            expandedBytes = checked(expandedBytes + entry.Length);
            BatchUploadPolicy.ValidateCompressionRatio(entry.CompressedLength, entry.Length);
        }

        BatchUploadPolicy.ValidateExpandedSize(expandedBytes);

        var batchId = Guid.NewGuid().ToString("N");
        var items = new List<BatchItemStatus>(entries.Length);

        foreach (var entry in entries)
        {
            try
            {
                var contentType = BatchUploadPolicy.GetContentType(entry.Name);
                await using var entryStream = entry.Open();
                var uploaded = await UploadBlobAsync(
                    new UploadDocument(entryStream, entry.Name, contentType, entry.Length),
                    "/api/documents/{documentId}/status",
                    cancellationToken);

                items.Add(new BatchItemStatus(
                    uploaded.FileName,
                    uploaded.DocumentId,
                    BatchItemState.Pending,
                    null));
            }
            catch (Exception exception)
                when (exception is DocumentValidationException or DocumentTooLargeException)
            {
                items.Add(new BatchItemStatus(
                    DocumentUploadPolicy.SanitizeFileName(entry.Name),
                    null,
                    BatchItemState.Rejected,
                    exception.Message));
            }
        }

        var uploadedCount = items.Count(item => item.DocumentId is not null);
        var rejectedCount = items.Count - uploadedCount;
        var batchState = uploadedCount > 0 ? BatchState.Indexing : BatchState.Failed;
        var batchStatus = new BatchStatus(
            batchId,
            DocumentUploadPolicy.SanitizeFileName(batch.FileName),
            batchState,
            items.Count,
            uploadedCount,
            0,
            rejectedCount,
            0,
            DateTimeOffset.UtcNow,
            items);

        await SaveBatchStatusAsync(batchStatus, cancellationToken);

        if (uploadedCount > 0)
        {
            try
            {
                await RunIndexerAsync(cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                var failedItems = items
                    .Select(item => item.State == BatchItemState.Pending
                        ? item with { State = BatchItemState.Failed, ErrorMessage = exception.Message }
                        : item)
                    .ToArray();
                batchStatus = batchStatus with
                {
                    State = BatchState.Failed,
                    Failed = uploadedCount,
                    Items = failedItems
                };
                await SaveBatchStatusAsync(batchStatus, cancellationToken);
            }
        }

        return new BatchAccepted(
            batchId,
            items.Count,
            uploadedCount,
            rejectedCount,
            batchStatus.State,
            statusUrl.Replace("{batchId}", batchId, StringComparison.Ordinal));
    }

    public async Task<BatchStatus> GetBatchStatusAsync(
        string batchId,
        CancellationToken cancellationToken)
    {
        var container = blobServiceClient.GetBlobContainerClient(_options.BatchContainerName);
        var blob = container.GetBlobClient($"{batchId}.json");

        BatchStatus current;
        try
        {
            var download = await blob.DownloadContentAsync(cancellationToken);
            current = download.Value.Content.ToObjectFromJson<BatchStatus>()
                ?? throw new InvalidDataException("O status persistido do lote é inválido.");
        }
        catch (RequestFailedException exception) when (exception.Status == 404)
        {
            throw new KeyNotFoundException("O lote solicitado não foi encontrado.");
        }

        if (current.State is BatchState.Indexed or BatchState.PartiallyFailed or BatchState.Failed)
        {
            return current;
        }

        var items = new List<BatchItemStatus>(current.Items.Count);
        foreach (var item in current.Items)
        {
            if (item.DocumentId is null || item.State is BatchItemState.Rejected or BatchItemState.Failed)
            {
                items.Add(item);
                continue;
            }

            var document = await GetStatusAsync(item.DocumentId, cancellationToken);
            items.Add(item with
            {
                State = document.State switch
                {
                    DocumentState.Indexed => BatchItemState.Indexed,
                    DocumentState.Failed => BatchItemState.Failed,
                    _ => BatchItemState.Pending
                },
                ErrorMessage = document.ErrorMessage
            });
        }

        var indexed = items.Count(item => item.State == BatchItemState.Indexed);
        var rejected = items.Count(item => item.State == BatchItemState.Rejected);
        var failed = items.Count(item => item.State == BatchItemState.Failed);
        var pending = items.Count(item => item.State == BatchItemState.Pending);
        var state = pending > 0
            ? BatchState.Indexing
            : indexed > 0 && rejected + failed > 0
                ? BatchState.PartiallyFailed
                : indexed > 0
                    ? BatchState.Indexed
                    : BatchState.Failed;

        var updated = current with
        {
            State = state,
            Indexed = indexed,
            Rejected = rejected,
            Failed = failed,
            Items = items
        };

        await SaveBatchStatusAsync(updated, cancellationToken);
        return updated;
    }

    public async Task<DocumentStatus> GetStatusAsync(
        string documentId,
        CancellationToken cancellationToken)
    {
        try
        {
            var statusOptions = new SearchOptions
            {
                Size = 1,
                Filter = $"DocumentId eq '{EscapeODataString(documentId)}'"
            };
            statusOptions.Select.Add("ChunkId");

            var indexedChunks = await searchClient.SearchAsync<SearchDocument>(
                "*",
                statusOptions,
                cancellationToken);

            await foreach (var _ in indexedChunks.Value.GetResultsAsync())
            {
                return new DocumentStatus(documentId, DocumentState.Indexed, null, null);
            }
        }
        catch (RequestFailedException exception) when (exception.Status == 404)
        {
            // O índice pode ainda estar sendo criado pelo bootstrap da aplicação.
        }

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

    public async Task<SearchPage> SearchAsync(
        string query,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        SearchRequestPolicy.Validate(query, page, pageSize);

        var focusedQuery = FocusedSearchQuery.Parse(query);
        var candidateCount = HybridSearchPolicy.GetCandidateCount(page, pageSize);
        var searchOptions = new SearchOptions
        {
            Size = candidateCount,
            QueryType = SearchQueryType.Simple,
            SearchMode = SearchMode.All
        };
        var vectorQuery = new VectorizableTextQuery(focusedQuery.Original)
        {
            KNearestNeighborsCount = candidateCount
        };
        vectorQuery.Fields.Add(SearchBootstrapper.VectorFieldName);
        searchOptions.VectorSearch = new VectorSearchOptions();
        searchOptions.VectorSearch.Queries.Add(vectorQuery);
        searchOptions.HighlightFields.Add("Content");
        searchOptions.Select.Add("DocumentId");
        searchOptions.Select.Add("FileName");
        searchOptions.Select.Add("Content");
        searchOptions.Select.Add("ContentType");
        searchOptions.Select.Add("Size");
        searchOptions.Select.Add("LastModified");

        var response = await searchClient.SearchAsync<SearchDocument>(
            focusedQuery.LexicalQuery,
            searchOptions,
            cancellationToken);

        var hitsByDocument = new Dictionary<string, SearchHit>(StringComparer.Ordinal);
        await foreach (var result in response.Value.GetResultsAsync())
        {
            var document = result.Document;
            var documentId = GetValue<string>(document, "DocumentId") ?? string.Empty;
            var highlights = result.Highlights is not null
                && result.Highlights.TryGetValue("Content", out var matchedFragments)
                ? SearchHighlightPolicy.OrderByMatchDensity(matchedFragments)
                : CreateSemanticSnippet(GetValue<string>(document, "Content"));

            if (hitsByDocument.TryGetValue(documentId, out var existing))
            {
                var mergedHighlights = SearchHighlightPolicy.OrderByMatchDensity(
                    existing.Highlights.Concat(highlights));
                hitsByDocument[documentId] = existing with { Highlights = mergedHighlights };
                continue;
            }

            hitsByDocument.Add(documentId, new SearchHit(
                documentId,
                GetValue<string>(document, "FileName") ?? "sem-nome",
                GetValue<string>(document, "ContentType"),
                GetValue<long?>(document, "Size"),
                GetValue<DateTimeOffset?>(document, "LastModified"),
                result.Score,
                highlights));
        }

        var uniqueHits = hitsByDocument.Values.ToArray();
        var relevantHits = HybridSearchPolicy.KeepRelevantResults(
            uniqueHits,
            hit => hit.Score);
        var hits = relevantHits
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToArray();

        return new SearchPage(
            focusedQuery.Original,
            page,
            pageSize,
            relevantHits.Count,
            hits,
            focusedQuery.Subject,
            focusedQuery.Focus);
    }

    public async Task CheckAsync(CancellationToken cancellationToken)
    {
        var container = blobServiceClient.GetBlobContainerClient(_options.ContainerName);
        var batchContainer = blobServiceClient.GetBlobContainerClient(_options.BatchContainerName);
        await container.GetPropertiesAsync(cancellationToken: cancellationToken);
        await batchContainer.GetPropertiesAsync(cancellationToken: cancellationToken);
        await searchIndexClient.GetIndexAsync(_options.IndexName, cancellationToken);
    }

    private async Task<UploadAccepted> UploadBlobAsync(
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

        return new UploadAccepted(
            documentId,
            safeFileName,
            statusUrl.Replace("{documentId}", documentId, StringComparison.Ordinal));
    }

    private async Task RunIndexerAsync(CancellationToken cancellationToken)
    {
        try
        {
            await searchIndexerClient.RunIndexerAsync(_options.IndexerName, cancellationToken);
        }
        catch (RequestFailedException exception) when (exception.Status == 409)
        {
            // Uma execução já está em andamento e também encontrará os blobs recém-enviados.
        }
    }

    private async Task SaveBatchStatusAsync(
        BatchStatus status,
        CancellationToken cancellationToken)
    {
        var container = blobServiceClient.GetBlobContainerClient(_options.BatchContainerName);
        var blob = container.GetBlobClient($"{status.BatchId}.json");
        await blob.UploadAsync(
            BinaryData.FromObjectAsJson(status),
            overwrite: true,
            cancellationToken);
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

        var targetType = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);

        if (targetType == typeof(DateTimeOffset))
        {
            if (value is DateTime dateTime)
            {
                return (T?)(object)new DateTimeOffset(dateTime);
            }

            if (value is string dateText
                && DateTimeOffset.TryParse(
                    dateText,
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.AssumeUniversal,
                    out var dateTimeOffset))
            {
                return (T?)(object)dateTimeOffset;
            }
        }

        return (T?)Convert.ChangeType(
            value,
            targetType,
            System.Globalization.CultureInfo.InvariantCulture);
    }

    private static string EscapeODataString(string value) =>
        value.Replace("'", "''", StringComparison.Ordinal);

    private static string[] CreateSemanticSnippet(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return [];
        }

        const int maximumLength = 320;
        var normalized = string.Join(
            " ",
            content.Split(
                [' ', '\r', '\n', '\t'],
                StringSplitOptions.RemoveEmptyEntries));

        return
        [
            normalized.Length <= maximumLength
                ? normalized
                : $"{normalized[..maximumLength].TrimEnd()}…"
        ];
    }
}
