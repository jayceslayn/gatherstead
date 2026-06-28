@description('The Azure region where SQL resources will be created.')
param location string

@description('Workload token used in CAF resource names.')
param workload string

@description('Environment token used in CAF resource names.')
param environment string

@description('Region abbreviation used in CAF resource names.')
param locationAbbreviation string

@description('Short deterministic suffix ensuring the globally-unique SQL server name is free.')
param resourceToken string

@description('The object ID of the Entra ID user or group that will be the SQL administrator.')
param sqlEntraAdminObjectId string

@description('The UPN or display name of the SQL Entra ID administrator.')
param sqlEntraAdminLogin string

@description('The tenant ID for Entra ID authentication.')
param tenantId string

@description('Resource ID of the Log Analytics workspace for diagnostic settings.')
param workspaceId string

var sqlServerName = 'sql-${workload}-${environment}-${locationAbbreviation}-${resourceToken}'

resource sqlServer 'Microsoft.Sql/servers@2023-08-01-preview' = {
  name: sqlServerName
  location: location
  properties: {
    // No administrator login/password — Entra ID-only authentication
    administrators: {
      administratorType: 'ActiveDirectory'
      login: sqlEntraAdminLogin
      sid: sqlEntraAdminObjectId
      tenantId: tenantId
      azureADOnlyAuthentication: true
    }
    minimalTlsVersion: '1.2'
  }
}

// "Allow Azure services and resources to access this server" — the App Service connects from
// Azure public IPs, so this opens the door for them. Access is still gated by Entra ID-only auth
// (azureADOnlyAuthentication) plus per-identity database users; this rule alone grants no data
// access. Codified here so a fresh environment reproduces the posture without a manual portal toggle.
resource allowAzureServices 'Microsoft.Sql/servers/firewallRules@2023-08-01-preview' = {
  parent: sqlServer
  name: 'AllowAllWindowsAzureIps'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

resource sqlDatabase 'Microsoft.Sql/servers/databases@2023-08-01-preview' = {
  parent: sqlServer
  name: 'gatherstead'
  location: location
  // General Purpose serverless: auto-pauses when idle (pay storage only), bills compute
  // per-second when active. Cheapest for current infrequent traffic; VBS secure enclaves
  // are supported on any tier, so this is enclave-ready. Switch to DTU/provisioned later
  // online if usage grows.
  sku: {
    name: 'GP_S_Gen5_1'
    tier: 'GeneralPurpose'
    family: 'Gen5'
    capacity: 1
  }
  properties: {
    collation: 'SQL_Latin1_General_CP1_CI_AS'
    autoPauseDelay: 60          // minutes idle before auto-pause (60 = minimum)
    minCapacity: json('0.5')    // vCore floor while active
    preferredEnclaveType: 'VBS' // enables Always Encrypted with secure enclaves
  }
}

resource sqlDatabaseDiag 'Microsoft.Insights/diagnosticSettings@2021-05-01-preview' = {
  name: 'diag-sql'
  scope: sqlDatabase
  properties: {
    workspaceId: workspaceId
    logs: [
      { category: 'SQLSecurityAuditEvents', enabled: true }
      { category: 'SQLInsights', enabled: true }
      { category: 'Errors', enabled: true }
      { category: 'Deadlocks', enabled: true }
    ]
    metrics: [
      { category: 'Basic', enabled: true }
    ]
  }
}

output sqlServerName string = sqlServer.name
output sqlDatabaseName string = sqlDatabase.name
output sqlServerFqdn string = sqlServer.properties.fullyQualifiedDomainName
