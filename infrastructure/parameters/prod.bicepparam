using '../main.bicep'

param location = 'eastus'
param resourceGroupName = 'gatherstead-rg'

// Object ID of the Entra ID group designated as SQL administrator in production.
// Get with: az ad group show --group "<group-name>" --query id -o tsv
param sqlEntraAdminObjectId = '<prod-admin-group-object-id>'
param sqlEntraAdminLogin = '<prod-admin-group-name>'

// Object ID of the service principal used by CI/CD to run deployments.
// Get with: az ad sp show --id "<app-id>" --query id -o tsv
param deployerObjectId = '<cicd-service-principal-object-id>'

// Basic tier: 1 dedicated core, 1.75 GB RAM, always-on, custom domains (~$13/month/plan).
// Scale up to B2/B3/P1v3 as traffic grows.
param appServicePlanSku = 'B1'
