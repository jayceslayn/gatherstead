@description('The Azure region where the CI managed identity will be created.')
param location string

@description('Workload token used in CAF resource names.')
param workload string

@description('Environment token used in CAF resource names.')
param environment string

@description('Region abbreviation used in CAF resource names.')
param locationAbbreviation string

@description('The GitHub repository (owner/name) allowed to federate with this identity.')
param githubRepository string = 'jayceslayn/gatherstead'

@description('The git branch whose workflow runs may assume this identity.')
param githubBranch string = 'main'

// Built-in role definition IDs
var websiteContributorRoleId = 'de139f84-1756-47ae-9be6-808fbbe84772'
var sqlServerContributorRoleId = '6d8ee4ec-f05a-4a1d-8b00-a9b17e38b437'

var ciIdentityName = 'id-${workload}-ci-${environment}-${locationAbbreviation}'

// CI/CD identity assumed by GitHub Actions via OIDC — no client secret, no password.
resource ciIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: ciIdentityName
  location: location
}

// Trust GitHub's OIDC issuer for workflow runs on the given repo + branch.
resource ciFederatedCredential 'Microsoft.ManagedIdentity/userAssignedIdentities/federatedIdentityCredentials@2023-01-31' = {
  parent: ciIdentity
  name: 'github-${githubBranch}'
  properties: {
    issuer: 'https://token.actions.githubusercontent.com'
    subject: 'repo:${githubRepository}:ref:refs/heads/${githubBranch}'
    audiences: ['api://AzureADTokenExchange']
  }
}

// Website Contributor — lets CI zip-deploy code to the API + Web App Service apps.
// Scoped to the resource group (dedicated to this app) for simplicity.
resource ciWebsiteContributorRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(resourceGroup().id, ciIdentity.id, websiteContributorRoleId)
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', websiteContributorRoleId)
    principalId: ciIdentity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

// SQL Server Contributor — lets CI add/remove the temporary firewall rule used while
// applying EF Core migrations. This is control-plane only; data-plane (DDL) access is
// granted separately to the identity as a SQL database user via ci-grant.sql.
resource ciSqlServerContributorRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(resourceGroup().id, ciIdentity.id, sqlServerContributorRoleId)
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', sqlServerContributorRoleId)
    principalId: ciIdentity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

output ciIdentityName string = ciIdentity.name
output ciIdentityClientId string = ciIdentity.properties.clientId
output ciIdentityPrincipalId string = ciIdentity.properties.principalId
