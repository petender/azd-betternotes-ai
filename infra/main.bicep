targetScope = 'subscription'

@minLength(1)
@maxLength(64)
@description('Name of the environment that can be used as part of naming resource convention')
param environmentName string

@minLength(1)
@description('Primary location for all resources')
param location string

@description('Name of the Azure Document Intelligence resource')
param documentIntelligenceName string = ''

@description('SKU for the Azure Document Intelligence resource')
param documentIntelligenceSku string = 'S0'

var abbrs = loadJsonContent('./abbreviations.json')
var resourceToken = toLower(uniqueString(subscription().id, environmentName, location))
var tags = { 'azd-env-name': environmentName }

// Organize resources in a resource group
resource rg 'Microsoft.Resources/resourceGroups@2021-04-01' = {
  name: '${abbrs.resourcesResourceGroups}${environmentName}'
  location: location
  tags: tags
}

// Monitor application with Azure Monitor
module monitoring './core/monitor/monitoring.bicep' = {
  name: 'monitoring'
  scope: rg
  params: {
    location: location
    tags: tags
    logAnalyticsName: '${abbrs.operationalInsightsWorkspaces}${resourceToken}'
    applicationInsightsName: '${abbrs.insightsComponents}${resourceToken}'
  }
}

// App Service Plan to host the web app
module appServicePlan './core/host/appserviceplan.bicep' = {
  name: 'appserviceplan'
  scope: rg
  params: {
    name: '${abbrs.webServerFarms}${resourceToken}'
    location: location
    tags: tags
    sku: {
      name: 'B1'
      capacity: 1
    }
  }
}

// Azure Document Intelligence (Form Recognizer) resource - created first without role assignment
module documentIntelligence './core/ai/cognitiveservices.bicep' = {
  name: 'documentintelligence'
  scope: rg
  params: {
    name: !empty(documentIntelligenceName) ? documentIntelligenceName : '${abbrs.cognitiveServicesAccounts}${resourceToken}'
    location: location
    tags: tags
    kind: 'FormRecognizer'
    sku: documentIntelligenceSku
  }
}

// Storage account for file uploads and downloads
module storage './core/storage/storage-account.bicep' = {
  name: 'storage'
  scope: rg
  params: {
    name: '${abbrs.storageStorageAccounts}${resourceToken}'
    location: location
    tags: tags
  }
}

// Web app hosting the application - depends on Document Intelligence for endpoint
module web './core/host/appservice.bicep' = {
  name: 'web'
  scope: rg
  params: {
    name: '${abbrs.webSitesAppService}web-${resourceToken}'
    location: location
    tags: union(tags, { 'azd-service-name': 'web' })
    applicationInsightsName: monitoring.outputs.applicationInsightsName
    appServicePlanId: appServicePlan.outputs.id
    runtimeName: 'dotnet'
    runtimeVersion: '9.0'
    managedIdentity: true
    appSettings: {
      'AzureAI:Endpoint': documentIntelligence.outputs.endpoint
      'AzureAI:Key': ''
      'AzureStorage:AccountName': storage.outputs.name
      'AzureStorage:UploadContainer': 'uploads'
      'AzureStorage:DownloadContainer': 'downloads'
    }
  }
}

// Role assignment to grant the web app's managed identity access to Document Intelligence
module roleAssignment './core/security/role-assignment.bicep' = {
  name: 'documentintelligence-role-assignment'
  scope: rg
  params: {
    principalId: web.outputs.identityPrincipalId
    roleDefinitionId: 'a97b65f3-24c7-4388-baec-2e87135dc908' // Cognitive Services User role
    principalType: 'ServicePrincipal'
    cognitiveServicesName: documentIntelligence.outputs.name
  }
}

// Role assignment to grant the web app's managed identity access to Storage Account
module storageRoleAssignment './core/security/storage-role-assignment.bicep' = {
  name: 'storage-role-assignment'
  scope: rg
  params: {
    principalId: web.outputs.identityPrincipalId
    roleDefinitionId: 'ba92f5b4-2d11-453d-a403-e96b0029c9fe' // Storage Blob Data Contributor role
    principalType: 'ServicePrincipal'
    storageAccountName: storage.outputs.name
  }
}

// Outputs
output AZURE_LOCATION string = location
output AZURE_TENANT_ID string = tenant().tenantId
output AZURE_RESOURCE_GROUP string = rg.name

output APPLICATIONINSIGHTS_CONNECTION_STRING string = monitoring.outputs.applicationInsightsConnectionString
output AZURE_APP_SERVICE_NAME string = web.outputs.name
output AZURE_APP_SERVICE_URI string = web.outputs.uri
output AZURE_DOCUMENT_INTELLIGENCE_ENDPOINT string = documentIntelligence.outputs.endpoint
output AZURE_DOCUMENT_INTELLIGENCE_NAME string = documentIntelligence.outputs.name
output AZURE_STORAGE_ACCOUNT_NAME string = storage.outputs.name
