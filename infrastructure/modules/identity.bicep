@description('The Azure region where the managed identity will be created.')
param location string

@description('Workload token used in CAF resource names.')
param workload string

@description('Environment token used in CAF resource names.')
param environment string

@description('Region abbreviation used in CAF resource names.')
param locationAbbreviation string

var identityName = 'id-${workload}-${environment}-${locationAbbreviation}'

resource identity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: identityName
  location: location
}

output id string = identity.id
output name string = identity.name
output principalId string = identity.properties.principalId
output clientId string = identity.properties.clientId
