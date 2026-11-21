# Azure Enterprise Services Ecosystem in .NET

A production-grade, enterprise-ready reference solution and comprehensive study guide demonstrating how to architect, develop, configure, and monitor cloud-native applications in **.NET 8** across major Microsoft Azure PaaS and Serverless services:

1. **Azure App Service**: Web hosting, deployment slots, auto-scaling, configuration, and health checks.
2. **Azure Functions & Durable Functions**: Serverless execution, triggers (HTTP, Service Bus, Blob), and stateful workflow orchestration.
3. **Azure Storage Services**: Complete implementation of **Blob**, **Queue**, **Table**, and **File Share** storage.
4. **Microsoft Entra ID & Azure Key Vault**: Secure identity management, `DefaultAzureCredential`, Managed Identity, and seamless configuration provider secrets integration.
5. **Azure Service Bus**: Queues (1-to-1 decoupling, DLQ), Topics & Subscriptions (1-to-many pub/sub with SQL filters), and message retry policies.
6. **Azure Application Insights**: Distributed tracing, W3C TraceContext correlation, custom events, custom metrics, and exception analysis.

---

## 🏛 Solution Architecture

```
                       ┌──────────────────────────────────────────────────────────┐
                       │                   Microsoft Entra ID                     │
                       │           (Authentication & Managed Identity)            │
                       └───────────┬──────────────────────────────────┬───────────┘
                                   │                                  │
                                   ▼                                  ▼
┌──────────────────────────────────────┐                  ┌──────────────────────────────┐
│           Azure Key Vault            │                  │  Azure Application Insights   │
│   (Secrets & Configuration Provider) │                  │  (Logs, Traces, Telemetry)   │
└──────────────────┬───────────────────┘                  └──────────────┬───────────────┘
                   │                                                     │
                   ▼                                                     ▼
┌─────────────────────────────────────────────────────────────────────────────────────────┐
│                    Azure App Service (ASP.NET Core Web API)                             │
│  - Hosting: Linux / Container / P1v3                                                    │
│  - Scaling: Auto-scale CPU > 70%                                                        │
│  - Zero-Downtime Deployment: Staging Slot -> Production Swap                            │
│  - Health Probes: /healthz, /readyz, /livez                                             │
└──────────────┬───────────────────────────┬──────────────────────────────┬───────────────┘
               │                           │                              │
               ▼                           ▼                              ▼
┌──────────────────────────────┐ ┌───────────────────┐ ┌──────────────────────────────────┐
│    Azure Storage Services    │ │ Azure Service Bus │ │     Azure Functions (Serverless) │
│ ├─ Blob: Invoices & Files    │ │ ├─ Queue (Orders) │ │ ├─ HTTP Trigger                  │
│ ├─ Queue: Notifications      │ │ ├─ Dead-Letter Q  │ │ ├─ Service Bus Queue Trigger     │
│ ├─ Table: High-speed Audits  │ │ └─ Topic (Pub/Sub)│ │ ├─ Blob Trigger                  │
│ └─ File Share: Cloud SMB     │ │    └─ Subscriptions│ │ └─ Durable Functions Orchestrator│
└──────────────────────────────┘ └───────────────────┘ └──────────────────────────────────┘
```

---

## 📂 Project Structure

```
D:\Program files\.net\Azure services\
├── AzureEnterpriseServices.slnx         # Solution file
├── AzureServices.Core/                  # Domain contracts, models, DTOs & interfaces
│   ├── Interfaces/                      # IBlobStorageService, IServiceBusQueueService, etc.
│   ├── Models/                          # Order, OrderAuditTableEntity, ServiceBusOrderMessage
│   └── Options/                         # AzureSettings configuration options
├── AzureServices.Infrastructure/        # Concrete Azure SDK implementations
│   ├── KeyVault/                        # KeyVaultService
│   ├── Monitoring/                     # AppInsightsTelemetryService
│   ├── ServiceBus/                      # ServiceBusQueueService, ServiceBusTopicService
│   ├── Storage/                         # Blob, Queue, Table, and FileShare services
│   └── DependencyInjection.cs           # Central DI registration with Managed Identity
├── AzureServices.AppService.Api/        # Azure App Service Web API Host
│   ├── Controllers/                     # Storage, ServiceBus, AppService, Monitoring controllers
│   ├── Middleware/                      # ExceptionHandlingMiddleware + Correlation ID
│   ├── Program.cs                       # App configuration, Entra ID auth, Swagger, Health Checks
│   ├── Dockerfile                       # Container deployment for Linux App Service
│   └── appsettings.json                 # Unified Azure settings
├── AzureServices.Functions/             # Azure Functions Isolated Worker (.NET 8)
│   ├── Durable/                         # OrderWorkflowOrchestrator, Activities & HttpStarter
│   ├── Triggers/                        # OrderHttpTrigger, ServiceBusTrigger, BlobTrigger
│   ├── host.json                        # Functions host configuration & telemetry sampling
│   ├── local.settings.json              # Local development connection strings
│   └── Program.cs                       # Serverless host builder
├── deploy/
│   └── main.bicep                       # Complete Infrastructure as Code (IaC) template
└── .github/workflows/
    └── azure-deploy.yml                 # CI/CD pipeline with staging slot zero-downtime swap
```

---

## 📘 Comprehensive Study Guide & Concepts

### 1. Azure App Service
* **Application Hosting**: Fully managed platform (PaaS) supporting Linux and Windows. Automatically handles OS patching, load balancing, SSL/TLS termination, and HTTP/2.
* **Configuration Management**:
  * Azure Portal App Settings and Connection Strings are injected as environment variables at runtime.
  * Hierarchical JSON keys in `appsettings.json` (e.g. `Azure:Storage:BlobEndpoint`) are overridden in Azure Portal by setting `Azure__Storage__BlobEndpoint`.
* **Deployment Slots**:
  * Allows deploying to a non-production slot (e.g., `staging`) first.
  * Execute smoke tests and health checks on staging.
  * Swap slots seamlessly: Azure performs traffic routing warming before DNS redirection, ensuring **zero downtime**.
  * Use "Sticky Settings" (`slotSetting: true`) for configurations that should not follow the swap (e.g., slot-specific database connection strings).
* **Scaling**:
  * **Scale Up (Vertical)**: Change App Service Plan tier (e.g., Free -> Basic -> Standard -> Premium v3).
  * **Scale Out (Horizontal)**: Automatically increase instance count based on metrics (e.g. CPU > 70% or queue depth) or predefined schedules.
* **Health Checks**:
  * App Service health check probes ping endpoints like `/healthz` or `/readyz`.
  * If an instance fails the probe consecutively, App Service removes it from the load balancer rotation and auto-heals/replaces the faulty instance.

---

### 2. Azure Functions & Durable Functions
* **Serverless Execution**:
  * Event-driven, sub-second auto-scaling from 0 to 200+ instances.
  * **Isolated Worker Model (.NET 8)**: The user code runs in a separate process (`dotnet-isolated`) independent of the Azure Functions host runtime, providing full dependency injection control, custom middleware, and no assembly conflicts.
* **Triggers**:
  * `HttpTrigger`: Synchronous HTTP endpoints with route parameter binding.
  * `ServiceBusTrigger`: Listens to Service Bus queues/topics, processing batches with automatic lock renewal.
  * `BlobTrigger`: Fired when a blob is uploaded or updated in a storage container.
* **Durable Functions & Stateful Orchestration**:
  * **Deterministic Replay**: Orchestrator code must be deterministic; execution pauses at `await context.CallActivityAsync(...)` and replays from history tables upon event completion.
  * **Implemented Orchestration Patterns**:
    1. **Function Chaining**: Validate Order $\rightarrow$ Charge Payment $\rightarrow$ Upload Blob $\rightarrow$ Save Table Record.
    2. **Human-in-the-loop (External Events)**: For orders $> \$1,000$, pauses and waits up to 5 minutes for a `"ManagerApproval"` event before completion (`context.WaitForExternalEvent`).
    3. **Durable Timers**: Deterministic delays and timeouts (`context.CreateTimer`).

---

### 3. Azure Storage Services (Comparison & Use Cases)

| Service | Primary Use Case | Access Protocol | Key Characteristics |
| :--- | :--- | :--- | :--- |
| **Azure Blob Storage** | Unstructured object storage (PDFs, images, backups, big data) | REST / HTTPS | Access tiers: Hot, Cool, Cold, Archive. Secure time-limited SAS tokens. |
| **Azure Queue Storage** | Simple, high-volume async task queuing | REST / HTTPS | Up to 64 KB per message; visibility timeout pattern; millions of messages. |
| **Azure Table Storage** | Ultra-fast, schemaless NoSQL key-value store | REST / OData | Scalable partitioning via `PartitionKey` and `RowKey`; very low cost. |
| **Azure Files (Shares)**| Fully managed cloud file shares | SMB 3.0 / NFS / REST | Mountable as network drive in Windows/Linux/containers. Shared app configs. |

---

### 4. Microsoft Entra ID & Azure Key Vault
* **Authentication & Authorization**:
  * Entra ID (formerly Azure AD) issues secure OAuth 2.0 / OIDC JWT tokens.
  * Configured via `Microsoft.Identity.Web` in ASP.NET Core with role-based policies (`RequireAdminRole`).
* **DefaultAzureCredential & Managed Identity**:
  * Eliminates storing credentials in source control or configuration files.
  * When deployed to Azure (App Service, Functions, VM), it uses the **System-Assigned Managed Identity**.
  * When running locally, it seamlessly attempts Azure CLI, Visual Studio, or Environment credentials.
* **Azure Key Vault**:
  * Centralized secret, key, and certificate management.
  * In this application, secrets are loaded via the `Azure.Extensions.AspNetCore.Configuration.Secrets` provider:
    ```csharp
    builder.Configuration.AddAzureKeyVault(vaultUri, new DefaultAzureCredential());
    ```
  * Secrets in Key Vault named `ConnectionStrings--SqlDatabase` transparently bind to `IConfiguration["ConnectionStrings:SqlDatabase"]`.

---

### 5. Azure Service Bus
* **Queues (Point-to-Point)**:
  * 1-to-1 asynchronous message decoupling between producer and consumer.
  * **Dead-Letter Queue (DLQ)**: Poison messages or messages exceeding `MaxDeliveryCount` are automatically isolated in `orders-queue/$DeadLetterQueue` for inspection.
  * **Exponential Backoff**: Configured via `ServiceBusRetryOptions` to handle transient network hiccups.
* **Topics & Subscriptions (Publish-Subscribe)**:
  * 1-to-many event broadcast.
  * Each subscription can have independent **SQL Filters** (e.g. `Priority = 'High'` or `Region = 'eastus'`), routing only relevant events to dedicated workers.

---

### 6. Azure Application Insights
* **Observability Pillars**:
  * **Distributed Tracing**: Automatically correlates calls across App Service HTTP requests, Service Bus messages (`Diagnostic-Id`), and Azure Functions.
  * **Custom Events**: Business event tracking via `TelemetryClient.TrackEvent("OrderPlaced", properties, metrics)`.
  * **Custom Metrics**: Numeric performance tracking via `TrackMetric("OrderAmountUSD", amount)`.
  * **Exception Analysis**: Captures full stack traces, correlation IDs, and unhandled exception rates via custom middleware.
  * **Dependency Tracking**: Measures duration and failure rates of outbound calls (HTTP, SQL, Storage).
* **Sample KQL (Kusto Query Language) Queries**:
  ```kql
  // Find top failed operations in the last 24 hours
  requests
  | where success == false
  | summarize count() by name, resultCode
  | order by count_ desc

  // Correlate order exceptions with custom properties
  exceptions
  | where timestamp > ago(1h)
  | project timestamp, problemId, customDimensions.CorrelationId, innermostMessage
  ```

---

## 🚀 Running and Testing Locally

### Prerequisites
* [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
* Azure Storage Emulator / [Azurite](https://learn.microsoft.com/en-us/azure/storage/common/storage-use-azurite) (or local connection string)

### 1. Build the Solution
```powershell
Set-Location "D:\Program files\.net\Azure services"
dotnet build AzureEnterpriseServices.slnx
```

### 2. Run the App Service Web API
```powershell
Set-Location "D:\Program files\.net\Azure services\AzureServices.AppService.Api"
dotnet run
```
Navigate your browser to:
👉 **`http://localhost:5000`** or **`https://localhost:5001`** to explore the interactive **Swagger / OpenAPI UI**.

### 3. Key Endpoints to Try
* `GET /api/appservice/runtime-info`: Inspect Azure App Service hosting & environment details.
* `POST /api/orders`: Create an order that executes across Blob Storage, Table Storage, Service Bus, and Application Insights.
* `POST /api/blobs/upload-text`: Upload a file to Azure Blob Storage and generate a SAS URI.
* `POST /api/queues/messages`: Enqueue messages to Azure Storage Queue.
* `POST /api/servicebus/topic/publish`: Publish events with SQL filter properties.
* `GET /api/monitoring/simulate-exception`: Test Application Insights exception telemetry.
* `GET /healthz`: Verify Azure App Service health probe response.

### 4. Run Azure Functions
```powershell
Set-Location "D:\Program files\.net\Azure services\AzureServices.Functions"
dotnet run
```
Trigger the Durable Order Workflow:
```http
POST http://localhost:7071/api/orchestrators/OrderWorkflow
Content-Type: application/json

{
  "customerId": "CUST-9001",
  "customerName": "Alice Smith",
  "amount": 1500.00
}
```

---

## ☁️ Deploying to Azure

### Automated Deployment via Bicep
```powershell
# 1. Login to Azure
az login

# 2. Create Resource Group
az group create --name rg-azureservices-prod --location eastus

# 3. Deploy all Azure resources
az deployment group create `
  --resource-group rg-azureservices-prod `
  --template-file deploy/main.bicep
```
