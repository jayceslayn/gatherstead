@description('The Azure region for the Static Web App.')
param location string

@description('Workload token used in CAF resource names.')
param workload string

@description('Environment token used in CAF resource names.')
param environment string

@description('Region abbreviation used in CAF resource names.')
param locationAbbreviation string

@description('Principal ID of the CI managed identity, granted Contributor so the pipeline can fetch the SWA deployment token at runtime.')
param ciIdentityPrincipalId string

// Contributor — includes Microsoft.Web/staticSites/listSecrets/action used by `az staticwebapp secrets list`.
var contributorRoleId = 'b24988ac-6180-42a0-ab88-20f7382dd24c'

resource demoSite 'Microsoft.Web/staticSites@2023-12-01' = {
  name: 'stapp-${workload}-demo-${environment}-${locationAbbreviation}'
  location: location
  sku: {
    name: 'Free'
    tier: 'Free'
  }
  properties: {
    buildProperties: {
      appLocation: 'src/Gatherstead.Web'
      outputLocation: '.output/public'
    }
  }
}

// Lets the CI identity read the deployment token (Microsoft.Web/staticSites/listSecrets/action).
resource ciDemoDeployRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(demoSite.id, ciIdentityPrincipalId, contributorRoleId)
  scope: demoSite
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', contributorRoleId)
    principalId: ciIdentityPrincipalId
    principalType: 'ServicePrincipal'
  }
}

output demoSiteUrl string = demoSite.properties.defaultHostname
