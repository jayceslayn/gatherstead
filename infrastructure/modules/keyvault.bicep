@description('The Azure region where the Key Vault will be created.')
param location string

@description('The principal ID of the app managed identity (granted Crypto User + Secrets User).')
param appManagedIdentityPrincipalId string

@description('The principal ID of the CI managed identity (granted Crypto User for deploy-setup column encryption).')
param ciIdentityPrincipalId string

@description('The object ID of the deployer (granted Key Vault Administrator for initial setup).')
param deployerObjectId string

@description('Resource ID of the Log Analytics workspace for diagnostic settings.')
param workspaceId string

// Built-in role definition IDs
var keyVaultAdministratorRoleId = '00482a5a-887f-4fb3-b363-3b7fe8e74483'
var keyVaultCryptoUserRoleId = '12338af0-0e69-4776-bea7-57ae8d297424'
var keyVaultSecretsUserRoleId = '4633458b-17de-408a-b874-0445c86b69e6'

var keyVaultName = 'gat-kv-${uniqueString(resourceGroup().id)}'

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
  }
}

// Column Master Key for Always Encrypted.
//
// Rotation policy — intentionally none (Option A):
//   - No auto-rotation. A Key Vault `Rotate` action only creates a new key *version*; Always
//     Encrypted binds the CEK to a specific key, so version rotation is invisible to it. CMK
//     rotation is a manual ceremony (create new CMK → re-sign the CEK with both → roll apps →
//     retire old) and is tracked operationally (calendar / ag-gatherstead-oncall), not by KV.
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
    keyOps: [
      'unwrapKey'
      'wrapKey'
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

// CI managed identity: Key Vault Crypto User — the deploy-setup job (Gatherstead.Data.Setup,
// run as the CI identity) wraps/unwraps the CMK to create the CEK and run ALTER COLUMN ...
// ENCRYPTED. No Secrets User: deploy-setup reads no Key Vault secrets.
resource ciCryptoUserRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(keyVault.id, ciIdentityPrincipalId, keyVaultCryptoUserRoleId)
  scope: keyVault
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', keyVaultCryptoUserRoleId)
    principalId: ciIdentityPrincipalId
    principalType: 'ServicePrincipal'
  }
}

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
output cmkKeyId string = cmkKey.id
