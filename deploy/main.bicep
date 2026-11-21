@description('Primary location for all resources')
param location string = resourceGroup().location

@description('Unique suffix for resource names')
param environmentName string = 'prod'

var uniqueSuffix = uniqueString(resourceGroup().id)
var appServicePlanName = 'asp-azureservices-${environmentName}'
var webAppName = 'app-azureservices-${environmentName}-${uniqueSuffix}'
var storageAccountName = 'st${uniqueSuffix}'
var keyVaultName = 'kv-${uniqueSuffix}'
var serviceBusNamespaceName = 'sb-${uniqueSuffix}'
var appInsightsName = 'appi-${environmentName}-${uniqueSuffix}'
var logAnalyticsWorkspaceName = 'law-${environmentName}-${uniqueSuffix}'

// 1. Log Analytics Workspace
resource logAnalyticsWorkspace 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: logAnalyticsWorkspaceName
  location: location
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: 30
  }
}

// 2. Azure Application Insights
resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: appInsightsName
  location: location
  kind: 'web'
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: logAnalyticsWorkspace.id
    publicNetworkAccessForIngestion: 'Enabled'
    publicNetworkAccessForQuery: 'Enabled'
  }
}

// 3. Azure Storage Account (Blob, Queue, Table, File Share)
resource storageAccount 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: storageAccountName
  location: location
  sku: {
    name: 'Standard_LRS'
  }
  kind: 'StorageV2'
  properties: {
    minimumTlsVersion: 'TLS1_2'
    supportsHttpsTrafficOnly: true
    accessTier: 'Hot'
    allowBlobPublicAccess: false
  }
}

resource blobService 'Microsoft.Storage/storageAccounts/blobServices@2023-05-01' = {
  parent: storageAccount
  name: 'default'
}

resource orderDocumentsContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-05-01' = {
  parent: blobService
  name: 'order-documents'
  properties: {
    publicAccess: 'None'
  }
}

resource queueService 'Microsoft.Storage/storageAccounts/queueServices@2023-05-01' = {
  parent: storageAccount
  name: 'default'
}

resource orderQueue 'Microsoft.Storage/storageAccounts/queueServices/queues@2023-05-01' = {
  parent: queueService
  name: 'order-queue'
}

resource tableService 'Microsoft.Storage/storageAccounts/tableServices@2023-05-01' = {
  parent: storageAccount
  name: 'default'
}

resource orderAuditsTable 'Microsoft.Storage/storageAccounts/tableServices/tables@2023-05-01' = {
  parent: tableService
  name: 'OrderAudits'
}

resource fileService 'Microsoft.Storage/storageAccounts/fileServices@2023-05-01' = {
  parent: storageAccount
  name: 'default'
}

resource enterpriseFilesShare 'Microsoft.Storage/storageAccounts/fileServices/shares@2023-05-01' = {
  parent: fileService
  name: 'enterprise-files'
  properties: {
    shareQuota: 5120
  }
}

// 4. Azure Key Vault
resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: keyVaultName
  location: location
  properties: {
    sku: {
      family: 'A'
      name: 'standard'
    }
    tenantId: subscription().tenantId
    enableRbacAuthorization: true
    enableSoftDelete: true
    softDeleteRetentionInDays: 90
  }
}

// 5. Azure Service Bus (Namespace, Queue with DLQ, Topic, Subscriptions with SQL Filters)
resource serviceBusNamespace 'Microsoft.ServiceBus/namespaces@2022-10-01-preview' = {
  name: serviceBusNamespaceName
  location: location
  sku: {
    name: 'Standard'
    tier: 'Standard'
  }
}

resource serviceBusQueue 'Microsoft.ServiceBus/namespaces/queues@2022-10-01-preview' = {
  parent: serviceBusNamespace
  name: 'orders-queue'
  properties: {
    deadLetteringOnMessageExpiration: true
    maxDeliveryCount: 5
    lockDuration: 'PT1M'
    defaultMessageTimeToLive: 'P14D'
  }
}

resource serviceBusTopic 'Microsoft.ServiceBus/namespaces/topics@2022-10-01-preview' = {
  parent: serviceBusNamespace
  name: 'orders-topic'
}

resource subHighPriority 'Microsoft.ServiceBus/namespaces/topics/subscriptions@2022-10-01-preview' = {
  parent: serviceBusTopic
  name: 'sub-high-priority'
  properties: {
    maxDeliveryCount: 5
  }
}

resource highPriorityRule 'Microsoft.ServiceBus/namespaces/topics/subscriptions/rules@2022-10-01-preview' = {
  parent: subHighPriority
  name: 'HighPrioritySqlFilter'
  properties: {
    filterType: 'SqlFilter'
    sqlFilter: {
      sqlExpression: 'Priority = \'High\' OR Priority = \'Critical\''
    }
  }
}

resource subAuditAll 'Microsoft.ServiceBus/namespaces/topics/subscriptions@2022-10-01-preview' = {
  parent: serviceBusTopic
  name: 'sub-audit-all'
  properties: {
    maxDeliveryCount: 5
  }
}

// 6. Azure App Service Plan (with Auto-scale support)
resource appServicePlan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: appServicePlanName
  location: location
  sku: {
    name: 'P1v3'
    tier: 'PremiumV3'
    capacity: 1
  }
  properties: {
    reserved: true // Linux
  }
}

// 7. Azure App Service Web App (Production Slot + Managed Identity)
resource webApp 'Microsoft.Web/sites@2023-12-01' = {
  name: webAppName
  location: location
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: appServicePlan.id
    httpsOnly: true
    siteConfig: {
      linuxFxVersion: 'DOTNETCORE|8.0'
      alwaysOn: true
      healthCheckPath: '/healthz'
      appSettings: [
        {
          name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
          value: appInsights.properties.ConnectionString
        }
        {
          name: 'Azure__Storage__BlobEndpoint'
          value: storageAccount.properties.primaryEndpoints.blob
        }
        {
          name: 'Azure__Storage__QueueEndpoint'
          value: storageAccount.properties.primaryEndpoints.queue
        }
        {
          name: 'Azure__Storage__TableEndpoint'
          value: storageAccount.properties.primaryEndpoints.table
        }
        {
          name: 'Azure__Storage__FileEndpoint'
          value: storageAccount.properties.primaryEndpoints.file
        }
        {
          name: 'Azure__Storage__UseManagedIdentity'
          value: 'true'
        }
        {
          name: 'Azure__ServiceBus__FullyQualifiedNamespace'
          value: '${serviceBusNamespace.name}.servicebus.windows.net'
        }
        {
          name: 'Azure__ServiceBus__UseManagedIdentity'
          value: 'true'
        }
        {
          name: 'Azure__KeyVault__VaultUri'
          value: keyVault.properties.vaultUri
        }
        {
          name: 'Azure__KeyVault__Enabled'
          value: 'true'
        }
      ]
    }
  }
}

// 8. Staging Deployment Slot for Zero-Downtime Swaps
resource stagingSlot 'Microsoft.Web/sites/slots@2023-12-01' = {
  parent: webApp
  name: 'staging'
  location: location
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: appServicePlan.id
    siteConfig: {
      linuxFxVersion: 'DOTNETCORE|8.0'
      alwaysOn: true
      healthCheckPath: '/healthz'
    }
  }
}

// 9. Auto-scale Settings (Scale out on CPU > 70%, Scale in on CPU < 30%)
resource autoScaleSetting 'Microsoft.Insights/autoscalesettings@2022-10-01' = {
  name: 'autoscale-${appServicePlanName}'
  location: location
  properties: {
    targetResourceUri: appServicePlan.id
    enabled: true
    profiles: [
      {
        name: 'DefaultAutoscaleProfile'
        capacity: {
          minimum: '1'
          maximum: '5'
          default: '1'
        }
        rules: [
          {
            metricTrigger: {
              metricName: 'CpuPercentage'
              metricResourceUri: appServicePlan.id
              timeGrain: 'PT1M'
              statistic: 'Average'
              timeWindow: 'PT5M'
              timeAggregation: 'Average'
              operator: 'GreaterThan'
              threshold: 70
            }
            scaleAction: {
              direction: 'Increase'
              type: 'ChangeCount'
              value: '1'
              cooldown: 'PT5M'
            }
          }
          {
            metricTrigger: {
              metricName: 'CpuPercentage'
              metricResourceUri: appServicePlan.id
              timeGrain: 'PT1M'
              statistic: 'Average'
              timeWindow: 'PT5M'
              timeAggregation: 'Average'
              operator: 'LessThan'
              threshold: 30
            }
            scaleAction: {
              direction: 'Decrease'
              type: 'ChangeCount'
              value: '1'
              cooldown: 'PT5M'
            }
          }
        ]
      }
    ]
  }
}

output webAppUrl string = 'https://${webApp.properties.defaultHostName}'
output stagingSlotUrl string = 'https://${stagingSlot.properties.defaultHostName}'
output keyVaultUri string = keyVault.properties.vaultUri
output storageAccountName string = storageAccount.name
output serviceBusNamespace string = serviceBusNamespace.name
