$ErrorActionPreference = "Stop"
$projectRoot = Split-Path -Parent $PSScriptRoot

dotnet restore "$projectRoot/AzureBlobSearch.slnx"
dotnet build "$projectRoot/AzureBlobSearch.slnx" --configuration Release --no-restore
dotnet test "$projectRoot/AzureBlobSearch.slnx" --configuration Release --no-build

if (Get-Command az -ErrorAction SilentlyContinue) {
    az bicep build --file "$projectRoot/infra/main.bicep"
}
else {
    Write-Warning "Azure CLI ausente: validação do Bicep foi ignorada."
}

if (Get-Command docker -ErrorAction SilentlyContinue) {
    docker build --tag azure-blob-search:local $projectRoot
}
else {
    Write-Warning "Docker ausente: build da imagem foi ignorado."
}

