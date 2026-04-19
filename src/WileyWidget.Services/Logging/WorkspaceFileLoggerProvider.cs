using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging;

namespace WileyWidget.Services.Logging;

/// <summary>
/// Writes application logs to a workspace-root file beneath the shared logs folder.
/// </summary>
public sealed class WorkspaceFileLoggerProvider : ILoggerProvider
{
    private readonly string _logFilePath;

    public WorkspaceFileLoggerProvider(string fileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        var logsDirectory = LogPathResolver.GetLogDirectory();
        _logFilePath = Path.Combine(logsDirectory, fileName);
    }

    public ILogger CreateLogger(string categoryName)
    {
        return new WorkspaceFileLogger(categoryName, _logFilePath);
    }

    public void Dispose()
    {
        GC.KeepAlive(_logFilePath);
    }
}

internal sealed class WorkspaceFileLogger : ILogger
{
    private static readonly object FileWriteLock = new();
    private readonly string _categoryName;
    private readonly string _logFilePath;

    public WorkspaceFileLogger(string categoryName, string logFilePath)
    {
        _categoryName = categoryName;
        _logFilePath = logFilePath;
    }

    public IDisposable BeginScope<TState>(TState state) where TState : notnull
    {
        GC.KeepAlive(state);
        GC.KeepAlive(_logFilePath);
        return new MemoryStream();
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        return logLevel != LogLevel.None;
    }

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
        {
            return;
        }

        ArgumentNullException.ThrowIfNull(formatter);

        string message;
        try
        {
            message = formatter(state, exception);
        }
        catch
        {
            message = state?.ToString() ?? string.Empty;
        }

        if (string.IsNullOrWhiteSpace(message) && exception is null)
        {
            return;
        }

        var timestamp = DateTimeOffset.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff zzz", CultureInfo.InvariantCulture);
        var builder = new StringBuilder();
        builder.Append(timestamp)
            .Append(' ')
            .Append('[')
            .Append(logLevel.ToString().ToUpperInvariant())
            .Append(']')
            .Append(' ')
            .Append(_categoryName);

        if (eventId.Id != 0 || !string.IsNullOrWhiteSpace(eventId.Name))
        {
            builder.Append(" (EventId: ").Append(eventId.Id);
            if (!string.IsNullOrWhiteSpace(eventId.Name))
            {
                builder.Append(':').Append(eventId.Name);
            }

            builder.Append(')');
        }

        builder.Append(" - ").Append(message);

        if (exception is not null)
        {
            builder.AppendLine();
            builder.Append(exception);
        }

        builder.AppendLine();

        lock (FileWriteLock)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_logFilePath) ?? AppContext.BaseDirectory);
            using var fileStream = new FileStream(_logFilePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
            using var writer = new StreamWriter(fileStream, Encoding.UTF8);
            writer.Write(builder.ToString());
            writer.Flush();
        }
    }
}
