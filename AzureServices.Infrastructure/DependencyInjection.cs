using Azure.Core;
using Azure.Data.Tables;
using Azure.Identity;
using Azure.Messaging.ServiceBus;
using Azure.Security.KeyVault.Secrets;
using Azure.Storage.Blobs;
using Azure.Storage.Files.Shares;
using Azure.Storage.Queues;
using AzureServices.Core.Interfaces;
using AzureServices.Core.Options;
using AzureServices.Infrastructure.KeyVault;
using AzureServices.Infrastructure.Monitoring;
using AzureServices.Infrastructure.ServiceBus;
using AzureServices.Infrastructure.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AzureServices.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddAzureInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var azureSection = configuration.GetSection(AzureSettings.SectionName);
        var azureSettings = azureSection.Get<AzureSettings>() ?? new AzureSettings();
        services.Configure<AzureSettings>(azureSection);

        // 1. Azure Identity: DefaultAzureCredential supports Managed Identity in App Service/Functions,
        // Azure CLI, Visual Studio, and Environment Variables locally.
        TokenCredential credential = new DefaultAzureCredential(new DefaultAzureCredentialOptions
        {
            ExcludeInteractiveBrowserCredential = true
        });
        services.AddSingleton(credential);

        // 2. Azure Key Vault Client
        if (azureSettings.KeyVault.Enabled && Uri.TryCreate(azureSettings.KeyVault.VaultUri, UriKind.Absolute, out var vaultUri))
        {
            services.AddSingleton(new SecretClient(vaultUri, credential));
        }
        services.AddScoped<IKeyVaultService>(sp =>
        {
            var client = sp.GetService<SecretClient>();
            var logger = sp.GetRequiredService<ILogger<KeyVaultService>>();
            return new KeyVaultService(client, logger);
        });

        // 3. Azure Storage Services (Blob, Queue, Table, File Share)
        if (azureSettings.Storage.UseManagedIdentity)
        {
            services.AddSingleton(new BlobServiceClient(new Uri(azureSettings.Storage.BlobEndpoint), credential));
            services.AddSingleton(new QueueServiceClient(new Uri(azureSettings.Storage.QueueEndpoint), credential, new QueueClientOptions
            {
                MessageEncoding = QueueMessageEncoding.Base64
            }));
            services.AddSingleton(new TableServiceClient(new Uri(azureSettings.Storage.TableEndpoint), credential));
            services.AddSingleton(new ShareServiceClient(new Uri(azureSettings.Storage.FileEndpoint), credential));
        }
        else
        {
            string storageConn = string.IsNullOrWhiteSpace(azureSettings.Storage.ConnectionString)
                ? "UseDevelopmentStorage=true"
                : azureSettings.Storage.ConnectionString;

            services.AddSingleton(new BlobServiceClient(storageConn));
            services.AddSingleton(new QueueServiceClient(storageConn, new QueueClientOptions
            {
                MessageEncoding = QueueMessageEncoding.Base64
            }));
            services.AddSingleton(new TableServiceClient(storageConn));
            services.AddSingleton(new ShareServiceClient(storageConn));
        }

        services.AddScoped<IBlobStorageService, BlobStorageService>();
        services.AddScoped<IQueueStorageService, QueueStorageService>();
        services.AddScoped<ITableStorageService, TableStorageService>();
        services.AddScoped<IFileShareStorageService, FileShareStorageService>();

        // 4. Azure Service Bus Client
        if (azureSettings.ServiceBus.UseManagedIdentity && !string.IsNullOrWhiteSpace(azureSettings.ServiceBus.FullyQualifiedNamespace))
        {
            var sbOptions = new ServiceBusClientOptions
            {
                TransportType = ServiceBusTransportType.AmqpTcp,
                RetryOptions = new ServiceBusRetryOptions
                {
                    Mode = ServiceBusRetryMode.Exponential,
                    MaxRetries = 3,
                    Delay = TimeSpan.FromSeconds(1),
                    MaxDelay = TimeSpan.FromSeconds(10)
                }
            };
            services.AddSingleton(new ServiceBusClient(azureSettings.ServiceBus.FullyQualifiedNamespace, credential, sbOptions));
        }
        else if (!string.IsNullOrWhiteSpace(azureSettings.ServiceBus.ConnectionString))
        {
            var sbOptions = new ServiceBusClientOptions
            {
                TransportType = ServiceBusTransportType.AmqpTcp,
                RetryOptions = new ServiceBusRetryOptions
                {
                    Mode = ServiceBusRetryMode.Exponential,
                    MaxRetries = 3
                }
            };
            services.AddSingleton(new ServiceBusClient(azureSettings.ServiceBus.ConnectionString, sbOptions));
        }

        services.AddScoped<IServiceBusQueueService>(sp =>
        {
            var client = sp.GetService<ServiceBusClient>();
            var logger = sp.GetRequiredService<ILogger<ServiceBusQueueService>>();
            return new ServiceBusQueueService(client, logger);
        });

        services.AddScoped<IServiceBusTopicService>(sp =>
        {
            var client = sp.GetService<ServiceBusClient>();
            var logger = sp.GetRequiredService<ILogger<ServiceBusTopicService>>();
            return new ServiceBusTopicService(client, logger);
        });

        // 5. Application Insights Telemetry Service
        services.AddScoped<IAppInsightsTelemetryService, AppInsightsTelemetryService>();

        return services;
    }
}
