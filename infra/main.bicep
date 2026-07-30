targetScope = 'resourceGroup'

@description('Prefixo curto e único usado nos nomes dos recursos.')
@minLength(3)
@maxLength(12)
param namePrefix string

@description('Região dos recursos.')
param location string = resourceGroup().location

@description('Imagem inicial. O script deploy.ps1 a substitui pela imagem criada no ACR.')
param bootstrapImage string = 'mcr.microsoft.com/dotnet/samples:aspnetapp'

@description('SKU do Azure AI Search.')
@allowed([
  'basic'
  'standard'
])
param searchSku string = 'basic'

@description('Nome de um recurso Microsoft Foundry/Azure OpenAI existente no mesmo resource group.')
param embeddingAccountName string

var suffix = uniqueString(subscription().subscriptionId, resourceGroup().id, namePrefix)
var storageName = toLower('stg${take(replace('${namePrefix}${suffix}', '-', ''), 21)}')
var searchName = toLower(take('${namePrefix}-${suffix}', 60))
var registryName = toLower('acrmv${take(replace('${namePrefix}${suffix}', '-', ''), 45)}')
var environmentName = '${namePrefix}-env'
var containerAppName = '${namePrefix}-api'
var identityName = '${namePrefix}-api-identity'
var logName = '${namePrefix}-logs'
var containerName = 'documents'
var batchContainerName = 'batch-status'

var storageBlobDataReaderRoleId = subscriptionResourceId(
  'Microsoft.Authorization/roleDefinitions',
  '2a2b9908-6ea1-4ae2-8e65-a410df84e7d1'
)
var storageBlobDataContributorRoleId = subscriptionResourceId(
  'Microsoft.Authorization/roleDefinitions',
  'ba92f5b4-2d11-453d-a403-e96b0029c9fe'
)
var acrPullRoleId = subscriptionResourceId(
  'Microsoft.Authorization/roleDefinitions',
  '7f951dda-4ed3-4680-a7ca-43fe172d538d'
)
var searchServiceContributorRoleId = subscriptionResourceId(
  'Microsoft.Authorization/roleDefinitions',
  '7ca78c08-252a-4471-8644-bb5ff32d4ba0'
)
var searchIndexDataContributorRoleId = subscriptionResourceId(
  'Microsoft.Authorization/roleDefinitions',
  '8ebe5a00-799e-43f5-93ac-243d3dce84a7'
)
var searchIndexDataReaderRoleId = subscriptionResourceId(
  'Microsoft.Authorization/roleDefinitions',
  '1407120a-92aa-4202-b7e9-c0e197c71c8f'
)
var cognitiveServicesOpenAIUserRoleId = subscriptionResourceId(
  'Microsoft.Authorization/roleDefinitions',
  '5e0bd9bd-7b93-4f28-af87-19fc36ad61bd'
)

resource storage 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: storageName
  location: location
  sku: {
    name: 'Standard_LRS'
  }
  kind: 'StorageV2'
  properties: {
    accessTier: 'Hot'
    allowBlobPublicAccess: false
    allowSharedKeyAccess: false
    minimumTlsVersion: 'TLS1_2'
    supportsHttpsTrafficOnly: true
  }
}

resource blobService 'Microsoft.Storage/storageAccounts/blobServices@2023-05-01' = {
  parent: storage
  name: 'default'
  properties: {
    deleteRetentionPolicy: {
      enabled: true
      days: 7
    }
    containerDeleteRetentionPolicy: {
      enabled: true
      days: 7
    }
  }
}

resource documentsContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-05-01' = {
  parent: blobService
  name: containerName
  properties: {
    publicAccess: 'None'
  }
}

resource batchStatusContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-05-01' = {
  parent: blobService
  name: batchContainerName
  properties: {
    publicAccess: 'None'
  }
}

resource search 'Microsoft.Search/searchServices@2023-11-01' = {
  name: searchName
  location: location
  identity: {
    type: 'SystemAssigned'
  }
  sku: {
    name: searchSku
  }
  properties: {
    disableLocalAuth: true
    hostingMode: 'default'
    partitionCount: 1
    publicNetworkAccess: 'enabled'
    replicaCount: 1
    semanticSearch: 'disabled'
  }
}

resource embeddingAccount 'Microsoft.CognitiveServices/accounts@2024-10-01' existing = {
  name: embeddingAccountName
}

resource embeddingDeployment 'Microsoft.CognitiveServices/accounts/deployments@2024-10-01' existing = {
  parent: embeddingAccount
  name: 'text-embedding-3-small'
}

resource registry 'Microsoft.ContainerRegistry/registries@2023-07-01' = {
  name: registryName
  location: location
  sku: {
    name: 'Basic'
  }
  properties: {
    adminUserEnabled: false
    publicNetworkAccess: 'Enabled'
  }
}

resource appIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: identityName
  location: location
}

resource logs 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: logName
  location: location
  properties: {
    retentionInDays: 30
    sku: {
      name: 'PerGB2018'
    }
  }
}

resource environment 'Microsoft.App/managedEnvironments@2024-03-01' = {
  name: environmentName
  location: location
  properties: {
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsConfiguration: {
        customerId: logs.properties.customerId
        sharedKey: logs.listKeys().primarySharedKey
      }
    }
  }
}

resource searchStorageReader 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(storage.id, search.id, storageBlobDataReaderRoleId)
  scope: storage
  properties: {
    principalId: search.identity.principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: storageBlobDataReaderRoleId
  }
}

resource searchOpenAIUser 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(embeddingAccount.id, search.id, cognitiveServicesOpenAIUserRoleId)
  scope: embeddingAccount
  properties: {
    principalId: search.identity.principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: cognitiveServicesOpenAIUserRoleId
  }
}

resource appStorageContributor 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(documentsContainer.id, appIdentity.id, storageBlobDataContributorRoleId)
  scope: documentsContainer
  properties: {
    principalId: appIdentity.properties.principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: storageBlobDataContributorRoleId
  }
}

resource appBatchStorageContributor 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(batchStatusContainer.id, appIdentity.id, storageBlobDataContributorRoleId)
  scope: batchStatusContainer
  properties: {
    principalId: appIdentity.properties.principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: storageBlobDataContributorRoleId
  }
}

resource appSearchServiceContributor 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(search.id, appIdentity.id, searchServiceContributorRoleId)
  scope: search
  properties: {
    principalId: appIdentity.properties.principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: searchServiceContributorRoleId
  }
}

resource appSearchDataContributor 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(search.id, appIdentity.id, searchIndexDataContributorRoleId)
  scope: search
  properties: {
    principalId: appIdentity.properties.principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: searchIndexDataContributorRoleId
  }
}

resource appSearchDataReader 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(search.id, appIdentity.id, searchIndexDataReaderRoleId)
  scope: search
  properties: {
    principalId: appIdentity.properties.principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: searchIndexDataReaderRoleId
  }
}

resource appRegistryPull 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(registry.id, appIdentity.id, acrPullRoleId)
  scope: registry
  properties: {
    principalId: appIdentity.properties.principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: acrPullRoleId
  }
}

resource app 'Microsoft.App/containerApps@2024-03-01' = {
  name: containerAppName
  location: location
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${appIdentity.id}': {}
    }
  }
  properties: {
    environmentId: environment.id
    configuration: {
      activeRevisionsMode: 'Single'
      ingress: {
        allowInsecure: false
        external: true
        targetPort: 8080
        transport: 'auto'
      }
      registries: [
        {
          server: registry.properties.loginServer
          identity: appIdentity.id
        }
      ]
    }
    template: {
      containers: [
        {
          name: 'api'
          image: bootstrapImage
          env: [
            {
              name: 'ASPNETCORE_ENVIRONMENT'
              value: 'Production'
            }
            {
              name: 'Azure__StorageAccountUri'
              value: storage.properties.primaryEndpoints.blob
            }
            {
              name: 'Azure__StorageResourceId'
              value: storage.id
            }
            {
              name: 'Azure__SearchEndpoint'
              value: 'https://${search.name}.search.windows.net'
            }
            {
              name: 'Azure__OpenAIEndpoint'
              value: 'https://${embeddingAccount.name}.services.ai.azure.com'
            }
            {
              name: 'Azure__IndexName'
              value: 'document-chunks-index'
            }
            {
              name: 'Azure__IndexerName'
              value: 'documents-vector-indexer'
            }
            {
              name: 'Azure__SkillsetName'
              value: 'documents-vector-skillset'
            }
            {
              name: 'Azure__EmbeddingDeploymentName'
              value: embeddingDeployment.name
            }
            {
              name: 'Azure__ContainerName'
              value: containerName
            }
            {
              name: 'Azure__BatchContainerName'
              value: batchContainerName
            }
            {
              name: 'Azure__ManagedIdentityClientId'
              value: appIdentity.properties.clientId
            }
          ]
          resources: {
            cpu: json('0.5')
            memory: '1Gi'
          }
          probes: [
            {
              type: 'Liveness'
              httpGet: {
                path: '/health/live'
                port: 8080
                scheme: 'HTTP'
              }
              initialDelaySeconds: 15
              periodSeconds: 30
            }
          ]
        }
      ]
      scale: {
        minReplicas: 1
        maxReplicas: 3
        rules: [
          {
            name: 'http-scaling'
            http: {
              metadata: {
                concurrentRequests: '50'
              }
            }
          }
        ]
      }
    }
  }
  dependsOn: [
    appStorageContributor
    appBatchStorageContributor
    appSearchServiceContributor
    appSearchDataContributor
    appSearchDataReader
    appRegistryPull
    searchStorageReader
    searchOpenAIUser
  ]
}

output apiUrl string = 'https://${app.properties.configuration.ingress.fqdn}'
output containerAppName string = app.name
output registryName string = registry.name
output registryLoginServer string = registry.properties.loginServer
output storageAccountName string = storage.name
output searchServiceName string = search.name
output embeddingAccountName string = embeddingAccount.name
output embeddingEndpoint string = 'https://${embeddingAccount.name}.services.ai.azure.com'
output appIdentityClientId string = appIdentity.properties.clientId
