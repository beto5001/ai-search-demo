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

function Assert-LastCommand {
    param([Parameter(Mandatory = $true)][string] $Operation)

    if ($LASTEXITCODE -ne 0) {
        throw "Falha em: $Operation (exit code $LASTEXITCODE)."
    }
}

az account set --subscription $SubscriptionId
Assert-LastCommand "selecionar a assinatura Azure"

az group create --name $ResourceGroup --location $Location --output none
Assert-LastCommand "criar ou atualizar o resource group"

az deployment group create `
    --name $deploymentName `
    --resource-group $ResourceGroup `
    --template-file "$projectRoot/infra/main.bicep" `
    --parameters namePrefix=$NamePrefix location=$Location `
    --output none
Assert-LastCommand "implantar a infraestrutura Bicep"

$outputsJson = az deployment group show `
    --name $deploymentName `
    --resource-group $ResourceGroup `
    --query properties.outputs `
    --output json
Assert-LastCommand "consultar os outputs do deployment"

$outputs = $outputsJson | ConvertFrom-Json

$registryName = $outputs.registryName.value
$registryLoginServer = $outputs.registryLoginServer.value
$containerAppName = $outputs.containerAppName.value
$imageTag = Get-Date -Format "yyyyMMddHHmmss"
$image = "$registryLoginServer/azure-blob-search:$imageTag"

$acrSessionJson = az acr login `
    --name $registryName `
    --expose-token `
    --output json
Assert-LastCommand "obter um token temporário do ACR"

$acrSession = $acrSessionJson | ConvertFrom-Json
$env:DOTNET_CONTAINER_REGISTRY_UNAME = $acrSession.username
$env:DOTNET_CONTAINER_REGISTRY_PWORD = $acrSession.accessToken

try {
    dotnet publish "$projectRoot/src/AzureBlobSearch.Api/AzureBlobSearch.Api.csproj" `
        --configuration Release `
        --os linux `
        --arch x64 `
        /t:PublishContainer `
        -p:ContainerRegistry=$registryLoginServer `
        -p:ContainerRepository="azure-blob-search" `
        -p:ContainerImageTag=$imageTag
    Assert-LastCommand "construir e publicar a imagem OCI com o .NET SDK"
}
finally {
    Remove-Item Env:\DOTNET_CONTAINER_REGISTRY_UNAME -ErrorAction SilentlyContinue
    Remove-Item Env:\DOTNET_CONTAINER_REGISTRY_PWORD -ErrorAction SilentlyContinue
}

az containerapp update `
    --name $containerAppName `
    --resource-group $ResourceGroup `
    --image $image `
    --output none
Assert-LastCommand "atualizar a imagem da Container App"

Write-Host "API: $($outputs.apiUrl.value)"
Write-Host "OpenAPI: $($outputs.apiUrl.value)/openapi/v1.json"
