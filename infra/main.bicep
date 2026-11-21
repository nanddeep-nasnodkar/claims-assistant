targetScope = 'subscription'

@description('Azure region for all resources.')
param location string = 'eastus2'

@description('Short workload name used in resource names.')
@minLength(3)
@maxLength(12)
param workloadName string = 'claims'

@description('Environment suffix used in resource names.')
@allowed([
  'dev'
  'prod'
])
param environmentName string = 'dev'

@description('Optional: your Entra object ID, for Cosmos DB data access in local dev.')
param developerPrincipalId string = '9d8db25c-5b84-42c4-9254-ad4d5bda3531'

var tags = {
  workload: workloadName
  environment: environmentName
  purpose: 'portfolio-demo'
}

// Short, stable suffix so globally unique names do not collide.
var suffix = take(uniqueString(subscription().subscriptionId, workloadName, environmentName), 6)

resource rg 'Microsoft.Resources/resourceGroups@2024-03-01' = {
  name: 'rg-${workloadName}-${environmentName}'
  location: location
  tags: tags
}

module cosmos 'modules/cosmos.bicep' = {
  scope: rg
  name: 'cosmos'
  params: {
    location: location
    tags: tags
    accountName: 'cosmos-${workloadName}-${environmentName}-${suffix}'
    developerPrincipalId: developerPrincipalId
  }
}

module registry 'modules/acr.bicep' = {
  scope: rg
  name: 'registry'
  params: {
    location: location
    tags: tags
    registryName: 'acr${workloadName}${environmentName}${suffix}'
  }
}

module containerAppsEnv 'modules/container-apps-env.bicep' = {
  scope: rg
  name: 'containerAppsEnv'
  params: {
    location: location
    tags: tags
    environmentName: 'cae-${workloadName}-${environmentName}'
    workspaceName: 'log-${workloadName}-${environmentName}'
  }
}

output resourceGroupName string = rg.name
output cosmosEndpoint string = cosmos.outputs.endpoint
output registryLoginServer string = registry.outputs.loginServer
output containerAppsEnvironmentName string = containerAppsEnv.outputs.name
