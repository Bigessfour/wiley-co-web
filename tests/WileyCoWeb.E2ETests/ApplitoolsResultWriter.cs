using System.Collections;
using System.Globalization;
using System.Text.Json;

namespace WileyCoWeb.E2ETests;

internal static class ApplitoolsResultWriter
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    internal static ApplitoolsSuiteSummary? WriteSummary(object? summary, string suiteName)
    {
        if (summary is not IEnumerable containers)
        {
            return null;
        }

        var sessions = new List<ApplitoolsSessionSummary>();
        foreach (var container in containers)
        {
            var session = CreateSessionSummary(container);
            if (session is not null)
            {
                sessions.Add(session);
            }
        }

        var payload = new ApplitoolsSuiteSummary(
            SuiteName: suiteName,
            GeneratedAtUtc: DateTimeOffset.UtcNow,
            TotalSessions: sessions.Count,
            PassedSessions: sessions.Count(session => session.IsPassed),
            UnresolvedSessions: sessions.Count(session => string.Equals(session.Outcome, "Unresolved", StringComparison.OrdinalIgnoreCase)),
            AbortedSessions: sessions.Count(session => string.Equals(session.Outcome, "Aborted", StringComparison.OrdinalIgnoreCase)),
            Sessions: sessions);

        var outputDirectory = GetOutputDirectory();
        Directory.CreateDirectory(outputDirectory);

        var safeSuiteName = string.Concat(suiteName.Select(character => char.IsLetterOrDigit(character) ? character : '-')).Trim('-');
        if (string.IsNullOrWhiteSpace(safeSuiteName))
        {
            safeSuiteName = "ApplitoolsResults";
        }

        var fileName = $"{DateTime.UtcNow:yyyyMMdd-HHmmss}-{safeSuiteName}.json";
        var outputPath = Path.Combine(outputDirectory, fileName);
        File.WriteAllText(outputPath, JsonSerializer.Serialize(payload, SerializerOptions));
        return payload;
    }

    internal static void ReactToResults(ApplitoolsSuiteSummary? summary)
    {
        if (summary is null || !ShouldFailOnUnresolved())
        {
            return;
        }

        var problematicSessions = summary.Sessions
            .Where(session => !string.Equals(session.Outcome, "Passed", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (problematicSessions.Count == 0)
        {
            return;
        }

        var message = string.Join(
            Environment.NewLine,
            problematicSessions.Select(session =>
                $"- {session.TestName ?? "<unknown>"}: {session.Outcome} | Status={session.StatusCode ?? "<none>"} | Url={session.SessionUrl ?? "<none>"}"));

        throw new Xunit.Sdk.XunitException(
            $"Applitools Eyes reported non-passing sessions for {summary.SuiteName}.{Environment.NewLine}{message}");
    }

    private static string GetOutputDirectory()
    {
        var overridePath = Environment.GetEnvironmentVariable("WILEYCO_APPLITOOLS_RESULTS_DIR")
            ?? Environment.GetEnvironmentVariable("APPLITOOLS_RESULTS_DIR");

        if (!string.IsNullOrWhiteSpace(overridePath))
        {
            return Path.GetFullPath(overridePath);
        }

        return Path.Combine(AppContext.BaseDirectory, "TestResults", "Applitools");
    }

    private static bool ShouldFailOnUnresolved()
    {
        var value = Environment.GetEnvironmentVariable("APPLITOOLS_FAIL_ON_UNRESOLVED")
            ?? Environment.GetEnvironmentVariable("WILEYCO_APPLITOOLS_FAIL_ON_UNRESOLVED");

        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        return bool.TryParse(value, out var parsed) ? parsed : true;
    }

    private static ApplitoolsSessionSummary? CreateSessionSummary(object? container)
    {
        var testResults = GetPropertyValue(container, "TestResults");
        if (testResults is null)
        {
            return null;
        }

        var appUrls = GetPropertyValue(testResults, "AppUrls");
        var apiUrls = GetPropertyValue(testResults, "ApiUrls");
        var hostDisplaySize = GetPropertyValue(testResults, "HostDisplaySize");
        var exception = GetPropertyValue(container, "Exception");

        var isPassed = GetBool(testResults, "IsPassed");
        var isDifferent = GetBool(testResults, "IsDifferent");
        var isNew = GetBool(testResults, "IsNew");
        var isAborted = GetBool(testResults, "IsAborted");
        var exceptionMessage = GetString(exception, "Message");

        return new ApplitoolsSessionSummary(
            AppName: GetString(testResults, "AppName"),
            TestName: GetString(testResults, "Name"),
            BatchName: GetString(testResults, "BatchName"),
            BranchName: GetString(testResults, "BranchName"),
            Outcome: GetOutcome(isPassed, isDifferent, isNew, isAborted, exceptionMessage),
            StatusCode: GetStringValue(GetPropertyValue(testResults, "Status")),
            IsPassed: isPassed,
            IsDifferent: isDifferent,
            IsNew: isNew,
            IsAborted: isAborted,
            SessionUrl: GetString(testResults, "Url") ?? GetString(appUrls, "Session"),
            BatchUrl: GetString(appUrls, "Batch"),
            ApiSessionUrl: GetString(apiUrls, "Session"),
            ApiBatchUrl: GetString(apiUrls, "Batch"),
            Steps: GetInt(testResults, "Steps"),
            Matches: GetInt(testResults, "Matches"),
            Mismatches: GetInt(testResults, "Mismatches"),
            Missing: GetInt(testResults, "Missing"),
            HostApp: GetString(testResults, "HostApp"),
            HostOs: GetString(testResults, "HostOS"),
            StartedAt: GetDateTimeOffset(testResults, "StartedAt"),
            DurationSeconds: GetInt(testResults, "Duration"),
            ViewportWidth: GetInt(hostDisplaySize, "Width"),
            ViewportHeight: GetInt(hostDisplaySize, "Height"),
            ExceptionMessage: exceptionMessage);
    }

    private static string GetOutcome(bool isPassed, bool isDifferent, bool isNew, bool isAborted, string? exceptionMessage)
    {
        if (isAborted)
        {
            return "Aborted";
        }

        if (isPassed)
        {
            return "Passed";
        }

        if (isDifferent || isNew || !string.IsNullOrWhiteSpace(exceptionMessage))
        {
            return "Unresolved";
        }

        return "Failed";
    }

    private static object? GetPropertyValue(object? source, string propertyName)
    {
        return source?.GetType().GetProperty(propertyName)?.GetValue(source);
    }

    private static string? GetString(object? source, string propertyName)
    {
        return GetStringValue(GetPropertyValue(source, propertyName));
    }

    private static string? GetStringValue(object? value)
    {
        return value switch
        {
            null => null,
            string stringValue => stringValue,
            _ => Convert.ToString(value, CultureInfo.InvariantCulture)
        };
    }

    private static bool GetBool(object? source, string propertyName)
    {
        var value = GetPropertyValue(source, propertyName);
        return value switch
        {
            bool boolValue => boolValue,
            string stringValue when bool.TryParse(stringValue, out var parsed) => parsed,
            _ => false
        };
    }

    private static int? GetInt(object? source, string propertyName)
    {
        var value = GetPropertyValue(source, propertyName);
        return value switch
        {
            null => null,
            int intValue => intValue,
            long longValue => checked((int)longValue),
            short shortValue => shortValue,
            string stringValue when int.TryParse(stringValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) => parsed,
            _ => null
        };
    }

    private static DateTimeOffset? GetDateTimeOffset(object? source, string propertyName)
    {
        var value = GetPropertyValue(source, propertyName);
        return value switch
        {
            DateTimeOffset dto => dto,
            DateTime dt => new DateTimeOffset(dt),
            string stringValue when DateTimeOffset.TryParse(stringValue, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed) => parsed,
            _ => null
        };
    }

    internal sealed record ApplitoolsSuiteSummary(
        string SuiteName,
        DateTimeOffset GeneratedAtUtc,
        int TotalSessions,
        int PassedSessions,
        int UnresolvedSessions,
        int AbortedSessions,
        IReadOnlyList<ApplitoolsSessionSummary> Sessions);

    internal sealed record ApplitoolsSessionSummary(
        string? AppName,
        string? TestName,
        string? BatchName,
        string? BranchName,
        string Outcome,
        string? StatusCode,
        bool IsPassed,
        bool IsDifferent,
        bool IsNew,
        bool IsAborted,
        string? SessionUrl,
        string? BatchUrl,
        string? ApiSessionUrl,
        string? ApiBatchUrl,
        int? Steps,
        int? Matches,
        int? Mismatches,
        int? Missing,
        string? HostApp,
        string? HostOs,
        DateTimeOffset? StartedAt,
        int? DurationSeconds,
        int? ViewportWidth,
        int? ViewportHeight,
        string? ExceptionMessage);
}