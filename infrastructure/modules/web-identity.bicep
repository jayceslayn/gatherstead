@description('The Azure region where the managed identity will be created.')
param location string

@description('Workload token used in CAF resource names.')
param workload string

@description('Environment token used in CAF resource names.')
param environment string

@description('Region abbreviation used in CAF resource names.')
param locationAbbreviation string

// Dedicated identity for the web App Service. Granted ONLY Key Vault Secrets User (in keyvault.bicep)
// so a web-app compromise cannot reach the CMK or SQL the app identity holds — least privilege.
var webIdentityName = 'id-${workload}-web-${environment}-${locationAbbreviation}'

resource webIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: webIdentityName
  location: location
}

output id string = webIdentity.id
output name string = webIdentity.name
output principalId string = webIdentity.properties.principalId
output clientId string = webIdentity.properties.clientId
