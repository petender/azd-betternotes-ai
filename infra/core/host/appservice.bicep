param name string
param location string = resourceGroup().location
param tags object = {}

param applicationInsightsName string = ''
param appServicePlanId string
param managedIdentity bool = true
param runtimeName string
param runtimeVersion string
param appSettings object = {}

// Convert appSettings object to array format for App Service
var appSettingsArray = [for key in items(appSettings): {
  name: key.key
  value: key.value
}]

var defaultAppSettings = [
  {
    name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
    value: !empty(applicationInsightsName) ? applicationInsights.properties.ConnectionString : ''
  }
  {
    name: 'ApplicationInsightsAgent_EXTENSION_VERSION'
    value: '~3'
  }
  {
    name: 'XDT_MicrosoftApplicationInsights_Mode'
    value: 'default'
  }
]

resource appService 'Microsoft.Web/sites@2022-03-01' = {
  name: name
  location: location
  tags: tags
  kind: 'app'
  properties: {
    serverFarmId: appServicePlanId
    siteConfig: {
      netFrameworkVersion: 'v${runtimeVersion}'
      alwaysOn: true
      ftpsState: 'FtpsOnly'
      minTlsVersion: '1.2'
      http20Enabled: true
      appSettings: union(defaultAppSettings, appSettingsArray)
    }
    httpsOnly: true
  }
  identity: managedIdentity ? {
    type: 'SystemAssigned'
  } : null
}

resource applicationInsights 'Microsoft.Insights/components@2020-02-02' existing = if (!empty(applicationInsightsName)) {
  name: applicationInsightsName
}

output id string = appService.id
output name string = appService.name
output uri string = 'https://${appService.properties.defaultHostName}'
output identityPrincipalId string = managedIdentity ? appService.identity.principalId : ''
