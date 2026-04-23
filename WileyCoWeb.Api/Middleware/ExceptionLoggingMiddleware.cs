using System.Diagnostics;

namespace WileyCoWeb.Api.Middleware;

/// <summary>
/// Middleware that captures and logs all unhandled exceptions with detailed context.
/// </summary>
public sealed class ExceptionLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionLoggingMiddleware> _logger;

    public ExceptionLoggingMiddleware(RequestDelegate next, ILogger<ExceptionLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();
        var requestId = Activity.Current?.Id ?? context.TraceIdentifier;

        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            _logger.LogError(
                ex,
                "Unhandled exception in request pipeline. " +
                "RequestId: {RequestId}, Path: {Path}, Method: {Method}, " +
                "StatusCode: {StatusCode}, Duration: {Duration}ms, " +
                "User: {User}, RemoteIp: {RemoteIp}",
                requestId,
                context.Request.Path,
                context.Request.Method,
                context.Response.StatusCode,
                stopwatch.ElapsedMilliseconds,
                context.User?.Identity?.Name ?? "Anonymous",
                context.Connection.RemoteIpAddress?.ToString() ?? "Unknown"
            );

            LogExceptionDetails(ex, requestId);

            throw;
        }
    }

    private void LogExceptionDetails(Exception ex, string requestId)
    {
        var exceptionChain = new List<string>();
        var currentException = ex;
        var depth = 0;

        while (currentException != null && depth < 10)
        {
            exceptionChain.Add($"[{depth}] {currentException.GetType().Name}: {currentException.Message}");
            currentException = currentException.InnerException;
            depth++;
        }

        _logger.LogDebug(
            "Exception chain for RequestId {RequestId}: {ExceptionChain}. StackTrace: {StackTrace}",
            requestId,
            string.Join(" -> ", exceptionChain),
            ex.StackTrace
        );
    }
}

public static class ExceptionLoggingMiddlewareExtensions
{
    public static IApplicationBuilder UseExceptionLogging(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<ExceptionLoggingMiddleware>();
    }
}
