namespace AzureBlobSearch.Application;

public sealed record UploadDocument(
    Stream Content,
    string FileName,
    string ContentType,
    long Length);

public sealed record UploadAccepted(
    string DocumentId,
    string FileName,
    string StatusUrl);

public enum DocumentState
{
    Pending,
    Indexed,
    Failed
}

public sealed record DocumentStatus(
    string DocumentId,
    DocumentState State,
    string? ErrorMessage,
    DateTimeOffset? LastIndexerRun);

public sealed record SearchHit(
    string DocumentId,
    string FileName,
    string? ContentType,
    long? Size,
    DateTimeOffset? LastModified,
    double? Score,
    IReadOnlyList<string> Highlights);

public sealed record SearchPage(
    string Query,
    int Page,
    int PageSize,
    long Total,
    IReadOnlyList<SearchHit> Items);

public interface IDocumentService
{
    Task<UploadAccepted> UploadAsync(
        UploadDocument document,
        string statusUrl,
        CancellationToken cancellationToken);

    Task<DocumentStatus> GetStatusAsync(
        string documentId,
        CancellationToken cancellationToken);
}

public interface IDocumentSearchService
{
    Task<SearchPage> SearchAsync(
        string query,
        int page,
        int pageSize,
        CancellationToken cancellationToken);
}

public interface IReadinessService
{
    Task CheckAsync(CancellationToken cancellationToken);
}

