using Azure;
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
    private readonly AzureServicesOptions _options = options.Value;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        LogBootstrapping(logger);

        await CreateOrUpdateIndexAsync(cancellationToken);
        await CreateOrUpdateDataSourceAsync(cancellationToken);
        await CreateOrUpdateIndexerAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task CreateOrUpdateIndexAsync(CancellationToken cancellationToken)
    {
        var fields = new FieldBuilder().Build(typeof(BlobSearchDocument));
        var index = new SearchIndex(_options.IndexName, fields);
        await searchIndexClient.CreateOrUpdateIndexAsync(index, cancellationToken: cancellationToken);
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
            Description = "Extrai conteúdo e metadados de PDF, DOCX e TXT no Blob Storage.",
            Schedule = new IndexingSchedule(TimeSpan.FromMinutes(5)),
            Parameters = parameters
        };

        indexer.FieldMappings.Add(new FieldMapping("documentid")
        {
            TargetFieldName = "Id"
        });
        indexer.FieldMappings.Add(new FieldMapping("originalfilename")
        {
            TargetFieldName = "FileName"
        });
        indexer.FieldMappings.Add(new FieldMapping("metadata_content_type")
        {
            TargetFieldName = "ContentType"
        });
        indexer.FieldMappings.Add(new FieldMapping("metadata_storage_size")
        {
            TargetFieldName = "Size"
        });
        indexer.FieldMappings.Add(new FieldMapping("metadata_storage_last_modified")
        {
            TargetFieldName = "LastModified"
        });
        indexer.FieldMappings.Add(new FieldMapping("metadata_storage_path")
        {
            TargetFieldName = "StoragePath"
        });

        await searchIndexerClient.CreateOrUpdateIndexerAsync(
            indexer,
            onlyIfUnchanged: false,
            cancellationToken);
    }

    [LoggerMessage(
        EventId = 1000,
        Level = LogLevel.Information,
        Message = "Garantindo índice, data source e indexador do Azure AI Search")]
    private static partial void LogBootstrapping(ILogger logger);

    private sealed class BlobSearchDocument
    {
        [SimpleField(IsKey = true, IsFilterable = true)]
        public string Id { get; set; } = string.Empty;

        [SearchableField(AnalyzerName = LexicalAnalyzerName.Values.PtBrMicrosoft)]
        public string Content { get; set; } = string.Empty;

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
