@description('The Azure region for the Static Web App.')
param location string

resource demoSite 'Microsoft.Web/staticSites@2023-12-01' = {
  name: 'gat-demo-swa'
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

output demoSiteUrl string = demoSite.properties.defaultHostname
