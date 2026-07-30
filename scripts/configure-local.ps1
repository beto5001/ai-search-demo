param(
    [Parameter(Mandatory = $true)]
    [string] $StorageAccountName,

    [Parameter(Mandatory = $true)]
    [string] $StorageResourceId,

    [Parameter(Mandatory = $true)]
    [string] $SearchServiceName,

    [Parameter(Mandatory = $true)]
    [string] $EmbeddingAccountName
)

$ErrorActionPreference = "Stop"
$apiProject = Join-Path $PSScriptRoot "../src/AzureBlobSearch.Api"

dotnet user-secrets init --project $apiProject
dotnet user-secrets set "Azure:StorageAccountUri" "https://$StorageAccountName.blob.core.windows.net" --project $apiProject
dotnet user-secrets set "Azure:StorageResourceId" $StorageResourceId --project $apiProject
dotnet user-secrets set "Azure:SearchEndpoint" "https://$SearchServiceName.search.windows.net" --project $apiProject
dotnet user-secrets set "Azure:OpenAIEndpoint" "https://$EmbeddingAccountName.services.ai.azure.com" --project $apiProject

Write-Host "Configuração local salva em User Secrets."
Write-Host "Execute: dotnet run --project src/AzureBlobSearch.Api"

