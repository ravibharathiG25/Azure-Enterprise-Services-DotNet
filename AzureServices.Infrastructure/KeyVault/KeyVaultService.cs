using Azure.Security.KeyVault.Secrets;
using AzureServices.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace AzureServices.Infrastructure.KeyVault;

public class KeyVaultService : IKeyVaultService
{
    private readonly SecretClient? _secretClient;
    private readonly ILogger<KeyVaultService> _logger;

    public KeyVaultService(SecretClient? secretClient, ILogger<KeyVaultService> logger)
    {
        _secretClient = secretClient;
        _logger = logger;
    }

    public async Task<string> GetSecretAsync(string secretName, CancellationToken cancellationToken = default)
    {
        if (_secretClient == null)
        {
            _logger.LogWarning("Azure Key Vault is not configured or disabled in settings.");
            return $"[Mock/Fallback-Secret-For-{secretName}]";
        }

        _logger.LogInformation("Retrieving secret '{SecretName}' from Azure Key Vault", secretName);
        KeyVaultSecret secret = await _secretClient.GetSecretAsync(secretName, cancellationToken: cancellationToken);
        return secret.Value;
    }

    public async Task SetSecretAsync(string secretName, string secretValue, CancellationToken cancellationToken = default)
    {
        if (_secretClient == null)
        {
            _logger.LogWarning("Azure Key Vault is not configured. Secret '{SecretName}' was not stored in cloud.", secretName);
            return;
        }

        _logger.LogInformation("Setting secret '{SecretName}' in Azure Key Vault", secretName);
        var secret = new KeyVaultSecret(secretName, secretValue)
        {
            Properties =
            {
                ContentType = "text/plain",
                ExpiresOn = DateTimeOffset.UtcNow.AddYears(1)
            }
        };
        await _secretClient.SetSecretAsync(secret, cancellationToken);
    }

    public async Task<IReadOnlyDictionary<string, string>> GetAllSecretPropertiesAsync(CancellationToken cancellationToken = default)
    {
        if (_secretClient == null)
        {
            return new Dictionary<string, string>
            {
                { "DatabaseConnectionString", "Secret masked (Key Vault simulated)" },
                { "ServiceBusPrimaryKey", "Secret masked (Key Vault simulated)" }
            };
        }

        var results = new Dictionary<string, string>();
        await foreach (SecretProperties prop in _secretClient.GetPropertiesOfSecretsAsync(cancellationToken))
        {
            results[prop.Name] = $"Enabled: {prop.Enabled}, Expires: {prop.ExpiresOn?.ToString("u") ?? "Never"}";
        }

        return results;
    }

    public async Task DeleteSecretAsync(string secretName, CancellationToken cancellationToken = default)
    {
        if (_secretClient == null)
        {
            return;
        }

        _logger.LogInformation("Scheduling soft-delete for secret '{SecretName}' in Azure Key Vault", secretName);
        await _secretClient.StartDeleteSecretAsync(secretName, cancellationToken);
    }
}
