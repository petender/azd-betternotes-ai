@description('The principal ID to assign the role to')
param principalId string

@description('The role definition ID to assign')
param roleDefinitionId string

@description('The principal type (User, Group, ServicePrincipal)')
param principalType string = 'ServicePrincipal'

@description('The Cognitive Services account name')
param cognitiveServicesName string

// Reference the existing Cognitive Services account
resource cognitiveServices 'Microsoft.CognitiveServices/accounts@2023-05-01' existing = {
  name: cognitiveServicesName
}

// Create role assignment scoped to the Cognitive Services resource
resource roleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(cognitiveServices.id, principalId, roleDefinitionId)
  scope: cognitiveServices
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roleDefinitionId)
    principalId: principalId
    principalType: principalType
  }
}

output roleAssignmentId string = roleAssignment.id
