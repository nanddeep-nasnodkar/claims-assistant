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
param developerPrincipalId string = ''

@description('Name of your existing Foundry (Azure OpenAI) resource.')
param foundryName string

@description('Resource group that contains the Foundry resource.')
param foundryResourceGroup string

@description('Azure OpenAI endpoint of the Foundry resource, e.g. https://name.openai.azure.com/')
param openAiEndpoint string

@description('Deployment name of the chat model.')
param chatDeployment string

@description('Deployment name of the embedding model.')
param embeddingDeployment string = 'text-embedding-3-small'

@description('Phase 1 creates infrastructure and permissions. Phase 2 (true) also creates the apps.')
param deployApps bool = false

param mcpImageTag string = 'v1'
param apiImageTag string = 'v1'

@description('Optional CIDR allowed to call the public API, e.g. 203.0.113.7/32.')
param allowedClientIp string = ''

@description('Show the API docs page (Scalar) in the deployed API.')
param enableApiDocs bool = false

@secure()
@description('Shared secret that the API Management gateway sends to the API. Required when deployApps is true.')
param gatewaySecret string = ''

@description('GitHub OIDC subject allowed to deploy, e.g. repo:OWNER@123/REPO@456:ref:refs/heads/main. Empty skips it.')
param githubSubject string = ''

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

// ---------- identities: one per app, so each gets only the access it needs ----------
module mcpIdentity 'modules/identity.bicep' = {
  scope: rg
  name: 'mcpIdentity'
  params: {
    location: location
    tags: tags
    name: 'id-${workloadName}-mcp-${environmentName}'
  }
}

module apiIdentity 'modules/identity.bicep' = {
  scope: rg
  name: 'apiIdentity'
  params: {
    location: location
    tags: tags
    name: 'id-${workloadName}-api-${environmentName}'
  }
}

// ---------- core resources ----------
module cosmos 'modules/cosmos.bicep' = {
  scope: rg
  name: 'cosmos'
  params: {
    location: location
    tags: tags
    accountName: 'cosmos-${workloadName}-${environmentName}-${suffix}'
    developerPrincipalId: developerPrincipalId
    dataContributorPrincipalIds: [
      mcpIdentity.outputs.principalId
    ]
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

// ---------- monitoring ----------
module appInsights 'modules/app-insights.bicep' = {
  scope: rg
  name: 'appInsights'
  params: {
    location: location
    tags: tags
    name: 'appi-${workloadName}-${environmentName}'
    workspaceId: containerAppsEnv.outputs.workspaceId
  }
}

// ---------- permissions ----------
module mcpAcrPull 'modules/acr-pull.bicep' = {
  scope: rg
  name: 'mcpAcrPull'
  params: {
    registryName: registry.outputs.name
    principalId: mcpIdentity.outputs.principalId
  }
}

module apiAcrPull 'modules/acr-pull.bicep' = {
  scope: rg
  name: 'apiAcrPull'
  params: {
    registryName: registry.outputs.name
    principalId: apiIdentity.outputs.principalId
  }
}

module mcpOpenAi 'modules/openai-user.bicep' = {
  scope: resourceGroup(foundryResourceGroup)
  name: 'mcpOpenAi'
  params: {
    foundryName: foundryName
    principalId: mcpIdentity.outputs.principalId
  }
}

module apiOpenAi 'modules/openai-user.bicep' = {
  scope: resourceGroup(foundryResourceGroup)
  name: 'apiOpenAi'
  params: {
    foundryName: foundryName
    principalId: apiIdentity.outputs.principalId
  }
}

// ---------- GitHub Actions deployment identity ----------
module githubIdentity 'modules/github-identity.bicep' = if (!empty(githubSubject)) {
  scope: rg
  name: 'githubIdentity'
  params: {
    location: location
    tags: tags
    name: 'id-${workloadName}-github-${environmentName}'
    subject: githubSubject
  }
}

module githubAcrPush 'modules/acr-push.bicep' = if (!empty(githubSubject)) {
  scope: rg
  name: 'githubAcrPush'
  params: {
    registryName: registry.outputs.name
    principalId: githubIdentity!.outputs.principalId
  }
}

module githubContributor 'modules/rg-contributor.bicep' = if (!empty(githubSubject)) {
  scope: rg
  name: 'githubContributor'
  params: {
    principalId: githubIdentity!.outputs.principalId
  }
}

// ---------- apps (phase 2) ----------
module mcpApp 'modules/container-app.bicep' = if (deployApps) {
  scope: rg
  name: 'mcpApp'
  params: {
    location: location
    tags: tags
    name: 'ca-${workloadName}-mcp-${environmentName}'
    environmentId: containerAppsEnv.outputs.id
    identityId: mcpIdentity.outputs.id
    registryServer: registry.outputs.loginServer
    image: '${registry.outputs.loginServer}/claims-mcp:${mcpImageTag}'
    externalIngress: false
    minReplicas: 1
    maxReplicas: 2
    env: [
      { name: 'AZURE_CLIENT_ID', value: mcpIdentity.outputs.clientId }
      { name: 'Cosmos__Endpoint', value: cosmos.outputs.endpoint }
      { name: 'Cosmos__Database', value: 'claims' }
      { name: 'OpenAI__Endpoint', value: openAiEndpoint }
      { name: 'OpenAI__EmbeddingDeployment', value: embeddingDeployment }
      { name: 'APPLICATIONINSIGHTS_CONNECTION_STRING', value: appInsights.outputs.connectionString }
    ]
  }
  dependsOn: [
    mcpAcrPull
    mcpOpenAi
  ]
}

module apiApp 'modules/container-app.bicep' = if (deployApps) {
  scope: rg
  name: 'apiApp'
  params: {
    location: location
    tags: tags
    name: 'ca-${workloadName}-api-${environmentName}'
    environmentId: containerAppsEnv.outputs.id
    identityId: apiIdentity.outputs.id
    registryServer: registry.outputs.loginServer
    image: '${registry.outputs.loginServer}/claims-api:${apiImageTag}'
    externalIngress: true
    allowedClientIp: allowedClientIp
    gatewaySecret: gatewaySecret
    minReplicas: 0
    maxReplicas: 2
    env: [
      { name: 'AZURE_CLIENT_ID', value: apiIdentity.outputs.clientId }
      { name: 'OpenAI__Endpoint', value: openAiEndpoint }
      { name: 'OpenAI__ChatDeployment', value: chatDeployment }
      { name: 'Mcp__Endpoint', value: 'https://${mcpApp!.outputs.fqdn}/mcp' }
      { name: 'EnableApiDocs', value: string(enableApiDocs) }
      { name: 'ASPNETCORE_FORWARDEDHEADERS_ENABLED', value: 'true' }
      { name: 'APPLICATIONINSIGHTS_CONNECTION_STRING', value: appInsights.outputs.connectionString }
    ]
  }
  dependsOn: [
    apiAcrPull
    apiOpenAi
  ]
}

output resourceGroupName string = rg.name
output cosmosEndpoint string = cosmos.outputs.endpoint
output registryLoginServer string = registry.outputs.loginServer
output containerAppsEnvironmentName string = containerAppsEnv.outputs.name
output apiUrl string = deployApps ? 'https://${apiApp!.outputs.fqdn}' : ''
output appInsightsName string = appInsights.outputs.name
output githubClientId string = !empty(githubSubject) ? githubIdentity!.outputs.clientId : ''
output tenantId string = tenant().tenantId
output subscriptionId string = subscription().subscriptionId
