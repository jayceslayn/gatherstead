@description('The Azure region for observability resources.')
param location string

@description('Number of days to retain logs in the workspace. Use 30 for dev, 90 for prod.')
@minValue(30)
@maxValue(730)
param logRetentionDays int = 30

@description('Email address for oncall alert notifications.')
param oncallEmail string

@description('Principal ID of the app managed identity (granted Monitoring Metrics Publisher on App Insights).')
param appManagedIdentityPrincipalId string

var workspaceName = 'gat-law-${uniqueString(resourceGroup().id)}'
var appInsightsName = 'gat-ai-${uniqueString(resourceGroup().id)}'

// Built-in: Monitoring Metrics Publisher — allows the managed identity to ingest telemetry via AAD auth
var monitoringMetricsPublisherRoleId = '3913510d-42f4-4e42-8a64-420c390055eb'

resource workspace 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: workspaceName
  location: location
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: logRetentionDays
    features: {
      enableLogAccessUsingOnlyResourcePermissions: true
    }
  }
}

resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: appInsightsName
  location: location
  kind: 'web'
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: workspace.id
    IngestionMode: 'LogAnalytics'
    publicNetworkAccessForIngestion: 'Enabled'
    publicNetworkAccessForQuery: 'Enabled'
  }
}

// Allows the app managed identity to publish telemetry with AAD-authenticated ingestion
resource metricsPublisherRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(appInsights.id, appManagedIdentityPrincipalId, monitoringMetricsPublisherRoleId)
  scope: appInsights
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', monitoringMetricsPublisherRoleId)
    principalId: appManagedIdentityPrincipalId
    principalType: 'ServicePrincipal'
  }
}

resource actionGroup 'Microsoft.Insights/actionGroups@2023-01-01' = {
  name: 'ag-gatherstead-oncall'
  location: 'global'
  properties: {
    groupShortName: 'gat-oncall'
    enabled: true
    emailReceivers: [
      {
        name: 'oncall-email'
        emailAddress: oncallEmail
        useCommonAlertSchema: true
      }
    ]
  }
}

// Sev 2 — fires when failed API requests exceed 5 in a 5-minute window.
// Requires Phase 2 OTel SDK wiring before data flows to App Insights.
resource alertFailedRequests 'Microsoft.Insights/metricAlerts@2018-03-01' = {
  name: 'gat-alert-failed-requests'
  location: 'global'
  properties: {
    description: 'API failed request count exceeded threshold — investigate recent deployments or errors.'
    severity: 2
    enabled: true
    scopes: [appInsights.id]
    evaluationFrequency: 'PT1M'
    windowSize: 'PT5M'
    criteria: {
      'odata.type': 'Microsoft.Azure.Monitor.SingleResourceMultipleMetricCriteria'
      allOf: [
        {
          name: 'FailedRequests'
          metricName: 'requests/failed'
          metricNamespace: 'microsoft.insights/components'
          operator: 'GreaterThan'
          threshold: 5
          timeAggregation: 'Count'
          criterionType: 'StaticThresholdCriterion'
        }
      ]
    }
    actions: [
      {
        actionGroupId: actionGroup.id
      }
    ]
    autoMitigate: true
  }
}

// Sev 1 — fires when SQL or other dependency failures exceed 3 in a 5-minute window.
// Requires Phase 2 OTel SDK wiring before data flows to App Insights.
resource alertDependencyFailures 'Microsoft.Insights/metricAlerts@2018-03-01' = {
  name: 'gat-alert-dependency-failures'
  location: 'global'
  properties: {
    description: 'Dependency failure count (SQL, Key Vault, etc.) exceeded threshold — may indicate downstream outage.'
    severity: 1
    enabled: true
    scopes: [appInsights.id]
    evaluationFrequency: 'PT1M'
    windowSize: 'PT5M'
    criteria: {
      'odata.type': 'Microsoft.Azure.Monitor.SingleResourceMultipleMetricCriteria'
      allOf: [
        {
          name: 'FailedDependencies'
          metricName: 'dependencies/failed'
          metricNamespace: 'microsoft.insights/components'
          operator: 'GreaterThan'
          threshold: 3
          timeAggregation: 'Count'
          criterionType: 'StaticThresholdCriterion'
        }
      ]
    }
    actions: [
      {
        actionGroupId: actionGroup.id
      }
    ]
    autoMitigate: true
  }
}

// TODO (Phase 4): Add scheduled query alert for CrossTenantWriteBlocked events in SecurityEvent table.
// TODO (Phase 5): Add metric alert for gatherstead.authn.failed custom metric burst (Sev 3).

output workspaceId string = workspace.id
output appInsightsConnectionString string = appInsights.properties.ConnectionString
output appInsightsId string = appInsights.id
output actionGroupId string = actionGroup.id
