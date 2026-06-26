targetScope = 'subscription'

@description('The Azure region where resources will be created.')
param location string = 'eastus'

@description('The name of the resource group.')
param resourceGroupName string = 'gatherstead-rg'

@description('Workload token used in CAF resource names (the <workload> segment).')
param workload string = 'gat'

@description('Environment token used in CAF resource names, e.g. dev or prod.')
param environment string = 'dev'

@description('Region abbreviation used in CAF resource names, e.g. wus2 for westus2.')
param locationAbbreviation string = 'eus'

@description('The object ID of the Entra ID user or group that will be the SQL administrator.')
param sqlEntraAdminObjectId string

@description('The UPN or display name of the SQL Entra ID administrator.')
param sqlEntraAdminLogin string

@description('The object ID of the principal running the deployment (granted Key Vault Administrator for initial setup).')
param deployerObjectId string

@description('The App Service Plan SKU. F1 = free (dev), B1 = basic always-on (prod).')
@allowed(['F1', 'B1', 'B2', 'B3', 'S1', 'S2', 'S3', 'P1v3', 'P2v3', 'P3v3'])
param appServicePlanSku string = 'F1'

@description('Whether to deploy the demo static web app.')
param deployDemo bool = false

@description('Number of days to retain logs in the Log Analytics workspace. Use 30 for dev, 90 for prod.')
@minValue(30)
@maxValue(730)
param logRetentionDays int = 30

@description('Email address that receives oncall alert notifications from Azure Monitor.')
param oncallEmail string

@description('The GitHub repository (owner/name) whose Actions runs may deploy via OIDC.')
param githubRepository string = 'jayceslayn/gatherstead'

@description('The git branch whose Actions runs may deploy via OIDC.')
param githubBranch string = 'main'

// External identity provider (Microsoft Entra External ID / Azure AD B2C) used to validate the
// API's JWT bearer tokens. All values are non-secret: bearer validation uses the IdP's published
// OIDC signing keys, not a client secret. These map 1:1 onto the API's `ExternalIdentity` config
// section and are injected as `ExternalIdentity__*` app settings on the API App Service.
@description('External ID instance/authority host, e.g. https://your-tenant.ciamlogin.com.')
param externalIdentityInstance string

@description('External ID domain, e.g. your-tenant.onmicrosoft.com (used to build the authority URL).')
param externalIdentityDomain string

@description('App registration (client) ID of the API — used as the JWT audience.')
param externalIdentityClientId string

@description('B2C sign-up/sign-in policy (user flow) ID. Leave empty for Entra External ID, which has no policy segment.')
param externalIdentitySignUpSignInPolicyId string = ''

@description('Expected token issuer, e.g. https://your-tenant.ciamlogin.com/<tenant-id>/v2.0/.')
param externalIdentityValidIssuer string

// Nuxt web app sign-in (Entra External ID, server-side OIDC + PKCE). All non-secret — the flow uses
// PKCE, not a client secret; the session-encryption key comes from Key Vault, not a param.
@description('App registration (client) ID of the Nuxt web app for the OIDC sign-in flow.')
param webExternalIdentityClientId string

@description('External ID tenant subdomain for the web app, e.g. gatherstead (→ gatherstead.ciamlogin.com).')
param webExternalIdentityTenantName string

@description('API scope the web app requests so its access token is audienced for the API, e.g. api://<api-client-id>/access_as_user.')
param webExternalIdentityApiScope string

resource rg 'Microsoft.Resources/resourceGroups@2024-03-01' = {
  name: resourceGroupName
  location: location
}

// Short deterministic suffix for resources whose names live in global DNS namespaces (Key Vault,
// SQL server, App Services). 6 chars keeps Key Vault within its 24-char cap; reused across the
// global-namespace resources for consistency.
var resourceToken = substring(uniqueString(rg.id), 0, 6)

module identity 'modules/identity.bicep' = {
  name: 'identity'
  scope: rg
  params: {
    location: location
    workload: workload
    environment: environment
    locationAbbreviation: locationAbbreviation
  }
}

module webIdentity 'modules/web-identity.bicep' = {
  name: 'web-identity'
  scope: rg
  params: {
    location: location
    workload: workload
    environment: environment
    locationAbbreviation: locationAbbreviation
  }
}

module observability 'modules/observability.bicep' = {
  name: 'observability'
  scope: rg
  params: {
    location: location
    workload: workload
    environment: environment
    locationAbbreviation: locationAbbreviation
    logRetentionDays: logRetentionDays
    oncallEmail: oncallEmail
    appManagedIdentityPrincipalId: identity.outputs.principalId
    deployDemo: deployDemo
  }
}

module keyvault 'modules/keyvault.bicep' = {
  name: 'keyvault'
  scope: rg
  params: {
    location: location
    workload: workload
    environment: environment
    locationAbbreviation: locationAbbreviation
    resourceToken: resourceToken
    appManagedIdentityPrincipalId: identity.outputs.principalId
    webManagedIdentityPrincipalId: webIdentity.outputs.principalId
    deployerObjectId: deployerObjectId
    workspaceId: observability.outputs.workspaceId
  }
}

module sql 'modules/sql.bicep' = {
  name: 'sql'
  scope: rg
  params: {
    location: location
    workload: workload
    environment: environment
    locationAbbreviation: locationAbbreviation
    resourceToken: resourceToken
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
    workload: workload
    environment: environment
    locationAbbreviation: locationAbbreviation
    resourceToken: resourceToken
    appServicePlanSku: appServicePlanSku
    appManagedIdentityId: identity.outputs.id
    appManagedIdentityClientId: identity.outputs.clientId
    sqlServerFqdn: sql.outputs.sqlServerFqdn
    sqlDatabaseName: sql.outputs.sqlDatabaseName
    keyVaultUri: keyvault.outputs.keyVaultUri
    workspaceId: observability.outputs.workspaceId
    appInsightsConnectionString: observability.outputs.appInsightsConnectionString
    externalIdentityInstance: externalIdentityInstance
    externalIdentityDomain: externalIdentityDomain
    externalIdentityClientId: externalIdentityClientId
    externalIdentitySignUpSignInPolicyId: externalIdentitySignUpSignInPolicyId
    externalIdentityValidIssuer: externalIdentityValidIssuer
    webManagedIdentityId: webIdentity.outputs.id
    webExternalIdentityClientId: webExternalIdentityClientId
    webExternalIdentityTenantName: webExternalIdentityTenantName
    webExternalIdentityApiScope: webExternalIdentityApiScope
  }
}

module ciIdentity 'modules/ci-identity.bicep' = {
  name: 'ci-identity'
  scope: rg
  params: {
    location: location
    workload: workload
    environment: environment
    locationAbbreviation: locationAbbreviation
    githubRepository: githubRepository
    githubBranch: githubBranch
  }
}

module demo 'modules/staticwebapp.bicep' = if (deployDemo) {
  name: 'demo'
  scope: rg
  params: {
    location: location
    workload: workload
    environment: environment
    locationAbbreviation: locationAbbreviation
    ciIdentityPrincipalId: ciIdentity.outputs.ciIdentityPrincipalId
  }
}

output managedIdentityName string = identity.outputs.name
output managedIdentityClientId string = identity.outputs.clientId
output sqlServerName string = sql.outputs.sqlServerName
output sqlDatabaseName string = sql.outputs.sqlDatabaseName
output sqlServerFqdn string = sql.outputs.sqlServerFqdn
output keyVaultUri string = keyvault.outputs.keyVaultUri
output keyVaultCmkId string = keyvault.outputs.cmkKeyId
output cmkKeyUri string = keyvault.outputs.cmkKeyUri
output apiAppName string = appservice.outputs.apiAppName
output apiAppUrl string = appservice.outputs.apiAppUrl
output webAppName string = appservice.outputs.webAppName
output webAppUrl string = appservice.outputs.webAppUrl
output appInsightsId string = observability.outputs.appInsightsId
output logAnalyticsWorkspaceId string = observability.outputs.workspaceId
// CI/CD: set ciIdentityClientId as the AZURE_CLIENT_ID GitHub secret used by ci-cd.yml.
output ciIdentityName string = ciIdentity.outputs.ciIdentityName
output ciIdentityClientId string = ciIdentity.outputs.ciIdentityClientId
output demoSiteUrl string = deployDemo ? demo.outputs.demoSiteUrl : ''
// Copy this into the DEMO_APPINSIGHTS_CONNECTION_STRING GitHub Actions secret used by ci-cd.yml.
output demoAppInsightsConnectionString string = observability.outputs.demoAppInsightsConnectionString
