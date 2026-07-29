param(
    [Parameter(Mandatory = $true)]
    [string] $SubscriptionId,

    [string] $ResourceGroup = "rg-blob-search-mvp",

    [string] $Location = "brazilsouth",

    [string] $NamePrefix = "blobsearch"
)

$ErrorActionPreference = "Stop"
$projectRoot = Split-Path -Parent $PSScriptRoot
$deploymentName = "blob-search-" + (Get-Date -Format "yyyyMMddHHmmss")

az account set --subscription $SubscriptionId
az group create --name $ResourceGroup --location $Location --output none

az deployment group create `
    --name $deploymentName `
    --resource-group $ResourceGroup `
    --template-file "$projectRoot/infra/main.bicep" `
    --parameters namePrefix=$NamePrefix location=$Location `
    --output none

$outputsJson = az deployment group show `
    --name $deploymentName `
    --resource-group $ResourceGroup `
    --query properties.outputs `
    --output json
$outputs = $outputsJson | ConvertFrom-Json

$registryName = $outputs.registryName.value
$containerAppName = $outputs.containerAppName.value
$image = "$($outputs.registryLoginServer.value)/azure-blob-search:latest"

az acr build `
    --registry $registryName `
    --image "azure-blob-search:latest" `
    --file "$projectRoot/Dockerfile" `
    $projectRoot `
    --output none

az containerapp update `
    --name $containerAppName `
    --resource-group $ResourceGroup `
    --image $image `
    --output none

Write-Host "API: $($outputs.apiUrl.value)"
Write-Host "OpenAPI: $($outputs.apiUrl.value)/openapi/v1.json"

