using System;
using System.IO;
using System.Linq;

namespace WileyWidget.Services.Logging
{
    /// <summary>
    /// Resolves a writable logs directory for file-based application logs.
    /// The newer resolver prefers container-safe temp storage, while the
    /// legacy helper keeps the repository-root behavior used by existing tests.
    /// </summary>
    public static class LogPathResolver
    {
        public const string LogsDirectoryEnvironmentVariable = "WILEY_LOGS_DIRECTORY";

        /// <summary>
        /// Gets the writable log directory to use for runtime file logging.
        /// App Runner, ECS, Docker, and Lambda should use a temp-backed path.
        /// </summary>
        public static string GetLogDirectory()
        {
            if (IsContainerEnvironment())
            {
                return EnsureDirectory(Path.Combine(Path.GetTempPath(), "wiley-widget", "logs"));
            }

            var configuredLogsDir = Environment.GetEnvironmentVariable(LogsDirectoryEnvironmentVariable);

            if (!string.IsNullOrWhiteSpace(configuredLogsDir))
            {
                return EnsureDirectory(configuredLogsDir);
            }

            var localLogsDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
            return EnsureDirectory(localLogsDirectory);
        }

        /// <summary>
        /// Gets the full file path for a daily log file under the writable log directory.
        /// </summary>
        public static string GetLogFilePath(string loggerName)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(loggerName);
            return Path.Combine(GetLogDirectory(), $"{loggerName}-{DateTime.UtcNow:yyyy-MM-dd}.log");
        }

        public static string GetLogsDirectory()
        {
            var configuredLogsDir = Environment.GetEnvironmentVariable(LogsDirectoryEnvironmentVariable);
            return GetLogsDirectory(Directory.GetCurrentDirectory(), AppContext.BaseDirectory, configuredLogsDir);
        }

        public static string GetLogsDirectory(string currentDirectory, string baseDirectory, string? configuredLogsDir)
        {
            if (IsContainerEnvironment())
            {
                return EnsureDirectory(Path.Combine(Path.GetTempPath(), "wiley-widget", "logs"));
            }

            var repoRoot = TryFindRepoRoot(currentDirectory)
                ?? TryFindRepoRoot(baseDirectory);

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

        private static bool IsContainerEnvironment()
        {
            return !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("AWS_EXECUTION_ENV"))
                || !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("CONTAINER"))
                || File.Exists("/.dockerenv")
                || !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("AWS_LAMBDA_FUNCTION_NAME"));
        }

        private static string EnsureDirectory(string directoryPath)
        {
            var fullPath = Path.GetFullPath(directoryPath);
            Directory.CreateDirectory(fullPath);
            return fullPath;
        }

        private static DirectoryInfo? TryFindRepoRoot(string startDirectory)
        {
            try
            {
                return FindRepoRoot(new DirectoryInfo(startDirectory));
            }
            catch (UnauthorizedAccessException)
            {
                return null;
            }
            catch (IOException)
            {
                return null;
            }
        }

        private static DirectoryInfo? FindRepoRoot(DirectoryInfo? start)
        {
            while (start != null)
            {
                try
                {
                    if (start.EnumerateFiles("*.sln").Any() || start.EnumerateFiles("*.slnx").Any())
                    {
                        return start;
                    }
                }
                catch (UnauthorizedAccessException)
                {
                    return null;
                }
                catch (IOException)
                {
                    return null;
                }

                start = start.Parent;
            }

            return null;
        }
    }
}
