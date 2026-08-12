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

public enum ChatRole
{
    User,
    Assistant
}

public sealed record ChatTurn(
    ChatRole Role,
    string Content);

public sealed record ChatRequest(
    string Message,
    IReadOnlyList<ChatTurn> History);

public sealed record ChatCitation(
    int Id,
    string DocumentId,
    string FileName,
    string Excerpt,
    double? Score);

public sealed record ChatResponse(
    string Answer,
    string RetrievalQuery,
    bool Grounded,
    IReadOnlyList<ChatCitation> Citations);

public enum ChatStreamEventType
{
    Status,
    Sources,
    Token,
    Completed
}

public sealed record ChatStreamEvent(
    ChatStreamEventType Type,
    string? Text = null,
    string? RetrievalQuery = null,
    IReadOnlyList<ChatCitation>? Citations = null,
    ChatResponse? Response = null);

public sealed record RetrievedChunk(
    string DocumentId,
    string FileName,
    string Content,
    double? Score);

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

public interface IDocumentRetrievalService
{
    Task<IReadOnlyList<RetrievedChunk>> RetrieveAsync(
        string query,
        int maximumResults,
        CancellationToken cancellationToken);
}

public interface IChatCompletionGateway
{
    Task<string> ContextualizeAsync(
        string message,
        IReadOnlyList<ChatTurn> history,
        CancellationToken cancellationToken);

    IAsyncEnumerable<string> StreamAnswerAsync(
        string message,
        IReadOnlyList<ChatTurn> history,
        IReadOnlyList<ChatCitation> citations,
        CancellationToken cancellationToken);
}

public interface IDocumentChatService
{
    IAsyncEnumerable<ChatStreamEvent> StreamAsync(
        ChatRequest request,
        CancellationToken cancellationToken);

    Task<ChatResponse> CompleteAsync(
        ChatRequest request,
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
