using System.Diagnostics;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using WileyWidget.Data;

namespace WileyCoWeb.Api.Middleware;

/// <summary>
/// Maps domain and infrastructure exceptions to RFC 7807 ProblemDetails responses.
/// </summary>
public sealed class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var requestId = Activity.Current?.Id ?? httpContext.TraceIdentifier;
        _logger.LogError(
            exception,
            "Unhandled exception. RequestId={RequestId} Path={Path} Method={Method}",
            requestId,
            httpContext.Request.Path,
            httpContext.Request.Method);

        var (statusCode, title, detail) = MapException(exception);

        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = detail,
            Instance = httpContext.Request.Path
        };
        problem.Extensions["traceId"] = requestId;

        if (exception is ConcurrencyConflictException conflict)
        {
            problem.Extensions["entityName"] = conflict.EntityName;
        }

        httpContext.Response.StatusCode = statusCode;
        await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken).ConfigureAwait(false);
        return true;
    }

    private static (int StatusCode, string Title, string Detail) MapException(Exception exception)
    {
        return exception switch
        {
            ConcurrencyConflictException => (
                StatusCodes.Status409Conflict,
                "Concurrency conflict",
                exception.Message),
            NotSupportedException => (
                StatusCodes.Status501NotImplemented,
                "Not supported",
                exception.Message),
            ArgumentException => (
                StatusCodes.Status400BadRequest,
                "Invalid request",
                exception.Message),
            KeyNotFoundException => (
                StatusCodes.Status404NotFound,
                "Not found",
                exception.Message),
            InvalidOperationException invalid when IsDuplicateImport(invalid) => (
                StatusCodes.Status409Conflict,
                "Duplicate import",
                invalid.Message),
            InvalidOperationException => (
                StatusCodes.Status400BadRequest,
                "Invalid operation",
                exception.Message),
            _ => (
                StatusCodes.Status500InternalServerError,
                "An unexpected error occurred.",
                exception.Message)
        };
    }

    private static bool IsDuplicateImport(InvalidOperationException exception)
    {
        return exception.Message.Contains("Duplicate QuickBooks", StringComparison.OrdinalIgnoreCase)
            || exception.Message.Contains("already imported", StringComparison.OrdinalIgnoreCase);
    }
}
