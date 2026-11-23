@description('Name of the container registry in this resource group.')
param registryName string

@description('Object ID of the identity that needs to push images.')
param principalId string

resource registry 'Microsoft.ContainerRegistry/registries@2023-07-01' existing = {
  name: registryName
}

// Built-in AcrPush role.
var acrPushRoleId = '8311e382-0749-4cb8-b61a-304f252e45ec'

resource acrPush 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: registry
  name: guid(registry.id, principalId, acrPushRoleId)
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', acrPushRoleId)
    principalId: principalId
    principalType: 'ServicePrincipal'
  }
}
