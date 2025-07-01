@description('Name of the Key Vault')
param vaultName string

@description('Location for all resources')
param location string = resourceGroup().location

@description('Managed Identity principal ID to grant access')
param principalId string

resource kv 'Microsoft.KeyVault/vaults@2022-07-01' = {
  name: vaultName
  location: location
  properties: {
    tenantId: subscription().tenantId
    sku: {
      name: 'standard'
      family: 'A'
    }
  enabledForTemplateDeployment: true
  enabledForDiskEncryption: false
  accessPolicies: []
  enableRbacAuthorization: true
  }
}

var secretUserRole = subscriptionResourceId(
  'Microsoft.Authorization/roleDefinitions',
  '4633458b-17de-408a-b874-0445c86b69e6'  // Key Vault Secrets User
)

resource kvRole 'Microsoft.Authorization/roleAssignments@2020-04-01-preview' = {
  name: guid(kv.id, principalId, secretUserRole)
  scope: kv
  properties: {
    principalId: principalId
    roleDefinitionId: secretUserRole
  }
}

output keyVaultUri string = kv.properties.vaultUri
output keyVaultName string = kv.name
