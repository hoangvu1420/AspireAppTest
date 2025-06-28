Let's centralize secret management using Azure Key Vault and Managed Identities. This involves changes to your Azure Bicep infrastructure, Aspire AppHost configuration, and the application code.

Here’s a step-by-step guide:

---

### **Part 1: Azure Infrastructure (Bicep) Changes**

We will provision Azure Key Vault, configure it for RBAC, grant access to the Managed Identity used by your Container Apps, and update how PostgreSQL and Redis are configured to use Managed Identity for application access.

**1. Create a Key Vault Bicep Module:**

Create a new file: `AspireAppTest.AppHost\infra\keyvault\keyvault.module.bicep`

```bicep
@description('The location for the resource(s) to be deployed.')
param location string = resourceGroup().location

@description('Tags that will be applied to the Key Vault')
param tags object = {}

resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: take('kv-${uniqueString(resourceGroup().id)}', 24) // Key Vault names are limited to 3-24 characters, alphanumeric
  location: location
  properties: {
    sku: {
      family: 'A'
      name: 'standard'
    }
    tenantId: subscription().tenantId
    enabledForDeployment: false
    enabledForDiskEncryption: false
    enabledForTemplateDeployment: false
    enableRbacAuthorization: true // Use RBAC for access control
    publicNetworkAccess: 'Disabled' // Disabled for private endpoint for higher security
  }
  tags: tags
}

output name string = keyVault.name
output vaultUri string = keyVault.properties.vaultUri
```

**2. Create a Key Vault Role Assignment Bicep Module:**

Create a new file: `AspireAppTest.AppHost\infra\keyvault-roles\keyvault-roles.module.bicep`

```bicep
@description('The location for the resource(s) to be deployed.')
param location string = resourceGroup().location

@description('Name of the Key Vault instance.')
param keyVaultName string

@description('Principal ID of the Managed Identity or user to grant access.')
param principalId string

// Role Definition ID for Key Vault Secrets User. See: https://learn.microsoft.com/en-us/azure/role-based-access-control/built-in-roles
@description('Role Definition ID for Key Vault Secrets User.')
param roleDefinitionId string = '4633458b-17de-408a-b874-0fdc5372cdba' 

resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' existing = {
  name: keyVaultName
}

resource keyVaultAccess 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(keyVault.id, principalId, roleDefinitionId)
  scope: keyVault
  properties: {
    principalId: principalId
    principalType: 'ServicePrincipal' // Managed Identity is a Service Principal
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roleDefinitionId)
  }
}
```

**3. Update `AspireAppTest.AppHost\infra\main.bicep`:**

* Remove the `postgresUser` and `postgresPassword` parameters.
* Remove the `CACHE_CONNECTIONSTRING` and `POSTGRES_CONNECTIONSTRING` outputs, as connection strings will no longer be used for deployed services.
* Add the Key Vault module and role assignment.
* Pass the Managed Identity details to the PostgreSQL module.
* Add the Key Vault URI to the outputs.

```diff
--- a/AspireAppTest.AppHost/infra/main.bicep
+++ b/AspireAppTest.AppHost/infra/main.bicep
@@ -10,12 +10,6 @@
 @description('Id of the user or app to assign application roles')
 param principalId string = ''
 
-@secure()
-param postgresPassword string
-
-@secure()
-param postgresUser string
-
 var tags = {
   'azd-env-name': environmentName
 }
@@ -31,12 +25,20 @@
   }
 }
 
+// Enable AAD authentication on the Redis cache
+module cache 'cache/cache.module.bicep' = {
+  name: 'cache'
+  scope: rg
+  params: {
+    location: location
+    principalId: resources.outputs.MANAGED_IDENTITY_PRINCIPAL_ID
+  }
+}
+
 module postgres 'postgres/postgres.module.bicep' = {
   name: 'postgres'
   scope: rg
   params: {
     location: location
-    postgresUser: postgresUser
-    postgresPassword: postgresPassword
+    aadAdminPrincipalId: resources.outputs.MANAGED_IDENTITY_PRINCIPAL_ID
+    aadAdminPrincipalName: resources.outputs.MANAGED_IDENTITY_NAME
+    aadAdminPrincipalType: 'ServicePrincipal'
+    virtualNetworkName: resources.outputs.AZURE_VNET_NAME
   }
 }
 
@@ -49,7 +51,6 @@
   params: {
     keyVaultName: keyVault.outputs.name
     principalId: resources.outputs.MANAGED_IDENTITY_PRINCIPAL_ID
-    roleDefinitionId: '/subscriptions/${subscription().subscriptionId}/providers/Microsoft.Authorization/roleDefinitions/4633458b-17de-408a-b874-0fdc5372cdba' // Key Vault Secrets User role ID
     location: location
   }
 }
@@ -65,6 +66,4 @@
 
 output AZURE_LOCATION string = location
 output AZURE_CONTAINER_REGISTRY_ENDPOINT string = resources.outputs.AZURE_CONTAINER_REGISTRY_ENDPOINT
 output AZURE_CONTAINER_APPS_ENVIRONMENT_DEFAULT_DOMAIN string = resources.outputs.AZURE_CONTAINER_APPS_ENVIRONMENT_DEFAULT_DOMAIN
-output CACHE_CONNECTIONSTRING string = cache.outputs.connectionString
-output POSTGRES_CONNECTIONSTRING string = postgres.outputs.connectionString
 output AZURE_KEY_VAULT_ENDPOINT string = keyVault.outputs.vaultUri
```

**4. Update `AspireAppTest.AppHost\infra\main.parameters.json`:**

Remove the parameters that are no longer used by `main.bicep`.

```diff
--- a/AspireAppTest.AppHost/infra/main.parameters.json
+++ b/AspireAppTest.AppHost/infra/main.parameters.json
@@ -5,12 +5,6 @@
     "parameters": {
       "principalId": {
         "value": "${AZURE_PRINCIPAL_ID}"
       },
-      "postgresPassword": {
-        "value": "${AZURE_POSTGRES_PASSWORD}"
-      },
-      "postgresUser": {
-        "value": "${AZURE_POSTGRES_USER}"
-      },
       "environmentName": {
         "value": "${AZURE_ENV_NAME}"
       },
```

**5. Update `AspireAppTest.AppHost\infra\postgres\postgres.module.bicep`:**

Modify the PostgreSQL module to configure an AAD administrator and disable public network access in favor of a private endpoint for enhanced security.

```diff
--- a/AspireAppTest.AppHost/infra/postgres/postgres.module.bicep
+++ b/AspireAppTest.AppHost/infra/postgres/postgres.module.bicep
@@ -2,20 +2,15 @@
 @description('The location for the resource(s) to be deployed.')
 param location string = resourceGroup().location
 
-@description('Admin user for PostgreSQL')
-@secure()
-param postgresUser string
-
-@description('Admin password for PostgreSQL')
-@secure()
-param postgresPassword string
-
 @description('Principal ID of the AAD admin for PostgreSQL')
 param aadAdminPrincipalId string
 @description('Principal Name of the AAD admin for PostgreSQL')
 param aadAdminPrincipalName string
 @description('Principal Type of the AAD admin for PostgreSQL')
 param aadAdminPrincipalType string = 'ServicePrincipal' // For Managed Identity
+
+@description('The name of the virtual network to deploy the private endpoint to')
+param virtualNetworkName string
 
 resource postgres 'Microsoft.DBforPostgreSQL/flexibleServers@2024-08-01' = {
   name: take('postgres-${uniqueString(resourceGroup().id)}', 63)
@@ -30,11 +25,9 @@
     version: '16'
     authConfig: {
       activeDirectoryAuth: 'Enabled'
-      passwordAuth: 'Enabled' // Keep password auth enabled for flexibility, but we'll primarily use AAD for the app
+      passwordAuth: 'Enabled' 
     }
-    administratorLogin: postgresUser 
-    administratorLoginPassword: postgresPassword
-  }
-  tags: {
-    'aspire-resource-name': 'postgres'
+    network: {
+      publicNetworkAccess: 'Disabled'
+    }
   }
 }
 
@@ -48,19 +41,8 @@
   parent: postgres
 }
 
-resource postgreSqlFirewallRule_AllowAllAzureIps 'Microsoft.DBforPostgreSQL/flexibleServers/firewallRules@2024-08-01-preview' = { // Use preview API for allowing all Azure IPs for AAD auth scenarios
-  name: 'AllowAllAzureIps'
-  properties: {
-    endIpAddress: '0.0.0.0'
-    startIpAddress: '0.0.0.0'
-  }
-  parent: postgres
-}
-
 resource LMSdb 'Microsoft.DBforPostgreSQL/flexibleServers/databases@2024-08-01' = {
   name: 'LMSdb'
   parent: postgres
 }
 
-output connectionString string = 'Host=${postgres.properties.fullyQualifiedDomainName};Username=${postgresUser};Password=${postgresPassword};Ssl Mode=Require'
-
 output name string = postgres.name
```

**6. Update `AspireAppTest.AppHost\infra\cache\cache.module.bicep`:**

Enable Azure AD authentication on the Redis resource.

```diff
--- a/AspireAppTest.AppHost/infra/cache/cache.module.bicep
+++ b/AspireAppTest.AppHost/infra/cache/cache.module.bicep
@@ -1,5 +1,8 @@
 @description('The location for the resource(s) to be deployed.')
 param location string = resourceGroup().location
+
+@description('The principal ID to assign the Data Owner role for the cache.')
+param principalId string
 
 resource cache 'Microsoft.Cache/Redis@2024-04-01' = {
   name: take('redis-${uniqueString(resourceGroup().id)}', 63)
@@ -11,11 +14,24 @@
     sku: {
       name: 'Basic'
       family: 'C'
       capacity: 0
     }
     redisVersion: '6'
+    redisConfiguration: {
+      'aad-enabled': 'True'
+    }
   }
 }
 
-output connectionString string = '${cache.properties.hostName},ssl=true,abortConnect=false,password=${cache.listKeys().primaryKey}'
+// Assign the 'Redis Data Owner' role to the managed identity
+resource dataOwnerRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
+  name: guid(cache.id, principalId, 'a0f7d4a0-6339-473d-8803-335a45526b18')
+  scope: cache
+  properties: {
+    principalId: principalId
+    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', 'a0f7d4a0-6339-473d-8803-335a45526b18') // Redis Data Owner
+  }
+}
 
 output name string = cache.name
+output hostName string = cache.properties.hostName
```

**7. Update Container App Templates (`apiservice.tmpl.yaml` and `webfrontend.tmpl.yaml`):**

Remove the direct `secrets` sections for connection strings. Aspire will inject the necessary configuration for passwordless connections.

Modify `AspireAppTest.AppHost\infra\apiservice.tmpl.yaml`:

```diff
--- a/AspireAppTest.AppHost/infra/apiservice.tmpl.yaml
+++ b/AspireAppTest.AppHost/infra/apiservice.tmpl.yaml
@@ -16,10 +16,6 @@
     registries:
       - server: {{ .Env.AZURE_CONTAINER_REGISTRY_ENDPOINT }}
         identity: {{ .Env.AZURE_CONTAINER_REGISTRY_MANAGED_IDENTITY_ID }}
-    secrets:
-      - name: connectionstrings--lmsdb
-        value: '{{ .Env.POSTGRES_CONNECTIONSTRING }};Database=LMSdb'
-      - name: connectionstrings--cache
-        value: '{{ .Env.CACHE_CONNECTIONSTRING }}'
   template:
     containers:
       - image: {{ .Image }}
@@ -33,9 +29,6 @@
             value: "true"
           - name: OTEL_DOTNET_EXPERIMENTAL_OTLP_RETRY
             value: in_memory
-          - name: ConnectionStrings__LMSdb
-            secretRef: connectionstrings--lmsdb
-          - name: ConnectionStrings__cache
-            secretRef: connectionstrings--cache
+          // Aspire will inject environment variables for Key Vault, Postgres, and Redis
+          // based on the AppHost configuration for passwordless connections.
     scale:
       minReplicas: 1
 tags:
```

Modify `AspireAppTest.AppHost\infra\webfrontend.tmpl.yaml`:

```diff
--- a/AspireAppTest.AppHost/infra/webfrontend.tmpl.yaml
+++ b/AspireAppTest.AppHost/infra/webfrontend.tmpl.yaml
@@ -16,8 +16,6 @@
     registries:
       - server: {{ .Env.AZURE_CONTAINER_REGISTRY_ENDPOINT }}
         identity: {{ .Env.AZURE_CONTAINER_REGISTRY_MANAGED_IDENTITY_ID }}
-    secrets:
-      - name: connectionstrings--cache
-        value: '{{ .Env.CACHE_CONNECTIONSTRING }}'
   template:
     containers:
       - image: {{ .Image }}
@@ -32,7 +30,6 @@
             value: http://apiservice.{{ .Env.AZURE_CONTAINER_APPS_ENVIRONMENT_DEFAULT_DOMAIN }}
           - name: services__apiservice__https__0
             value: https://apiservice.{{ .Env.AZURE_CONTAINER_APPS_ENVIRONMENT_DEFAULT_DOMAIN }}
-          - name: ConnectionStrings__cache
-            secretRef: connectionstrings--cache
+          // Aspire will inject environment variables for Key Vault and Redis
+          // based on the AppHost configuration for passwordless connections.
     scale:
       minReplicas: 1
 tags:
```

---

### **Part 2: Application Code (.NET Aspire) Changes**

Now we'll update the Aspire AppHost and the `apiservice` to use passwordless connections and interact with Key Vault.

**8. Update `AspireAppTest.AppHost\azure.yaml`:**

Remove the `postgresUser` and `postgresPassword` parameters.

```diff
--- a/AspireAppTest.AppHost/azure.yaml
+++ b/AspireAppTest.AppHost/azure.yaml
@@ -2,10 +2,6 @@
 
 name: AspireAppTest.AppHost
 parameters:
-  postgresUser:
-    secureValue: pgadmin123
-  postgresPassword:
-    secureValue: H14204011tuantm*
 services:  
   app:
     language: dotnet
```

**9. Update `AspireAppTest.AppHost\Program.cs`:**

* Configure Redis and PostgreSQL resources to use Azure AD authentication in publish mode.
* Add an `AddAzureKeyVault` resource.
* Reference the `keyVault` resource from the service projects.

```diff
--- a/AspireAppTest.AppHost/Program.cs
+++ b/AspireAppTest.AppHost/Program.cs
@@ -2,38 +2,27 @@
 
 var builder = DistributedApplication.CreateBuilder(args);
 
-var isPublishMode = builder.ExecutionContext.IsPublishMode;
+var postgres = builder.AddAzurePostgresFlexibleServer("postgres")
+                      .RunAsContainer(); // For local dev, use container. In Azure, maps to Bicep.
 
-var cache = isPublishMode
-    ? builder.AddAzureRedis("cache")
-    : builder.AddAzureRedis("cache").RunAsContainer();
+var cache = builder.AddAzureRedis("cache");
 
-// 1. Declare the builder itself first
-var postgresBuilder = builder
-    .AddAzurePostgresFlexibleServer("postgres");
+var db = postgres.AddDatabase("LMSdb");
 
-// 2. In publish mode, wire up password auth; otherwise, spin up a local container
-if (isPublishMode)
-{
-    // these only exist in publish mode
-    var userParam = builder.AddParameter("postgresUser", secret: true);
-    var passwordParam = builder.AddParameter("postgresPassword", secret: true);
+// Add Azure Key Vault resource
+var keyVault = builder.AddAzureKeyVault("keyvault");
 
-    // postgresBuilder = postgresBuilder
-    //     .WithPasswordAuthentication(userParam, passwordParam);
-    }
-else
-{
-    // local runs fallback to Docker Postgres
-    postgresBuilder = postgresBuilder.RunAsContainer();
-}
-
-// 3. Build the final resource and database
-var postgres = postgresBuilder;
-var db = postgres.AddDatabase("LMSdb");
-
 var apiService = builder.AddProject<Projects.AspireAppTest_ApiService>("apiservice")
     .WithExternalHttpEndpoints()
     .WithReference(db)
-    .WaitFor(postgres)
     .WithReference(cache)
-    .WaitFor(cache);
+    .WithReference(keyVault); // Add reference to Key Vault
 
 builder.AddProject<Projects.AspireAppTest_Web>("webfrontend")
     .WithExternalHttpEndpoints()
     .WithReference(cache)
-    .WaitFor(cache)
     .WithReference(apiService)
-    .WaitFor(apiService);
+    .WithReference(keyVault); // Add reference to Key Vault
+
+// In publish mode, configure services for passwordless (AAD) authentication
+if (builder.ExecutionContext.IsPublishMode)
+{
+    postgres.WithAzureAD();
+    cache.WithAzureAD();
+}
 
 builder.Build().Run();
```

**10. Modify `AspireAppTest.ApiService\Program.cs` to Integrate Key Vault:**

* Install the required NuGet package to integrate Azure Key Vault with the application's configuration.
* Modify the application setup to load secrets from Key Vault when deployed to Azure.

First, install the NuGet package:
In your `AspireAppTest.ApiService` project directory, run:

```bash
dotnet add package Azure.Extensions.AspNetCore.Configuration.Secrets
dotnet add package Azure.Identity
```

Then, modify `AspireAppTest.ApiService\Program.cs`:

```diff
--- a/AspireAppTest.ApiService/Program.cs
+++ b/AspireAppTest.ApiService/Program.cs
@@ -1,6 +1,7 @@
 using AspireAppTest.ApiService.Data;
 using AspireAppTest.ApiService.Endpoints;
 using AspireAppTest.ApiService.Services;
+using Azure.Identity;
 
 var builder = WebApplication.CreateBuilder(args);
 
@@ -8,6 +9,16 @@
 builder.AddServiceDefaults();
 
 // Add services to the container.
 builder.Services.AddProblemDetails();
 
+// In production, load secrets from Azure Key Vault
+if (!builder.Environment.IsDevelopment())
+{
+    var keyVaultUri = builder.Configuration["AZURE_KEY_VAULT_ENDPOINT"];
+    if (!string.IsNullOrEmpty(keyVaultUri))
+    {
+        builder.Configuration.AddAzureKeyVault(new Uri(keyVaultUri), new DefaultAzureCredential());
+    }
+}
+
 builder.AddNpgsqlDbContext<LibraryDbContext>("LMSdb");
 
 builder.AddRedisDistributedCache("cache");
```

---

### **Part 3: Manual Steps after Deployment**

After deploying with `azd up`, your Azure Key Vault will be provisioned, and your Container Apps will have Managed Identity access to it. You'll need to manually add any application-specific secrets to Key Vault.

**1. Populate Key Vault with Secrets:**

After `azd up` has successfully deployed your infrastructure, get the name of your Key Vault. You can find it in the Azure Portal or by running:

```bash
az keyvault list --resource-group rg-<your-environment-name> --query "[?contains(name, 'kv-')].name" -o tsv
```

Once you have the Key Vault name (e.g., `kv-yourappname`), add your secrets. For example, to add a secret named `ApiSettings--SecretKey`:

```bash
az keyvault secret set --vault-name <your-keyvault-name> --name "ApiSettings--SecretKey" --value "YourSuperSecretApiKey123"
```

*Note: Using `--` in the secret name (`ApiSettings--SecretKey`) allows it to be mapped directly to the .NET configuration system's `ApiSettings:SecretKey` format.*

---

**Summary of the New Secret Management Flow:**

1. **Deployment (Bicep/`azd`)**:
    * `azure.yaml` no longer contains credentials.
    * Bicep provisions a dedicated Azure Key Vault, configured for private access.
    * Bicep configures PostgreSQL and Redis to allow Azure AD authentication and sets the Container App's Managed Identity as an administrator/data owner.
    * Bicep grants the Container App's Managed Identity the `Key Vault Secrets User` role.
    * Aspire AppHost configures the service projects to use passwordless connections and passes the Key Vault URI.

2. **Runtime (Application)**:
    * The `apiservice` and `webfrontend` applications start up.
    * For PostgreSQL and Redis, the underlying client libraries use `DefaultAzureCredential` to authenticate via Managed Identity automatically. No connection strings are needed.
    * The application detects the `AZURE_KEY_VAULT_ENDPOINT` environment variable.
    * The `AddAzureKeyVault` configuration provider uses `DefaultAzureCredential` to authenticate and load all secrets from Key Vault into the application's `IConfiguration` service.
    * Application code can now access secrets (e.g., `ApiSettings:SecretKey`) from `IConfiguration` as usual.

This new setup provides a more secure, centralized, and auditable secret management solution for your Aspire application on Azure.
