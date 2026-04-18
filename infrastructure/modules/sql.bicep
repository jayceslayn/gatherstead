@description('The Azure region where SQL resources will be created.')
param location string

@description('The object ID of the Entra ID user or group that will be the SQL administrator.')
param sqlEntraAdminObjectId string

@description('The UPN or display name of the SQL Entra ID administrator.')
param sqlEntraAdminLogin string

@description('The tenant ID for Entra ID authentication.')
param tenantId string

@description('Resource ID of the Log Analytics workspace for diagnostic settings.')
param workspaceId string

var sqlServerName = 'gat-sql-${uniqueString(resourceGroup().id)}'

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

resource sqlDatabase 'Microsoft.Sql/servers/databases@2023-08-01-preview' = {
  parent: sqlServer
  name: 'gatherstead'
  location: location
  sku: {
    name: 'S0'
    tier: 'Standard'
  }
  properties: {
    collation: 'SQL_Latin1_General_CP1_CI_AS'
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
