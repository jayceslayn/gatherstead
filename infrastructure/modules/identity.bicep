@description('The Azure region where the managed identity will be created.')
param location string

var identityName = 'gat-id-${uniqueString(resourceGroup().id)}'

resource identity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: identityName
  location: location
}

output id string = identity.id
output name string = identity.name
output principalId string = identity.properties.principalId
output clientId string = identity.properties.clientId
