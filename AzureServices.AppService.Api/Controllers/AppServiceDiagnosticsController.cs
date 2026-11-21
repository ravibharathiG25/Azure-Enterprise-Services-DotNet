using System.Diagnostics;
using System.Runtime.InteropServices;
using AzureServices.Core.Models;
using AzureServices.Core.Options;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace AzureServices.AppService.Api.Controllers;

[ApiController]
[Route("api/appservice")]
public class AppServiceDiagnosticsController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly AzureSettings _azureSettings;

    public AppServiceDiagnosticsController(
        IConfiguration configuration,
        IOptions<AzureSettings> azureSettings)
    {
        _configuration = configuration;
        _azureSettings = azureSettings.Value;
    }

    /// <summary>
    /// Demonstrates Azure App Service Runtime Environment, Hosting, and Process Information.
    /// In Azure App Service, variables like WEBSITE_SITE_NAME, WEBSITE_INSTANCE_ID, and REGION_NAME are injected.
    /// </summary>
    [HttpGet("runtime-info")]
    public ActionResult<ApiResponse<object>> GetRuntimeInfo()
    {
        var currentProcess = Process.GetCurrentProcess();

        var info = new
        {
            Framework = RuntimeInformation.FrameworkDescription,
            OSArchitecture = RuntimeInformation.OSArchitecture.ToString(),
            OSDescription = RuntimeInformation.OSDescription,
            ProcessId = currentProcess.Id,
            WorkingSetMB = Math.Round((double)currentProcess.WorkingSet64 / (1024 * 1024), 2),
            ThreadCount = currentProcess.Threads.Count,
            Uptime = DateTime.UtcNow - currentProcess.StartTime.ToUniversalTime(),
            MachineName = Environment.MachineName,
            ProcessorCount = Environment.ProcessorCount,

            // Azure App Service specific environment variables
            AzureAppService = new
            {
                WebsiteSiteName = Environment.GetEnvironmentVariable("WEBSITE_SITE_NAME") ?? "local-iis-express-or-kestrel",
                WebsiteInstanceId = Environment.GetEnvironmentVariable("WEBSITE_INSTANCE_ID") ?? "local-instance-01",
                WebsiteSku = Environment.GetEnvironmentVariable("WEBSITE_SKU") ?? _azureSettings.AppService.AppServicePlanSku,
                WebsiteSlotName = Environment.GetEnvironmentVariable("WEBSITE_SLOT_NAME") ?? _azureSettings.AppService.DeploymentSlot,
                WebsiteRegion = Environment.GetEnvironmentVariable("REGION_NAME") ?? "local-dev",
                AppServicePlanSku = _azureSettings.AppService.AppServicePlanSku,
                MinInstances = _azureSettings.AppService.MinimumInstances,
                MaxInstances = _azureSettings.AppService.MaximumInstances
            }
        };

        return Ok(ApiResponse<object>.Ok(info, "Azure App Service runtime information retrieved successfully."));
    }

    /// <summary>
    /// Demonstrates Azure App Service Configuration management.
    /// In Azure App Service, App Settings override appsettings.json seamlessly.
    /// </summary>
    [HttpGet("configuration")]
    public ActionResult<ApiResponse<object>> GetConfigurationOverview()
    {
        var configSnapshot = new
        {
            Environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development",
            StorageAccountConfigured = !string.IsNullOrEmpty(_azureSettings.Storage.ConnectionString),
            ServiceBusConfigured = !string.IsNullOrEmpty(_azureSettings.ServiceBus.ConnectionString) || !string.IsNullOrEmpty(_azureSettings.ServiceBus.FullyQualifiedNamespace),
            KeyVaultConfigured = _azureSettings.KeyVault.Enabled,
            ApplicationInsightsConfigured = !string.IsNullOrEmpty(_configuration["ApplicationInsights:ConnectionString"]),
            EntraIdAudience = _azureSettings.EntraId.Audience
        };

        return Ok(ApiResponse<object>.Ok(configSnapshot, "Configuration summary loaded."));
    }

    /// <summary>
    /// Simulates horizontal scale-out behavior on Azure App Service.
    /// </summary>
    [HttpGet("scale-check")]
    public ActionResult<ApiResponse<object>> ScaleCheck()
    {
        var scaleInfo = new
        {
            InstanceId = Environment.GetEnvironmentVariable("WEBSITE_INSTANCE_ID") ?? Environment.MachineName,
            AppServicePlanSku = _azureSettings.AppService.AppServicePlanSku,
            CurrentScaleRule = "Auto-scale rule: CPU > 70% for 5 mins -> Scale out by 1 instance (up to 5)",
            ScaleInRule = "Auto-scale rule: CPU < 30% for 5 mins -> Scale in by 1 instance (down to 1)",
            Recommendation = "For production high-availability, use Premium (P1v3+) or Standard (S1+) tiers for autoscaling and deployment slots."
        };

        return Ok(ApiResponse<object>.Ok(scaleInfo, "App Service scaling metrics inspected."));
    }
}
