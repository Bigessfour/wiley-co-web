using System;
using System.IO;
using System.Linq;

namespace WileyWidget.Services.Logging
{
    /// <summary>
    /// Resolves a consistent logs directory for local and deployed runs.
    /// Prefers an explicit override, then the repository root when available,
    /// and finally falls back to a writable temp location.
    /// </summary>
    public static class LogPathResolver
    {
        public const string LogsDirectoryEnvironmentVariable = "WILEY_LOGS_DIRECTORY";

        public static string GetLogsDirectory()
        {
            var configuredLogsDir = Environment.GetEnvironmentVariable(LogsDirectoryEnvironmentVariable);
            return GetLogsDirectory(Directory.GetCurrentDirectory(), AppContext.BaseDirectory, configuredLogsDir);
        }

        public static string GetLogsDirectory(string currentDirectory, string baseDirectory, string? configuredLogsDir)
        {
            var repoRoot = FindRepoRoot(new DirectoryInfo(currentDirectory))
                ?? FindRepoRoot(new DirectoryInfo(baseDirectory));

            var candidateDirectories = new[]
            {
                configuredLogsDir,
                repoRoot is null ? null : Path.Combine(repoRoot.FullName, "logs"),
                Path.Combine(currentDirectory, "logs"),
                Path.Combine(baseDirectory, "logs"),
                Path.Combine(Path.GetTempPath(), "wiley-widget", "logs")
            };

            IOException? lastException = null;
            foreach (var candidateDirectory in candidateDirectories
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Select(path => Path.GetFullPath(path!))
                .Distinct(StringComparer.OrdinalIgnoreCase))
            {
                try
                {
                    Directory.CreateDirectory(candidateDirectory);
                    return candidateDirectory;
                }
                catch (IOException exception)
                {
                    lastException = exception;
                }
                catch (UnauthorizedAccessException exception)
                {
                    lastException = new IOException($"Unable to create logs directory '{candidateDirectory}'.", exception);
                }
            }

            throw new IOException("Unable to create any logs directory candidate.", lastException);
        }

        private static DirectoryInfo? FindRepoRoot(DirectoryInfo? start)
        {
            while (start != null)
            {
                if (start.EnumerateFiles("*.sln").Any() || start.EnumerateFiles("*.slnx").Any())
                {
                    return start;
                }

                start = start.Parent;
            }

            return null;
        }
    }
}
