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

public sealed record UploadBatch(
    Stream Content,
    string FileName,
    long Length);

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

public enum BatchState
{
    Uploading,
    Indexing,
    Indexed,
    PartiallyFailed,
    Failed
}

public enum BatchItemState
{
    Pending,
    Indexed,
    Rejected,
    Failed
}

public sealed record BatchItemStatus(
    string FileName,
    string? DocumentId,
    BatchItemState State,
    string? ErrorMessage);

public sealed record BatchStatus(
    string BatchId,
    string FileName,
    BatchState State,
    int Total,
    int Uploaded,
    int Indexed,
    int Rejected,
    int Failed,
    DateTimeOffset CreatedAt,
    IReadOnlyList<BatchItemStatus> Items);

public sealed record BatchAccepted(
    string BatchId,
    int Total,
    int Uploaded,
    int Rejected,
    BatchState State,
    string StatusUrl);

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
    IReadOnlyList<SearchHit> Items,
    string? Subject,
    string? Focus);

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

public interface IBatchDocumentService
{
    Task<BatchAccepted> UploadBatchAsync(
        UploadBatch batch,
        string statusUrl,
        CancellationToken cancellationToken);

    Task<BatchStatus> GetBatchStatusAsync(
        string batchId,
        CancellationToken cancellationToken);
}

public interface IReadinessService
{
    Task CheckAsync(CancellationToken cancellationToken);
}
