namespace AzureServices.Core.Options;

public class AzureSettings
{
    public const string SectionName = "Azure";

    public AppServiceOptions AppService { get; set; } = new();
    public KeyVaultOptions KeyVault { get; set; } = new();
    public StorageOptions Storage { get; set; } = new();
    public ServiceBusOptions ServiceBus { get; set; } = new();
    public ApplicationInsightsOptions ApplicationInsights { get; set; } = new();
    public EntraIdOptions EntraId { get; set; } = new();
}

public class AppServiceOptions
{
    public string EnvironmentName { get; set; } = "Development";
    public string DeploymentSlot { get; set; } = "production";
    public string AppServicePlanSku { get; set; } = "P1v3";
    public int MinimumInstances { get; set; } = 1;
    public int MaximumInstances { get; set; } = 5;
}

public class KeyVaultOptions
{
    public string VaultUri { get; set; } = "https://kv-azureservices-demo.vault.azure.net/";
    public bool Enabled { get; set; } = false; // When true, uses Managed Identity / Azure Key Vault
}

public class StorageOptions
{
    public string ConnectionString { get; set; } = "UseDevelopmentStorage=true";
    public string BlobEndpoint { get; set; } = "https://stazureservicesdemo.blob.core.windows.net/";
    public string QueueEndpoint { get; set; } = "https://stazureservicesdemo.queue.core.windows.net/";
    public string TableEndpoint { get; set; } = "https://stazureservicesdemo.table.core.windows.net/";
    public string FileEndpoint { get; set; } = "https://stazureservicesdemo.file.core.windows.net/";
    public string BlobContainerName { get; set; } = "order-documents";
    public string QueueName { get; set; } = "order-queue";
    public string TableName { get; set; } = "OrderAudits";
    public string FileShareName { get; set; } = "enterprise-files";
    public bool UseManagedIdentity { get; set; } = false;
}

public class ServiceBusOptions
{
    public string ConnectionString { get; set; } = string.Empty;
    public string FullyQualifiedNamespace { get; set; } = "sb-azureservices-demo.servicebus.windows.net";
    public string QueueName { get; set; } = "orders-queue";
    public string DeadLetterQueueSuffix { get; set; } = "/$DeadLetterQueue";
    public string TopicName { get; set; } = "orders-topic";
    public string HighPrioritySubscription { get; set; } = "sub-high-priority";
    public string NormalPrioritySubscription { get; set; } = "sub-normal-priority";
    public string AuditSubscription { get; set; } = "sub-audit-all";
    public bool UseManagedIdentity { get; set; } = false;
}

public class ApplicationInsightsOptions
{
    public string ConnectionString { get; set; } = string.Empty;
    public bool EnableAdaptiveSampling { get; set; } = true;
    public bool EnablePerformanceCounterCollectionModule { get; set; } = true;
}

public class EntraIdOptions
{
    public string Instance { get; set; } = "https://login.microsoftonline.com/";
    public string Domain { get; set; } = "contoso.onmicrosoft.com";
    public string TenantId { get; set; } = "common";
    public string ClientId { get; set; } = "00000000-0000-0000-0000-000000000000";
    public string Audience { get; set; } = "api://azure-services-demo";
}
