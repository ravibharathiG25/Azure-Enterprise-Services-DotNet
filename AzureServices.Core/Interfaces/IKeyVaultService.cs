namespace AzureServices.Core.Interfaces;

public interface IKeyVaultService
{
    Task<string> GetSecretAsync(string secretName, CancellationToken cancellationToken = default);
    Task SetSecretAsync(string secretName, string secretValue, CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<string, string>> GetAllSecretPropertiesAsync(CancellationToken cancellationToken = default);
    Task DeleteSecretAsync(string secretName, CancellationToken cancellationToken = default);
}
