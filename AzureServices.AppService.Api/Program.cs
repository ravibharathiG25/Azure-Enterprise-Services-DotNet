using Azure.Identity;
using AzureServices.AppService.Api.Middleware;
using AzureServices.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Identity.Web;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// 1. Azure Application Insights Integration
// Captures HTTP requests, exceptions, live metrics, and distributed tracing.
builder.Services.AddApplicationInsightsTelemetry();

// 2. Azure Key Vault Configuration Provider Integration
// Transparently loads secrets from Key Vault into IConfiguration so they override appsettings.json.
var keyVaultEnabled = builder.Configuration.GetValue<bool>("Azure:KeyVault:Enabled");
var keyVaultUri = builder.Configuration["Azure:KeyVault:VaultUri"];
if (keyVaultEnabled && Uri.TryCreate(keyVaultUri, UriKind.Absolute, out var kvUri))
{
    try
    {
        builder.Configuration.AddAzureKeyVault(kvUri, new DefaultAzureCredential());
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[KeyVault] Warning: Failed to connect to Key Vault on startup: {ex.Message}");
    }
}

// 3. Register Azure Infrastructure Services (Storage, ServiceBus, KeyVault, Telemetry)
builder.Services.AddAzureInfrastructure(builder.Configuration);

// 4. Microsoft Entra ID Authentication & Authorization
// Validates JWT bearer tokens issued by Entra ID (Azure AD).
var entraTenantId = builder.Configuration["Azure:EntraId:TenantId"];
var entraClientId = builder.Configuration["Azure:EntraId:ClientId"];
bool isEntraConfigured = !string.IsNullOrWhiteSpace(entraClientId) && entraClientId != "00000000-0000-0000-0000-000000000000";

if (isEntraConfigured)
{
    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddMicrosoftIdentityWebApi(builder.Configuration.GetSection("Azure:EntraId"));
}
else
{
    // Local / Dev Fallback: Allow anonymous or simulated JWT bearer scheme
    builder.Services.AddAuthentication("DevBypassScheme")
        .AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, DevBypassAuthHandler>("DevBypassScheme", _ => { });
}

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("RequireAdminRole", policy => policy.RequireRole("Admin", "GlobalAdmin"));
    options.AddPolicy("RequireUserRole", policy => policy.RequireAuthenticatedUser());
});

// 5. App Service Health Checks for Azure Health Probe & Auto-healing
builder.Services.AddHealthChecks();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// 6. Swagger / OpenAPI Configuration
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Azure Enterprise Services Ecosystem API",
        Version = "v1",
        Description = "Comprehensive .NET reference implementation demonstrating Azure App Service, Storage (Blob/Queue/Table/File), Key Vault, Service Bus, Entra ID, and Application Insights."
    });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "Microsoft Entra ID JWT Bearer Token. Example: \"Bearer {token}\"",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Azure Services Ecosystem v1");
    c.RoutePrefix = string.Empty; // Serve Swagger UI at application root
});

app.UseMiddleware<ExceptionHandlingMiddleware>();

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

// Health Check Endpoints for Azure App Service & Traffic Manager / Front Door
app.MapHealthChecks("/healthz");
app.MapHealthChecks("/readyz");
app.MapHealthChecks("/livez");

app.MapControllers();

app.Run();

// Development authentication bypass handler for local evaluation without cloud Entra ID
public class DevBypassAuthHandler : Microsoft.AspNetCore.Authentication.AuthenticationHandler<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions>
{
    public DevBypassAuthHandler(
        Microsoft.Extensions.Options.IOptionsMonitor<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        System.Text.Encodings.Web.UrlEncoder encoder)
        : base(options, logger, encoder) { }

    protected override Task<Microsoft.AspNetCore.Authentication.AuthenticateResult> HandleAuthenticateAsync()
    {
        var claims = new[]
        {
            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, "dev-local-user-id"),
            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Name, "Dev Azure User"),
            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Role, "Admin"),
            new System.Security.Claims.Claim("scp", "Orders.ReadWrite Documents.Manage")
        };
        var identity = new System.Security.Claims.ClaimsIdentity(claims, Scheme.Name);
        var principal = new System.Security.Claims.ClaimsPrincipal(identity);
        var ticket = new Microsoft.AspNetCore.Authentication.AuthenticationTicket(principal, Scheme.Name);
        return Task.FromResult(Microsoft.AspNetCore.Authentication.AuthenticateResult.Success(ticket));
    }
}
