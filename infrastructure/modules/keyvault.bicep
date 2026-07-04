@description('The Azure region where the Key Vault will be created.')
param location string

@description('Workload token used in CAF resource names.')
param workload string

@description('Environment token used in CAF resource names.')
param environment string

@description('Region abbreviation used in CAF resource names.')
param locationAbbreviation string

@description('Short deterministic suffix ensuring the globally-unique Key Vault name is free.')
param resourceToken string

@description('The principal ID of the app managed identity (granted Crypto User + Secrets User).')
param appManagedIdentityPrincipalId string

@description('The principal ID of the web managed identity (granted Secrets User for the NUXT_SESSION_PASSWORD reference).')
param webManagedIdentityPrincipalId string

@description('The object ID of the deployer (granted Key Vault Administrator for initial setup).')
param deployerObjectId string

@description('Resource ID of the Log Analytics workspace for diagnostic settings.')
param workspaceId string

// Built-in role definition IDs
var keyVaultAdministratorRoleId = '00482a5a-887f-4fb3-b363-3b7fe8e74483'
var keyVaultCryptoUserRoleId = '12338af0-0e69-4776-bea7-57ae8d297424'
var keyVaultSecretsUserRoleId = '4633458b-17de-408a-b874-0445c86b69e6'

// 'kv-gat-prod-wus2-xxxxxx' = 23 chars, within the Key Vault 24-char limit.
var keyVaultName = 'kv-${workload}-${environment}-${locationAbbreviation}-${resourceToken}'

resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: keyVaultName
  location: location
  properties: {
    sku: {
      family: 'A'
      name: 'premium'
    }
    tenantId: subscription().tenantId
    enableRbacAuthorization: true
    softDeleteRetentionInDays: 7
    enableSoftDelete: true
    // Purge protection prevents a soft-deleted vault OR key from being permanently purged inside the
    // retention window. The CMK (cmk-gatherstead) wraps the Always Encrypted CEK: purging it would render
    // every encrypted PII column permanently undecryptable, so protection against accidental/malicious
    // purge is mandatory. NOTE: this flag is irreversible once enabled on a vault (by Azure design).
    enablePurgeProtection: true
  }
}

// Column Master Key for Always Encrypted.
//
// Rotation policy — intentionally none (Option A):
//   - No auto-rotation. A Key Vault `Rotate` action only creates a new key *version*; Always
//     Encrypted binds the CEK to a specific key, so version rotation is invisible to it. CMK
//     rotation is a manual ceremony (create new CMK → re-sign the CEK with both → roll apps →
//     retire old) and is tracked operationally (calendar / ag-gat-oncall-*), not by KV.
//   - No expiry (no `attributes.exp`). An expired CMK cannot unwrapKey, which would block
//     decryption of every encrypted column — an outage whose blast radius is the whole dataset.
//   Deploying with no `rotationPolicy` strips the inert Azure-default "notify 30 days before
//   expiry" policy (harmless: with no expiry it never fires) and the drift does not recur.
//
// Option B (adopt at a higher maturity stage, once a manual-rotation runbook + ownership exist):
//   add a Notify-only policy with a long expiry as a compliance forcing-function, e.g.
//     rotationPolicy: {
//       attributes: { expiryTime: 'P2Y' }
//       lifetimeActions: [ { trigger: { timeBeforeExpiry: 'P90D' }, action: { type: 'Notify' } } ]
//     }
//   NB: never add a `Rotate` action, and the expiry becomes a hard deadline — the CMK must be
//   rotated before it lapses or encrypted-column access breaks.
resource cmkKey 'Microsoft.KeyVault/vaults/keys@2023-07-01' = {
  parent: keyVault
  name: 'cmk-gatherstead'
  properties: {
    kty: 'RSA'
    keySize: 2048
    // Always Encrypted wraps the CEK with the CMK (wrapKey/unwrapKey) AND signs the encrypted-CEK
    // metadata — key path + ciphertext — with the CMK, verifying it on read. sign/verify are therefore
    // required; omitting them fails CEK creation with "Operation sign is not permitted on this key".
    keyOps: [
      'unwrapKey'
      'wrapKey'
      'sign'
      'verify'
    ]
  }
  dependsOn: [deployerAdminRole]
}

// Deployer: Key Vault Administrator — needed to create keys and secrets during setup
resource deployerAdminRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(keyVault.id, deployerObjectId, keyVaultAdministratorRoleId)
  scope: keyVault
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', keyVaultAdministratorRoleId)
    principalId: deployerObjectId
    principalType: 'User'
  }
}

// App managed identity: Key Vault Crypto User — for CMK wrap/unwrap (Always Encrypted)
resource appCryptoUserRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(keyVault.id, appManagedIdentityPrincipalId, keyVaultCryptoUserRoleId)
  scope: keyVault
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', keyVaultCryptoUserRoleId)
    principalId: appManagedIdentityPrincipalId
    principalType: 'ServicePrincipal'
  }
}

// NOTE: The CI managed identity is intentionally NOT granted Key Vault Crypto User. Always Encrypted
// setup (CEK wrap/unwrap, column encryption) is run manually by a SQL admin, not by CI — see
// docs/DEPLOYMENT.md. CI does migrations only and needs no Key Vault access.

// App managed identity: Key Vault Secrets User — for reading the PASETO public key secret
resource appSecretsUserRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(keyVault.id, appManagedIdentityPrincipalId, keyVaultSecretsUserRoleId)
  scope: keyVault
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', keyVaultSecretsUserRoleId)
    principalId: appManagedIdentityPrincipalId
    principalType: 'ServicePrincipal'
  }
}

// Web managed identity: Key Vault Secrets User — App Service resolves the NUXT_SESSION_PASSWORD
// Key Vault reference with this identity. Secrets only — no key (CMK) or SQL access.
resource webSecretsUserRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(keyVault.id, webManagedIdentityPrincipalId, keyVaultSecretsUserRoleId)
  scope: keyVault
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', keyVaultSecretsUserRoleId)
    principalId: webManagedIdentityPrincipalId
    principalType: 'ServicePrincipal'
  }
}

resource keyVaultDiag 'Microsoft.Insights/diagnosticSettings@2021-05-01-preview' = {
  name: 'diag-kv'
  scope: keyVault
  properties: {
    workspaceId: workspaceId
    logs: [
      { category: 'AuditEvent', enabled: true }
      { category: 'AzurePolicyEvaluationDetails', enabled: true }
    ]
    metrics: [
      { category: 'AllMetrics', enabled: true }
    ]
  }
}

output keyVaultUri string = keyVault.properties.vaultUri
// ARM resource ID — NOT a valid Always Encrypted master-key path.
output cmkKeyId string = cmkKey.id
// Key Vault key URL (with version) — this is the value Gatherstead.Data.Setup expects as its
// <cmkKeyUrl> argument; see docs/DEPLOYMENT.md.
output cmkKeyUri string = cmkKey.properties.keyUriWithVersion
