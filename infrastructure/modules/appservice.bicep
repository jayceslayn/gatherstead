@description('The Azure region where App Service resources will be created.')
param location string

@description('Workload token used in CAF resource names.')
param workload string

@description('Environment token used in CAF resource names.')
param environment string

@description('Region abbreviation used in CAF resource names.')
param locationAbbreviation string

@description('Short deterministic suffix ensuring the globally-unique App Service names are free.')
param resourceToken string

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

@description('Resource ID of the Log Analytics workspace for diagnostic settings.')
param workspaceId string

@description('Application Insights connection string injected into both apps.')
param appInsightsConnectionString string

@description('External ID instance/authority host, e.g. https://your-tenant.ciamlogin.com.')
param externalIdentityInstance string

@description('External ID domain, e.g. your-tenant.onmicrosoft.com.')
param externalIdentityDomain string

@description('App registration (client) ID of the API — used as the JWT audience.')
param externalIdentityClientId string

@description('B2C sign-up/sign-in policy (user flow) ID. Empty for Entra External ID.')
param externalIdentitySignUpSignInPolicyId string = ''

@description('Expected token issuer, e.g. https://your-tenant.ciamlogin.com/<tenant-id>/v2.0/.')
param externalIdentityValidIssuer string

@description('Full resource ID of the dedicated web managed identity (used for Key Vault references).')
param webManagedIdentityId string

@description('App registration (client) ID of the Nuxt web app for the OIDC sign-in flow.')
param webExternalIdentityClientId string

@description('External ID tenant subdomain for the web app, e.g. gatherstead (→ gatherstead.ciamlogin.com).')
param webExternalIdentityTenantName string

@description('The API scope the web app requests so its access token is audienced for the API, e.g. api://<api-client-id>/access_as_user.')
param webExternalIdentityApiScope string

var planName = 'asp-${workload}-${environment}-${locationAbbreviation}'
var apiAppName = 'app-${workload}-api-${environment}-${locationAbbreviation}-${resourceToken}'
var webAppName = 'app-${workload}-web-${environment}-${locationAbbreviation}-${resourceToken}'

// Column Encryption Setting=Enabled turns on Always Encrypted (encrypt on write / decrypt on read,
// plus equality over deterministic columns) — none of which engages the enclave. No attestation
// protocol is set: this database uses VBS enclaves with no attestation (preferredEnclaveType=VBS in
// sql.bicep), so enclave operations work without one — and an HGS/AAS protocol here would instead
// force attestation against an endpoint that does not exist and fail. The manual encryption setup
// (Gatherstead.Data.Setup) connects the same way and successfully drives enclave-based in-place
// ALTER COLUMN, confirming attestation is not required.
var connectionString = 'Server=tcp:${sqlServerFqdn},1433;Database=${sqlDatabaseName};Authentication=Active Directory Managed Identity;User Id=${appManagedIdentityClientId};Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;Column Encryption Setting=Enabled;'

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
        { name: 'APPLICATIONINSIGHTS_CONNECTION_STRING', value: appInsightsConnectionString }
        { name: 'ApplicationInsightsAgent_EXTENSION_VERSION', value: '~3' }
        // Allow requests from the web app origin
        { name: 'Cors__AllowedOrigins__0', value: 'https://${webAppName}.azurewebsites.net' }
        // External identity provider (Entra External ID / Azure AD B2C) — JWT bearer validation.
        // `__` maps to the nested `ExternalIdentity:*` config keys consumed in Program.cs.
        { name: 'ExternalIdentity__Instance', value: externalIdentityInstance }
        { name: 'ExternalIdentity__Domain', value: externalIdentityDomain }
        { name: 'ExternalIdentity__ClientId', value: externalIdentityClientId }
        { name: 'ExternalIdentity__SignUpSignInPolicyId', value: externalIdentitySignUpSignInPolicyId }
        { name: 'ExternalIdentity__ValidIssuer', value: externalIdentityValidIssuer }
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
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${webManagedIdentityId}': {}
    }
  }
  properties: {
    serverFarmId: plan.id
    httpsOnly: true
    // Resolve @Microsoft.KeyVault(...) app-setting references with the web identity (required when the
    // site uses a user-assigned identity rather than a system-assigned one).
    keyVaultReferenceIdentity: webManagedIdentityId
    siteConfig: {
      linuxFxVersion: 'NODE|24-lts'
      alwaysOn: appServicePlanSku != 'F1'
      appCommandLine: 'node .output/server/index.mjs'
      appSettings: [
        // Overrides runtimeConfig.public.apiBaseUrl in nuxt.config.ts
        { name: 'NUXT_PUBLIC_API_BASE_URL', value: 'https://${apiAppName}.azurewebsites.net' }
        { name: 'WEBSITE_RUN_FROM_PACKAGE', value: '1' }
        { name: 'APPLICATIONINSIGHTS_CONNECTION_STRING', value: appInsightsConnectionString }
        { name: 'ApplicationInsightsAgent_EXTENSION_VERSION', value: '~3' }
        // Browser-facing key for the App Insights JS SDK (runtimeConfig.public.appInsightsConnectionString).
        // Same prod App Insights as the backend → end-to-end frontend/backend trace correlation.
        { name: 'NUXT_PUBLIC_APP_INSIGHTS_CONNECTION_STRING', value: appInsightsConnectionString }
        // Entra External ID (server-side OIDC authorization-code + PKCE) — bind
        // runtimeConfig.externalIdentity.* in nuxt.config.ts. The code is redeemed server-side as a
        // confidential web client, so a client secret is required (resolved from Key Vault below).
        { name: 'NUXT_EXTERNAL_IDENTITY_CLIENT_ID', value: webExternalIdentityClientId }
        { name: 'NUXT_EXTERNAL_IDENTITY_TENANT_NAME', value: webExternalIdentityTenantName }
        { name: 'NUXT_EXTERNAL_IDENTITY_API_SCOPE', value: webExternalIdentityApiScope }
        // Web app registration client secret — resolved from Key Vault at runtime via the web identity.
        // Create the secret out-of-band: az keyvault secret set --name web-external-identity-client-secret.
        { name: 'NUXT_EXTERNAL_IDENTITY_CLIENT_SECRET', value: '@Microsoft.KeyVault(SecretUri=${keyVaultUri}secrets/web-external-identity-client-secret/)' }
        // nuxt-auth-utils session encryption key — resolved from Key Vault at runtime via the web
        // identity. Create the secret out-of-band: az keyvault secret set --name nuxt-session-password.
        { name: 'NUXT_SESSION_PASSWORD', value: '@Microsoft.KeyVault(SecretUri=${keyVaultUri}secrets/nuxt-session-password/)' }
      ]
    }
  }
}

// Diagnostic settings for the API app — streams HTTP, console, application, and audit logs
// to Log Analytics. Effective on B1+ SKU; F1 (free) provisions the resource but collects no data.
resource apiAppDiag 'Microsoft.Insights/diagnosticSettings@2021-05-01-preview' = {
  name: 'diag-api'
  scope: apiApp
  properties: {
    workspaceId: workspaceId
    logs: [
      { category: 'AppServiceHTTPLogs', enabled: true }
      { category: 'AppServiceConsoleLogs', enabled: true }
      { category: 'AppServiceAppLogs', enabled: true }
      { category: 'AppServiceAuditLogs', enabled: true }
      { category: 'AppServiceIPSecAuditLogs', enabled: true }
      { category: 'AppServicePlatformLogs', enabled: true }
    ]
    metrics: [
      { category: 'AllMetrics', enabled: true }
    ]
  }
}

resource webAppDiag 'Microsoft.Insights/diagnosticSettings@2021-05-01-preview' = {
  name: 'diag-web'
  scope: webApp
  properties: {
    workspaceId: workspaceId
    logs: [
      { category: 'AppServiceHTTPLogs', enabled: true }
      { category: 'AppServiceConsoleLogs', enabled: true }
      { category: 'AppServiceAppLogs', enabled: true }
      { category: 'AppServiceAuditLogs', enabled: true }
      { category: 'AppServiceIPSecAuditLogs', enabled: true }
      { category: 'AppServicePlatformLogs', enabled: true }
    ]
    metrics: [
      { category: 'AllMetrics', enabled: true }
    ]
  }
}

output apiAppName string = apiApp.name
output apiAppUrl string = 'https://${apiApp.properties.defaultHostName}'
output webAppName string = webApp.name
output webAppUrl string = 'https://${webApp.properties.defaultHostName}'
