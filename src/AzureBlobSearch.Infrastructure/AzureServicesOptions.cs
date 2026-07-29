namespace AzureBlobSearch.Infrastructure;

public sealed class AzureServicesOptions
{
    public const string SectionName = "Azure";

    public required Uri StorageAccountUri { get; init; }

    public required string StorageResourceId { get; init; }

    public required Uri SearchEndpoint { get; init; }

    public string ContainerName { get; init; } = "documents";

    public string IndexName { get; init; } = "documents-index";

    public string DataSourceName { get; init; } = "documents-blob-datasource";

    public string IndexerName { get; init; } = "documents-blob-indexer";

    public long MaximumUploadBytes { get; init; } = 25 * 1024 * 1024;

    public string? ManagedIdentityClientId { get; init; }
}

