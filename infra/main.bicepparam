using './main.bicep'

param location = 'eastus2'
param workloadName = 'claims'
param environmentName = 'dev'
param developerPrincipalId = ''

// Fill these in with your own values (keep this file free of secrets).
param foundryName = 'rg-claims-fou-resource'
param foundryResourceGroup = 'rg-claims-dev'
param openAiEndpoint = 'https://rg-claims-fou-resource.openai.azure.com/'
param chatDeployment = 'gpt-5-mini'
param embeddingDeployment = 'text-embedding-3-small'
