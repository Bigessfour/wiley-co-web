using System.Diagnostics;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using WileyWidget.Data;

namespace WileyCoWeb.Api.Middleware;

/// <summary>
/// Maps domain and infrastructure exceptions to RFC 7807 ProblemDetails responses.
/// </summary>
public sealed class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;
    private readonly IHostEnvironment _environment;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger, IHostEnvironment environment)
    {
        _logger = logger;
        _environment = environment;
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

        var (statusCode, title, detail) = MapException(exception, _environment);

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
        else if (exception is DuplicateImportException dup)
        {
            problem.Extensions["entityName"] = dup.EntityName;
        }

        httpContext.Response.StatusCode = statusCode;
        await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken).ConfigureAwait(false);
        return true;
    }

    private static (int StatusCode, string Title, string Detail) MapException(Exception exception, IHostEnvironment environment)
    {
        bool isDevelopment = environment.IsDevelopment();

        string Sanitize500(string raw) =>
            isDevelopment ? raw : "An unexpected error occurred. Reference the trace ID when contacting support.";

        return exception switch
        {
            ConcurrencyConflictException => (
                StatusCodes.Status409Conflict,
                "Concurrency conflict",
                exception.Message),
            DuplicateImportException dup => (
                StatusCodes.Status409Conflict,
                "Duplicate import",
                dup.Message),
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
            DbUpdateException dbEx when IsPostgresUniqueViolation(dbEx) => (
                StatusCodes.Status409Conflict,
                "Duplicate import",
                "A duplicate record was detected (unique constraint violation). The operation was rejected."),
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
                Sanitize500(exception.Message))
        };
    }

    private static bool IsDuplicateImport(InvalidOperationException exception)
    {
        // Prefer typed DuplicateImportException (handled in switch); fallback to legacy string match for minimal diff.
        return exception.Message.Contains("Duplicate QuickBooks", StringComparison.OrdinalIgnoreCase)
            || exception.Message.Contains("already imported", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsPostgresUniqueViolation(DbUpdateException ex)
    {
        var full = (ex.Message ?? string.Empty) + " " + (ex.InnerException?.Message ?? string.Empty) + " " + ex.ToString();
        if (full.Contains("23505", StringComparison.Ordinal))
            return true;
        if (full.Contains("duplicate key value violates unique constraint", StringComparison.OrdinalIgnoreCase))
            return true;
        if (full.Contains("UNIQUE constraint failed", StringComparison.OrdinalIgnoreCase)) // SQLite fallback for tests
            return true;

        // Detect Npgsql PostgresException without requiring assembly reference in this compilation unit
        var innerType = ex.InnerException?.GetType();
        if (innerType != null && innerType.Name.Equals("PostgresException", StringComparison.Ordinal))
        {
            var sqlState = innerType.GetProperty("SqlState")?.GetValue(ex.InnerException) as string;
            if (string.Equals(sqlState, "23505", StringComparison.Ordinal))
                return true;
        }

        return false;
    }
}
