targetScope = 'subscription'

@minLength(1)
@maxLength(64)
@description('Name of the environment that can be used as part of naming resource convention, the name of the resource group for your application will use this name, prefixed with rg-')
param environmentName string

@minLength(1)
@description('The location used for all deployed resources')
param location string

@description('Id of the user or app to assign application roles')
param principalId string = ''

@secure()
param postgresPassword string
@secure()
param postgresUser string

var tags = {
  'azd-env-name': environmentName
}

resource rg 'Microsoft.Resources/resourceGroups@2022-09-01' = {
  name: 'rg-${environmentName}'
  location: location
  tags: tags
}

module resources 'resources.bicep' = {
  scope: rg
  name: 'resources'
  params: {
    location: location
    tags: tags
    principalId: principalId
  }
}

module cache 'cache/cache.module.bicep' = {
  name: 'cache'
  scope: rg
  params: {
    location: location
  }
}
module cache_roles 'cache-roles/cache-roles.module.bicep' = {
  name: 'cache-roles'
  scope: rg
  params: {
    cache_outputs_name: cache.outputs.name
    location: location
    principalId: resources.outputs.MANAGED_IDENTITY_PRINCIPAL_ID
    principalName: resources.outputs.MANAGED_IDENTITY_NAME
  }
}

module postgres 'postgres/postgres.module.bicep' = {
  name: 'postgres'
  scope: rg
  params: {
    location: location
    postgresUser: postgresUser
    postgresPassword: postgresPassword
  }
}
module postgres_roles 'postgres-roles/postgres-roles.module.bicep' = {
  name: 'postgres-roles'
  scope: rg
  params: {
    location: location
    postgres_outputs_name: postgres.outputs.name
    principalId: resources.outputs.MANAGED_IDENTITY_PRINCIPAL_ID
    principalName: resources.outputs.MANAGED_IDENTITY_NAME
    principalType: 'ServicePrincipal'
  }
}

module keyvault 'keyvault/keyvault.module.bicep' = {
  name: 'keyvault'
  scope: rg
  params: {
    vaultName: '${environmentName}-kv'
    principalId: resources.outputs.MANAGED_IDENTITY_PRINCIPAL_ID
    location: location
  }
}
module keyvaultSecrets 'keyvault/keyvault-secrets.module.bicep' = {
  name: 'keyvault-secrets'
  scope: rg
  params: {
    vaultName: keyvault.outputs.keyVaultName
    postgresConn: postgres.outputs.connectionString
    cacheConn: cache.outputs.connectionString
  }
}

output MANAGED_IDENTITY_CLIENT_ID string = resources.outputs.MANAGED_IDENTITY_CLIENT_ID
output MANAGED_IDENTITY_NAME string = resources.outputs.MANAGED_IDENTITY_NAME
output AZURE_LOG_ANALYTICS_WORKSPACE_NAME string = resources.outputs.AZURE_LOG_ANALYTICS_WORKSPACE_NAME
output AZURE_CONTAINER_REGISTRY_ENDPOINT string = resources.outputs.AZURE_CONTAINER_REGISTRY_ENDPOINT
output AZURE_CONTAINER_REGISTRY_MANAGED_IDENTITY_ID string = resources.outputs.AZURE_CONTAINER_REGISTRY_MANAGED_IDENTITY_ID
output AZURE_CONTAINER_APPS_ENVIRONMENT_NAME string = resources.outputs.AZURE_CONTAINER_APPS_ENVIRONMENT_NAME
output AZURE_CONTAINER_APPS_ENVIRONMENT_ID string = resources.outputs.AZURE_CONTAINER_APPS_ENVIRONMENT_ID
output AZURE_CONTAINER_APPS_ENVIRONMENT_DEFAULT_DOMAIN string = resources.outputs.AZURE_CONTAINER_APPS_ENVIRONMENT_DEFAULT_DOMAIN
output CACHE_CONNECTIONSTRING string = cache.outputs.connectionString
output POSTGRES_CONNECTIONSTRING string = postgres.outputs.connectionString
output KEYVAULT_URI string = keyvault.outputs.keyVaultUri
output KEYVAULT_NAME string = keyvault.outputs.keyVaultName
