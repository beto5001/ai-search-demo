namespace AzureBlobSearch.Infrastructure;

public sealed class AzureServicesOptions
{
    public const string SectionName = "Azure";

    public required Uri StorageAccountUri { get; init; }

    public required string StorageResourceId { get; init; }

    public required Uri SearchEndpoint { get; init; }

    public string ContainerName { get; init; } = "documents";

    public string BatchContainerName { get; init; } = "batch-status";

    public string IndexName { get; init; } = "document-chunks-index";

    public string DataSourceName { get; init; } = "documents-blob-datasource";

    public string IndexerName { get; init; } = "documents-vector-indexer";

    public string SkillsetName { get; init; } = "documents-vector-skillset";

    public required Uri OpenAIEndpoint { get; init; }

    public string EmbeddingDeploymentName { get; init; } = "text-embedding-3-small";

    public string ChatDeploymentName { get; init; } = "gpt-4.1-mini";

    public string EmbeddingModelName { get; init; } = "text-embedding-3-small";

    public int EmbeddingDimensions { get; init; } = 1536;

    public int MaximumChatOutputTokens { get; init; } = 600;

    public long MaximumUploadBytes { get; init; } = 25 * 1024 * 1024;

    public string? ManagedIdentityClientId { get; init; }
}
