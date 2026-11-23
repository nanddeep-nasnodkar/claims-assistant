@description('Azure region.')
param location string

param tags object

param name string

@description('Resource ID of the Log Analytics workspace that stores the data.')
param workspaceId string

resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: name
  location: location
  tags: tags
  kind: 'web'
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: workspaceId
    IngestionMode: 'LogAnalytics'
  }
}

output name string = appInsights.name
output connectionString string = appInsights.properties.ConnectionString
