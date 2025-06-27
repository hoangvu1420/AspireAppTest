@description('The location for the resource(s) to be deployed.')
param location string = resourceGroup().location

@description('Admin user for PostgreSQL')
@secure()
param postgresUser string

@description('Admin password for PostgreSQL')
@secure()
param postgresPassword string

resource postgres 'Microsoft.DBforPostgreSQL/flexibleServers@2024-08-01' = {
  name: take('postgres-${uniqueString(resourceGroup().id)}', 63)
  location: location
  sku: {
    name: 'Standard_B1ms'
    tier: 'Burstable'
  }
  properties: {
    availabilityZone: '1'
    backup: {
      backupRetentionDays: 7
      geoRedundantBackup: 'Disabled'
    }
    highAvailability: {
      mode: 'Disabled'
    }
    storage: {
      storageSizeGB: 32
    }
    version: '16'
    authConfig: {
      activeDirectoryAuth: 'Enabled'
      passwordAuth: 'Enabled'
    }
    administratorLogin: postgresUser 
    administratorLoginPassword: postgresPassword
  }
  tags: {
    'aspire-resource-name': 'postgres'
  }
}

resource postgreSqlFirewallRule_AllowAllAzureIps 'Microsoft.DBforPostgreSQL/flexibleServers/firewallRules@2024-08-01' = {
  name: 'AllowAllAzureIps'
  properties: {
    endIpAddress: '0.0.0.0'
    startIpAddress: '0.0.0.0'
  }
  parent: postgres
}

resource LMSdb 'Microsoft.DBforPostgreSQL/flexibleServers/databases@2024-08-01' = {
  name: 'LMSdb'
  parent: postgres
}

output connectionString string = 'Host=${postgres.properties.fullyQualifiedDomainName};Username=${postgresUser};Password=${postgresPassword};Ssl Mode=Require'

output name string = postgres.name
