@description('Azure region.')
param location string

param tags object

@description('Globally unique registry name (5-50 alphanumeric characters).')
param registryName string

resource registry 'Microsoft.ContainerRegistry/registries@2023-07-01' = {
  name: registryName
  location: location
  tags: tags
  sku: {
    name: 'Basic'
  }
  properties: {
    // No admin user: the Container App pulls images with a managed identity (AcrPull, Day 5).
    adminUserEnabled: false
    publicNetworkAccess: 'Enabled'
  }
}

output loginServer string = registry.properties.loginServer
output name string = registry.name
