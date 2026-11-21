@description('Azure region.')
param location string

param tags object

@description('Globally unique Cosmos DB account name (3-44 lowercase characters).')
param accountName string

param databaseName string = 'claims'

@description('Optional Entra object ID granted the built-in Data Contributor role.')
param developerPrincipalId string = ''

resource account 'Microsoft.DocumentDB/databaseAccounts@2024-05-15' = {
  name: accountName
  location: location
  tags: tags
  kind: 'GlobalDocumentDB'
  properties: {
    databaseAccountOfferType: 'Standard'
    consistencyPolicy: {
      defaultConsistencyLevel: 'Session'
    }
    locations: [
      {
        locationName: location
        failoverPriority: 0
        isZoneRedundant: false
      }
    ]
    capabilities: [
      {
        name: 'EnableServerless'
      }
      {
        name: 'EnableNoSQLVectorSearch'
      }
    ]
    // Keys are disabled on purpose: apps and you sign in with Entra ID / managed identity.
    disableLocalAuth: true
    minimalTlsVersion: 'Tls12'
    publicNetworkAccess: 'Enabled'
  }
}

resource database 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases@2024-05-15' = {
  parent: account
  name: databaseName
  properties: {
    resource: {
      id: databaseName
    }
  }
}

var containers = [
  {
    name: 'policies'
    partitionKey: '/id'
  }
  {
    name: 'claims'
    partitionKey: '/policyId'
  }
  {
    name: 'triageResults'
    partitionKey: '/claimId'
  }
]

resource containerResources 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers@2024-05-15' = [for c in containers: {
  parent: database
  name: c.name
  properties: {
    resource: {
      id: c.name
      partitionKey: {
        paths: [
          c.partitionKey
        ]
        kind: 'Hash'
      }
    }
  }
}]

// A vector policy can only be set when a container is created, so this is a new container.
// Note the newer API version (2024-11-15): 2024-05-15 does not know the vector properties.
resource policyClauses 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers@2024-11-15' = {
  parent: database
  name: 'policyClauses'
  dependsOn: [
    containerResources
  ]
  properties: {
    resource: {
      id: 'policyClauses'
      partitionKey: {
        paths: [
          '/policyId'
        ]
        kind: 'Hash'
      }
      indexingPolicy: {
        indexingMode: 'consistent'
        automatic: true
        includedPaths: [
          {
            path: '/*'
          }
        ]
        excludedPaths: [
          {
            path: '/embedding/*'
          }
          {
            path: '/"_etag"/?'
          }
        ]
        vectorIndexes: [
          {
            path: '/embedding'
            type: 'quantizedFlat'
          }
        ]
      }
      vectorEmbeddingPolicy: {
        vectorEmbeddings: [
          {
            path: '/embedding'
            dataType: 'float32'
            distanceFunction: 'cosine'
            dimensions: 1536
          }
        ]
      }
    }
  }
}

// Built-in "Cosmos DB Built-in Data Contributor" role.
var dataContributorRoleId = '${account.id}/sqlRoleDefinitions/00000000-0000-0000-0000-000000000002'

resource developerAccess 'Microsoft.DocumentDB/databaseAccounts/sqlRoleAssignments@2024-05-15' = if (!empty(developerPrincipalId)) {
  parent: account
  name: guid(account.id, developerPrincipalId, 'data-contributor')
  properties: {
    roleDefinitionId: dataContributorRoleId
    principalId: developerPrincipalId
    scope: account.id
  }
}

output endpoint string = account.properties.documentEndpoint
output accountName string = account.name
