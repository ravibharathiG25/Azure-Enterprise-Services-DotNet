using System.Net;
using System.Text.Json;
using AzureServices.Core.Interfaces;
using AzureServices.Core.Models;

namespace AzureServices.AppService.Api.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, IAppInsightsTelemetryService telemetryService)
    {
        string correlationId = context.TraceIdentifier;

        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception occurred. CorrelationId: {CorrelationId}", correlationId);

            telemetryService.TrackException(ex, new Dictionary<string, string>
            {
                { "CorrelationId", correlationId },
                { "Path", context.Request.Path },
                { "Method", context.Request.Method }
            });

            context.Response.ContentType = "application/json";
            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;

            var response = ApiResponse<string>.Fail(
                $"An error occurred processing your request: {ex.Message}",
                correlationId);

            var json = JsonSerializer.Serialize(response);
            await context.Response.WriteAsync(json);
        }
    }
}
