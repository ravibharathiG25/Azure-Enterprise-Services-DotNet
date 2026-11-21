namespace AzureServices.Core.Models;

public class StorageFileItem
{
    public string Name { get; set; } = string.Empty;
    public string Uri { get; set; } = string.Empty;
    public long? ContentLength { get; set; }
    public string? ContentType { get; set; }
    public string? AccessTier { get; set; }
    public DateTimeOffset? LastModified { get; set; }
    public IDictionary<string, string>? Metadata { get; set; }
}
