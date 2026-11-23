// Deploy into the existing resource group (run after the API app exists):
//   az deployment group create -g rg-claims-dev -n claims-apim -f infra/apim.bicep
//     -p publisherEmail=you@example.com -p apiBackendUrl=https://YOUR-API-FQDN -p gatewaySecret=...
targetScope = 'resourceGroup'

@description('Azure region.')
param location string = resourceGroup().location

param workloadName string = 'claims'
param environmentName string = 'dev'

@description('Contact email shown in API Management. Pass it on the command line.')
param publisherEmail string

param publisherName string = 'Claims Assistant Demo'

@description('Public URL of the API container app, for example https://ca-claims-api-dev.xxx.azurecontainerapps.io')
param apiBackendUrl string

@secure()
@description('Shared secret the gateway sends to the API. Must match the value given to the API app.')
param gatewaySecret string

var suffix = take(uniqueString(subscription().subscriptionId, workloadName, environmentName), 6)

// Consumption tier: pay per call (the first million calls each month are free).
resource apim 'Microsoft.ApiManagement/service@2022-08-01' = {
  name: 'apim-${workloadName}-${environmentName}-${suffix}'
  location: location
  sku: {
    name: 'Consumption'
    capacity: 0
  }
  properties: {
    publisherEmail: publisherEmail
    publisherName: publisherName
  }
}

resource gatewaySecretValue 'Microsoft.ApiManagement/service/namedValues@2022-08-01' = {
  parent: apim
  name: 'gateway-secret'
  properties: {
    displayName: 'gateway-secret'
    secret: true
    value: gatewaySecret
  }
}

resource api 'Microsoft.ApiManagement/service/apis@2022-08-01' = {
  parent: apim
  name: 'claims-api'
  properties: {
    displayName: 'Claims Triage API'
    path: 'api'
    protocols: [
      'https'
    ]
    serviceUrl: apiBackendUrl
    subscriptionRequired: true
    subscriptionKeyParameterNames: {
      header: 'Ocp-Apim-Subscription-Key'
      query: 'subscription-key'
    }
  }
}

resource triageOperation 'Microsoft.ApiManagement/service/apis/operations@2022-08-01' = {
  parent: api
  name: 'triage-claim'
  properties: {
    displayName: 'Triage a claim'
    method: 'POST'
    urlTemplate: '/claims/triage'
  }
}

resource healthOperation 'Microsoft.ApiManagement/service/apis/operations@2022-08-01' = {
  parent: api
  name: 'health'
  properties: {
    displayName: 'Health check'
    method: 'GET'
    urlTemplate: '/health'
  }
}

// Rate limit: 10 calls per minute for each subscription key.
// The gateway adds the shared secret and removes the subscription key before calling the API.
resource apiPolicy 'Microsoft.ApiManagement/service/apis/policies@2022-08-01' = {
  parent: api
  name: 'policy'
  dependsOn: [
    gatewaySecretValue
  ]
  properties: {
    format: 'rawxml'
    value: '''
<policies>
  <inbound>
    <base />
    <rate-limit calls="10" renewal-period="60" />
    <set-header name="X-Gateway-Secret" exists-action="override">
      <value>{{gateway-secret}}</value>
    </set-header>
    <set-header name="Ocp-Apim-Subscription-Key" exists-action="delete" />
  </inbound>
  <backend>
    <base />
  </backend>
  <outbound>
    <base />
  </outbound>
  <on-error>
    <base />
  </on-error>
</policies>
'''
  }
}

// A key for demos. Read it from the portal or with listSecrets (see the pack).
resource demoSubscription 'Microsoft.ApiManagement/service/subscriptions@2022-08-01' = {
  parent: apim
  name: 'demo'
  properties: {
    displayName: 'Demo key'
    scope: api.id
    state: 'active'
  }
}

output name string = apim.name
output gatewayUrl string = apim.properties.gatewayUrl
