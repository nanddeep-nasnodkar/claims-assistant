using './main.bicep'

param location = 'eastus2'
param workloadName = 'claims'
param environmentName = 'dev'
param developerPrincipalId = ''

// Fill these in with your own values, or keep them in infra/main.local.bicepparam (not committed).
param foundryName = 'REPLACE-WITH-YOUR-FOUNDRY-RESOURCE-NAME'
param foundryResourceGroup = 'REPLACE-WITH-ITS-RESOURCE-GROUP'
param openAiEndpoint = 'https://REPLACE-WITH-YOUR-FOUNDRY-RESOURCE-NAME.openai.azure.com/'
param chatDeployment = 'REPLACE-WITH-YOUR-CHAT-DEPLOYMENT-NAME'
param embeddingDeployment = 'text-embedding-3-small'
