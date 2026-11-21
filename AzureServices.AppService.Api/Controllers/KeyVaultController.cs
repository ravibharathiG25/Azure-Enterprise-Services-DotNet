using AzureServices.Core.Interfaces;
using AzureServices.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AzureServices.AppService.Api.Controllers;

[ApiController]
[Route("api/keyvault")]
public class KeyVaultController : ControllerBase
{
    private readonly IKeyVaultService _keyVaultService;
    private readonly IConfiguration _configuration;

    public KeyVaultController(IKeyVaultService keyVaultService, IConfiguration configuration)
    {
        _keyVaultService = keyVaultService;
        _configuration = configuration;
    }

    /// <summary>
    /// Lists secret metadata and names stored in Azure Key Vault.
    /// Requires Key Vault Secrets User / Reader role via Microsoft Entra ID.
    /// </summary>
    [HttpGet("secrets")]
    public async Task<ActionResult<ApiResponse<IReadOnlyDictionary<string, string>>>> ListSecrets(CancellationToken cancellationToken)
    {
        var secrets = await _keyVaultService.GetAllSecretPropertiesAsync(cancellationToken);
        return Ok(ApiResponse<IReadOnlyDictionary<string, string>>.Ok(secrets, "Secret properties retrieved from Azure Key Vault."));
    }

    /// <summary>
    /// Retrieves a secret value securely from Azure Key Vault.
    /// </summary>
    [HttpGet("secrets/{secretName}")]
    public async Task<ActionResult<ApiResponse<string>>> GetSecret(string secretName, CancellationToken cancellationToken)
    {
        var value = await _keyVaultService.GetSecretAsync(secretName, cancellationToken);
        return Ok(ApiResponse<string>.Ok(value, $"Secret '{secretName}' retrieved."));
    }

    /// <summary>
    /// Sets or rotates a secret in Azure Key Vault with automatic expiration.
    /// Requires Admin or Contributor role in Microsoft Entra ID.
    /// </summary>
    [HttpPost("secrets")]
    public async Task<ActionResult<ApiResponse<string>>> SetSecret([FromBody] SetSecretRequest request, CancellationToken cancellationToken)
    {
        await _keyVaultService.SetSecretAsync(request.SecretName, request.SecretValue, cancellationToken);
        return Ok(ApiResponse<string>.Ok(request.SecretName, $"Secret '{request.SecretName}' successfully created/updated in Azure Key Vault."));
    }

    /// <summary>
    /// Demonstrates how Azure Key Vault Configuration Provider seamlessly feeds secrets into ASP.NET Core IConfiguration.
    /// Secrets named 'Database--Password' in Key Vault automatically bind to configuration key 'Database:Password'.
    /// </summary>
    [HttpGet("config-provider-demo")]
    public ActionResult<ApiResponse<object>> DemonstrateConfigProvider()
    {
        var sampleKey = "DemoSecret";
        var resolvedValue = _configuration[sampleKey] ?? "(Value not in local config or Key Vault)";

        var info = new
        {
            SecretLookupKey = sampleKey,
            ResolvedValue = resolvedValue,
            Explanation = "The Azure Key Vault Configuration Provider maps '--' in secret names to ':' in ASP.NET Core configuration keys."
        };

        return Ok(ApiResponse<object>.Ok(info, "Key Vault Configuration Provider demo executed."));
    }
}

public class SetSecretRequest
{
    public string SecretName { get; set; } = string.Empty;
    public string SecretValue { get; set; } = string.Empty;
}
