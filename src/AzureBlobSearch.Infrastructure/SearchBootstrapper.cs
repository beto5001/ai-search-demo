using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Indexes.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AzureBlobSearch.Infrastructure;

public sealed partial class SearchBootstrapper(
    SearchIndexClient searchIndexClient,
    SearchIndexerClient searchIndexerClient,
    IOptions<AzureServicesOptions> options,
    ILogger<SearchBootstrapper> logger) : IHostedService
{
    internal const string VectorFieldName = "ContentVector";
    internal const string VectorProfileName = "content-vector-profile";
    internal const int ChunkLength = 2000;
    internal const int ChunkOverlap = 500;
    private const string VectorAlgorithmName = "content-hnsw";
    private const string VectorizerName = "azure-openai-text-vectorizer";

    private readonly AzureServicesOptions _options = options.Value;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        LogBootstrapping(logger);

        await CreateOrUpdateIndexAsync(cancellationToken);
        await CreateOrUpdateDataSourceAsync(cancellationToken);
        await CreateOrUpdateSkillsetAsync(cancellationToken);
        await CreateOrUpdateIndexerAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task CreateOrUpdateIndexAsync(CancellationToken cancellationToken)
    {
        var vectorizer = new AzureOpenAIVectorizer(VectorizerName)
        {
            Parameters = new AzureOpenAIVectorizerParameters
            {
                ResourceUri = _options.OpenAIEndpoint,
                DeploymentName = _options.EmbeddingDeploymentName,
                ModelName = GetEmbeddingModelName()
            }
        };

        var vectorSearch = new VectorSearch();
        vectorSearch.Algorithms.Add(new HnswAlgorithmConfiguration(VectorAlgorithmName));
        vectorSearch.Profiles.Add(new VectorSearchProfile(VectorProfileName, VectorAlgorithmName)
        {
            VectorizerName = VectorizerName
        });
        vectorSearch.Vectorizers.Add(vectorizer);

        var fields = new FieldBuilder().Build(typeof(BlobChunkDocument));
        var index = new SearchIndex(_options.IndexName, fields)
        {
            VectorSearch = vectorSearch
        };

        await searchIndexClient.CreateOrUpdateIndexAsync(
            index,
            allowIndexDowntime: false,
            onlyIfUnchanged: false,
            cancellationToken);
    }

    private async Task CreateOrUpdateDataSourceAsync(CancellationToken cancellationToken)
    {
        var connectionString = $"ResourceId={_options.StorageResourceId};";
        var dataSource = new SearchIndexerDataSourceConnection(
            _options.DataSourceName,
            SearchIndexerDataSourceType.AzureBlob,
            connectionString,
            new SearchIndexerDataContainer(_options.ContainerName));

        await searchIndexerClient.CreateOrUpdateDataSourceConnectionAsync(
            dataSource,
            onlyIfUnchanged: false,
            cancellationToken);
    }

    private async Task CreateOrUpdateSkillsetAsync(CancellationToken cancellationToken)
    {
        var splitSkill = new SplitSkill(
            [new InputFieldMappingEntry("text") { Source = "/document/content" }],
            [new OutputFieldMappingEntry("textItems") { TargetName = "pages" }])
        {
            Name = "split-content-into-chunks",
            Description = "Divide o texto extraído em trechos com sobreposição para preservar contexto.",
            Context = "/document",
            DefaultLanguageCode = SplitSkillLanguage.PtBr,
            TextSplitMode = TextSplitMode.Pages,
            MaximumPageLength = ChunkLength,
            PageOverlapLength = ChunkOverlap
        };

        var embeddingSkill = new AzureOpenAIEmbeddingSkill(
            [new InputFieldMappingEntry("text") { Source = "/document/pages/*" }],
            [new OutputFieldMappingEntry("embedding") { TargetName = "vector" }])
        {
            Name = "vectorize-content-chunks",
            Description = "Gera um embedding para cada trecho usando Azure OpenAI.",
            Context = "/document/pages/*",
            ResourceUri = _options.OpenAIEndpoint,
            DeploymentName = _options.EmbeddingDeploymentName,
            ModelName = GetEmbeddingModelName(),
            Dimensions = _options.EmbeddingDimensions
        };

        var projection = new SearchIndexerIndexProjection(
            [
                new SearchIndexerIndexProjectionSelector(
                    _options.IndexName,
                    "ParentDocumentKey",
                    "/document/pages/*",
                    [
                        ProjectionMapping("Content", "/document/pages/*"),
                        ProjectionMapping(VectorFieldName, "/document/pages/*/vector"),
                        ProjectionMapping("DocumentId", "/document/documentid"),
                        ProjectionMapping("FileName", "/document/originalfilename"),
                        ProjectionMapping("ContentType", "/document/metadata_content_type"),
                        ProjectionMapping("Size", "/document/metadata_storage_size"),
                        ProjectionMapping("LastModified", "/document/metadata_storage_last_modified"),
                        ProjectionMapping("StoragePath", "/document/metadata_storage_path")
                    ])
            ])
        {
            Parameters = new SearchIndexerIndexProjectionsParameters
            {
                ProjectionMode = IndexProjectionMode.SkipIndexingParentDocuments
            }
        };

        var skillset = new SearchIndexerSkillset(
            _options.SkillsetName,
            [splitSkill, embeddingSkill])
        {
            Description = "Extrai, divide e vetoriza documentos do Blob Storage.",
            IndexProjection = projection
        };

        await searchIndexerClient.CreateOrUpdateSkillsetAsync(
            skillset,
            onlyIfUnchanged: false,
            cancellationToken);
    }

    private async Task CreateOrUpdateIndexerAsync(CancellationToken cancellationToken)
    {
        var parameters = new IndexingParameters();
        parameters.Configuration["dataToExtract"] = "contentAndMetadata";
        parameters.Configuration["parsingMode"] = "default";

        var indexer = new SearchIndexer(
            _options.IndexerName,
            _options.DataSourceName,
            _options.IndexName)
        {
            Description = "Extrai, divide e vetoriza PDF, DOCX e TXT armazenados no Blob Storage.",
            SkillsetName = _options.SkillsetName,
            Schedule = new IndexingSchedule(TimeSpan.FromMinutes(5)),
            Parameters = parameters
        };

        await searchIndexerClient.CreateOrUpdateIndexerAsync(
            indexer,
            onlyIfUnchanged: false,
            cancellationToken);
    }

    private AzureOpenAIModelName GetEmbeddingModelName()
    {
        if (_options.EmbeddingModelName.Equals(
            "text-embedding-3-small",
            StringComparison.OrdinalIgnoreCase))
        {
            return AzureOpenAIModelName.TextEmbedding3Small;
        }

        if (_options.EmbeddingModelName.Equals(
            "text-embedding-3-large",
            StringComparison.OrdinalIgnoreCase))
        {
            return AzureOpenAIModelName.TextEmbedding3Large;
        }

        if (_options.EmbeddingModelName.Equals(
            "text-embedding-ada-002",
            StringComparison.OrdinalIgnoreCase))
        {
            return AzureOpenAIModelName.TextEmbeddingAda002;
        }

        throw new InvalidOperationException(
            $"O modelo de embedding '{_options.EmbeddingModelName}' não é suportado.");
    }

    private static InputFieldMappingEntry ProjectionMapping(string name, string source) =>
        new(name) { Source = source };

    [LoggerMessage(
        EventId = 1000,
        Level = LogLevel.Information,
        Message = "Garantindo índice vetorial, data source, skillset e indexador do Azure AI Search")]
    private static partial void LogBootstrapping(ILogger logger);

    private sealed class BlobChunkDocument
    {
        [SearchableField(
            IsKey = true,
            IsFilterable = true,
            AnalyzerName = LexicalAnalyzerName.Values.Keyword)]
        public string ChunkId { get; set; } = string.Empty;

        [SimpleField(IsFilterable = true)]
        public string ParentDocumentKey { get; set; } = string.Empty;

        [SimpleField(IsFilterable = true)]
        public string DocumentId { get; set; } = string.Empty;

        [SearchableField(AnalyzerName = LexicalAnalyzerName.Values.PtBrMicrosoft)]
        public string Content { get; set; } = string.Empty;

        [VectorSearchField(
            VectorSearchDimensions = 1536,
            VectorSearchProfileName = VectorProfileName)]
        public IReadOnlyList<float> ContentVector { get; set; } = [];

        [SearchableField(IsFilterable = true, IsSortable = true)]
        public string FileName { get; set; } = string.Empty;

        [SimpleField(IsFilterable = true)]
        public string? ContentType { get; set; }

        [SimpleField(IsFilterable = true, IsSortable = true)]
        public long? Size { get; set; }

        [SimpleField(IsFilterable = true, IsSortable = true)]
        public DateTimeOffset? LastModified { get; set; }

        [SimpleField]
        public string? StoragePath { get; set; }
    }
}
