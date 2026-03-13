@description('The Azure region where App Service resources will be created.')
param location string

@description('The App Service Plan SKU. Use F1 for dev (free) or B1 for prod (basic, always-on).')
@allowed(['F1', 'B1', 'B2', 'B3', 'S1', 'S2', 'S3', 'P1v3', 'P2v3', 'P3v3'])
param appServicePlanSku string = 'F1'

@description('Full resource ID of the user-assigned managed identity to attach to the API app.')
param appManagedIdentityId string

@description('Client ID of the managed identity — set as AZURE_CLIENT_ID so DefaultAzureCredential selects the correct identity.')
param appManagedIdentityClientId string

@description('Fully qualified domain name of the SQL server.')
param sqlServerFqdn string

@description('Name of the SQL database.')
param sqlDatabaseName string

@description('URI of the Key Vault instance.')
param keyVaultUri string

var planName = 'gat-asp-${uniqueString(resourceGroup().id)}'
var apiAppName = 'gat-api-${uniqueString(resourceGroup().id)}'
var webAppName = 'gat-web-${uniqueString(resourceGroup().id)}'

var connectionString = 'Server=tcp:${sqlServerFqdn},1433;Database=${sqlDatabaseName};Authentication=Active Directory Managed Identity;User Id=${appManagedIdentityClientId};Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;Column Encryption Setting=Enabled;Attestation Protocol=HGS;'

resource plan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: planName
  location: location
  kind: 'linux'
  sku: {
    name: appServicePlanSku
  }
  properties: {
    reserved: true // required for Linux
  }
}

resource apiApp 'Microsoft.Web/sites@2023-12-01' = {
  name: apiAppName
  location: location
  kind: 'app,linux'
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${appManagedIdentityId}': {}
    }
  }
  properties: {
    serverFarmId: plan.id
    httpsOnly: true
    siteConfig: {
      linuxFxVersion: 'DOTNETCORE|10.0'
      alwaysOn: appServicePlanSku != 'F1'
      appSettings: [
        // Tells DefaultAzureCredential which user-assigned identity to use
        { name: 'AZURE_CLIENT_ID', value: appManagedIdentityClientId }
        { name: 'Authentication__KeyVault__VaultUrl', value: keyVaultUri }
        // Allow requests from the web app origin
        { name: 'Cors__AllowedOrigins__0', value: 'https://${webAppName}.azurewebsites.net' }
      ]
      connectionStrings: [
        {
          name: 'Default'
          connectionString: connectionString
          type: 'SQLAzure'
        }
      ]
    }
  }
}

resource webApp 'Microsoft.Web/sites@2023-12-01' = {
  name: webAppName
  location: location
  kind: 'app,linux'
  properties: {
    serverFarmId: plan.id
    httpsOnly: true
    siteConfig: {
      linuxFxVersion: 'NODE|24-lts'
      alwaysOn: appServicePlanSku != 'F1'
      appCommandLine: 'node .output/server/index.mjs'
      appSettings: [
        // Overrides runtimeConfig.public.apiBaseUrl in nuxt.config.ts
        { name: 'NUXT_PUBLIC_API_BASE_URL', value: 'https://${apiAppName}.azurewebsites.net' }
        { name: 'WEBSITE_RUN_FROM_PACKAGE', value: '1' }
      ]
    }
  }
}

output apiAppName string = apiApp.name
output apiAppUrl string = 'https://${apiApp.properties.defaultHostName}'
output webAppName string = webApp.name
output webAppUrl string = 'https://${webApp.properties.defaultHostName}'
