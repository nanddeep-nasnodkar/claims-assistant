// Deploy separately, into the existing resource group:
//   az deployment group create -g rg-claims-dev -n claims-apim -f infra/apim.bicep
//     -p publisherEmail=you@example.com --no-wait
targetScope = 'resourceGroup'

@description('Azure region.')
param location string = resourceGroup().location

param workloadName string = 'claims'
param environmentName string = 'dev'

@description('Contact email shown in API Management. Pass it on the command line.')
param publisherEmail string

param publisherName string = 'Claims Assistant Demo'

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

output name string = apim.name
output gatewayUrl string = apim.properties.gatewayUrl
