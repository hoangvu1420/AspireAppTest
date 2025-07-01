targetScope = 'resourceGroup'

@description('The name of the Key Vault (must already exist in this RG)')
param vaultName string

@description('Connection string for Postgres')
@secure()
param postgresConn string

@description('Connection string for Redis')
@secure()
param cacheConn string

// Write Postgres secret
resource postgresSecret 'Microsoft.KeyVault/vaults/secrets@2021-06-01-preview' = {
  name: '${vaultName}/LMSdb-Conn'
  properties: {
    value: postgresConn
  }
}

// Write Redis secret
resource cacheSecret 'Microsoft.KeyVault/vaults/secrets@2021-06-01-preview' = {
  name: '${vaultName}/Cache-Conn'
  properties: {
    value: cacheConn
  }
}
