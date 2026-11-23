@description('Azure region.')
param location string

param tags object

param name string

@description('Subject of the GitHub OIDC token, for example repo:OWNER@123/REPO@456:ref:refs/heads/main')
param subject string

resource identity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: name
  location: location
  tags: tags
}

// Lets the GitHub Actions workflow sign in as this identity without any stored password.
resource federated 'Microsoft.ManagedIdentity/userAssignedIdentities/federatedIdentityCredentials@2023-01-31' = {
  parent: identity
  name: 'github-main'
  properties: {
    issuer: 'https://token.actions.githubusercontent.com'
    subject: subject
    audiences: [
      'api://AzureADTokenExchange'
    ]
  }
}

output clientId string = identity.properties.clientId
output principalId string = identity.properties.principalId
