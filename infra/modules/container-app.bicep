@description('Azure region.')
param location string

param tags object

param name string

@description('Resource ID of the Container Apps environment.')
param environmentId string

@description('Resource ID of the user-assigned managed identity used by this app.')
param identityId string

@description('Registry login server, for example myregistry.azurecr.io.')
param registryServer string

@description('Full image name including the tag.')
param image string

@description('True for a public endpoint, false for internal-only.')
param externalIngress bool

@description('Allow plain HTTP inside the environment (internal apps only, if TLS causes trouble).')
param allowInsecure bool = false

@description('Optional CIDR allowed to call a public app, e.g. 203.0.113.7/32. Empty means no restriction.')
param allowedClientIp string = ''

param minReplicas int = 0
param maxReplicas int = 2

@description('Environment variables as an array of { name, value } objects.')
param env array = []

var probes = [
  {
    type: 'Startup'
    httpGet: {
      path: '/health'
      port: 8080
    }
    initialDelaySeconds: 5
    periodSeconds: 10
    failureThreshold: 10 // 10 is the maximum Container Apps allows
  }
  {
    type: 'Liveness'
    httpGet: {
      path: '/health'
      port: 8080
    }
    periodSeconds: 30
  }
]

resource app 'Microsoft.App/containerApps@2024-03-01' = {
  name: name
  location: location
  tags: tags
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${identityId}': {}
    }
  }
  properties: {
    managedEnvironmentId: environmentId
    workloadProfileName: 'Consumption'
    configuration: {
      activeRevisionsMode: 'Single'
      ingress: {
        external: externalIngress
        targetPort: 8080
        transport: 'auto'
        allowInsecure: allowInsecure
        ipSecurityRestrictions: empty(allowedClientIp) ? null : [
          {
            name: 'allow-client'
            action: 'Allow'
            ipAddressRange: allowedClientIp
          }
        ]
      }
      registries: [
        {
          server: registryServer
          identity: identityId
        }
      ]
    }
    template: {
      containers: [
        {
          name: 'app'
          image: image
          resources: {
            cpu: json('0.25')
            memory: '0.5Gi'
          }
          env: env
          probes: probes
        }
      ]
      scale: {
        minReplicas: minReplicas
        maxReplicas: maxReplicas
        rules: [
          {
            name: 'http-concurrency'
            http: {
              metadata: {
                concurrentRequests: '10'
              }
            }
          }
        ]
      }
    }
  }
}

output fqdn string = app.properties.configuration.ingress.fqdn
output name string = app.name
