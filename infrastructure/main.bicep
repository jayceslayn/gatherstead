targetScope = 'subscription'

@description('The Azure region where resources will be created.')
param location string = 'eastus'

@description('The name of the resource group.')
param resourceGroupName string = 'gatherstead-rg'

@description('The object ID of the Entra ID user or group that will be the SQL administrator.')
param sqlEntraAdminObjectId string

@description('The UPN or display name of the SQL Entra ID administrator.')
param sqlEntraAdminLogin string

@description('The object ID of the principal running the deployment (granted Key Vault Administrator for initial setup).')
param deployerObjectId string

@description('The App Service Plan SKU. F1 = free (dev), B1 = basic always-on (prod).')
@allowed(['F1', 'B1', 'B2', 'B3', 'S1', 'S2', 'S3', 'P1v3', 'P2v3', 'P3v3'])
param appServicePlanSku string = 'F1'

@description('Number of days to retain logs in the Log Analytics workspace. Use 30 for dev, 90 for prod.')
@minValue(30)
@maxValue(730)
param logRetentionDays int = 30

@description('Email address that receives oncall alert notifications from Azure Monitor.')
param oncallEmail string

resource rg 'Microsoft.Resources/resourceGroups@2024-03-01' = {
  name: resourceGroupName
  location: location
}

module identity 'modules/identity.bicep' = {
  name: 'identity'
  scope: rg
  params: {
    location: location
  }
}

module observability 'modules/observability.bicep' = {
  name: 'observability'
  scope: rg
  params: {
    location: location
    logRetentionDays: logRetentionDays
    oncallEmail: oncallEmail
    appManagedIdentityPrincipalId: identity.outputs.principalId
  }
}

module keyvault 'modules/keyvault.bicep' = {
  name: 'keyvault'
  scope: rg
  params: {
    location: location
    appManagedIdentityPrincipalId: identity.outputs.principalId
    deployerObjectId: deployerObjectId
    workspaceId: observability.outputs.workspaceId
  }
}

module sql 'modules/sql.bicep' = {
  name: 'sql'
  scope: rg
  params: {
    location: location
    sqlEntraAdminObjectId: sqlEntraAdminObjectId
    sqlEntraAdminLogin: sqlEntraAdminLogin
    tenantId: tenant().tenantId
    workspaceId: observability.outputs.workspaceId
  }
}

module appservice 'modules/appservice.bicep' = {
  name: 'appservice'
  scope: rg
  params: {
    location: location
    appServicePlanSku: appServicePlanSku
    appManagedIdentityId: identity.outputs.id
    appManagedIdentityClientId: identity.outputs.clientId
    sqlServerFqdn: sql.outputs.sqlServerFqdn
    sqlDatabaseName: sql.outputs.sqlDatabaseName
    keyVaultUri: keyvault.outputs.keyVaultUri
    workspaceId: observability.outputs.workspaceId
    appInsightsConnectionString: observability.outputs.appInsightsConnectionString
  }
}

output managedIdentityName string = identity.outputs.name
output managedIdentityClientId string = identity.outputs.clientId
output sqlServerName string = sql.outputs.sqlServerName
output sqlDatabaseName string = sql.outputs.sqlDatabaseName
output sqlServerFqdn string = sql.outputs.sqlServerFqdn
output keyVaultUri string = keyvault.outputs.keyVaultUri
output keyVaultCmkId string = keyvault.outputs.cmkKeyId
output apiAppUrl string = appservice.outputs.apiAppUrl
output webAppUrl string = appservice.outputs.webAppUrl
output appInsightsId string = observability.outputs.appInsightsId
output logAnalyticsWorkspaceId string = observability.outputs.workspaceId
