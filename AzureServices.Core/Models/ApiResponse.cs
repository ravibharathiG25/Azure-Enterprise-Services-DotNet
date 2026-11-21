namespace AzureServices.Core.Models;

public class ApiResponse<T>
{
    public bool Success { get; set; } = true;
    public string Message { get; set; } = string.Empty;
    public T? Data { get; set; }
    public string? CorrelationId { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    public static ApiResponse<T> Ok(T data, string message = "Success", string? correlationId = null) =>
        new() { Success = true, Message = message, Data = data, CorrelationId = correlationId };

    public static ApiResponse<T> Fail(string message, string? correlationId = null) =>
        new() { Success = false, Message = message, Data = default, CorrelationId = correlationId };
}
