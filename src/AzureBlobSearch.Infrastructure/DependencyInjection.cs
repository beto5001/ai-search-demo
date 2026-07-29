using Azure.Core;
using Azure.Identity;
using Azure.Search.Documents;
using Azure.Search.Documents.Indexes;
using Azure.Storage.Blobs;
using AzureBlobSearch.Application;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AzureBlobSearch.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddAzureBlobSearch(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<AzureServicesOptions>()
            .Bind(configuration.GetSection(AzureServicesOptions.SectionName))
            .Validate(
                options => options.StorageAccountUri is not null
                    && options.SearchEndpoint is not null
                    && !string.IsNullOrWhiteSpace(options.StorageResourceId)
                    && !string.IsNullOrWhiteSpace(options.ContainerName)
                    && !string.IsNullOrWhiteSpace(options.IndexName),
                "As configurações Azure:StorageAccountUri, Azure:StorageResourceId e Azure:SearchEndpoint são obrigatórias.")
            .ValidateOnStart();

        services.AddSingleton<TokenCredential>(provider =>
        {
            var options = provider.GetRequiredService<IOptions<AzureServicesOptions>>().Value;
            return new DefaultAzureCredential(new DefaultAzureCredentialOptions
            {
                ManagedIdentityClientId = options.ManagedIdentityClientId
            });
        });

        services.AddSingleton(provider =>
        {
            var options = provider.GetRequiredService<IOptions<AzureServicesOptions>>().Value;
            var credential = provider.GetRequiredService<TokenCredential>();
            return new BlobServiceClient(options.StorageAccountUri, credential);
        });

        services.AddSingleton(provider =>
        {
            var options = provider.GetRequiredService<IOptions<AzureServicesOptions>>().Value;
            var credential = provider.GetRequiredService<TokenCredential>();
            return new SearchIndexClient(options.SearchEndpoint, credential);
        });

        services.AddSingleton(provider =>
        {
            var options = provider.GetRequiredService<IOptions<AzureServicesOptions>>().Value;
            var credential = provider.GetRequiredService<TokenCredential>();
            return new SearchIndexerClient(options.SearchEndpoint, credential);
        });

        services.AddSingleton(provider =>
        {
            var options = provider.GetRequiredService<IOptions<AzureServicesOptions>>().Value;
            var credential = provider.GetRequiredService<TokenCredential>();
            return new SearchClient(options.SearchEndpoint, options.IndexName, credential);
        });

        services.AddSingleton<AzureDocumentService>();
        services.AddSingleton<IDocumentService>(provider => provider.GetRequiredService<AzureDocumentService>());
        services.AddSingleton<IBatchDocumentService>(provider => provider.GetRequiredService<AzureDocumentService>());
        services.AddSingleton<IDocumentSearchService>(provider => provider.GetRequiredService<AzureDocumentService>());
        services.AddSingleton<IReadinessService>(provider => provider.GetRequiredService<AzureDocumentService>());
        services.AddHostedService<SearchBootstrapper>();

        return services;
    }
}
