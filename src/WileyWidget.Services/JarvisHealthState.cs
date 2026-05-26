using WileyCoWeb.Contracts;

namespace WileyWidget.Services;

public interface IJarvisHealthState
{
    void RecordTurn(string? answerSource, bool usedFallback, string? failureCode);

    void SetSemanticKernelAvailability(bool isAvailable);

    JarvisHealthResponse GetSnapshot();
}

public sealed class JarvisHealthState : IJarvisHealthState
{
    private readonly object sync = new();
    private bool semanticKernelAvailable;
    private string? latestAnswerSource;
    private bool latestUsedFallback;
    private string? latestFailureCode;
    private DateTimeOffset? lastTurnAtUtc;

    public void SetSemanticKernelAvailability(bool isAvailable)
    {
        lock (sync)
        {
            semanticKernelAvailable = isAvailable;
        }
    }

    public void RecordTurn(string? answerSource, bool usedFallback, string? failureCode)
    {
        lock (sync)
        {
            latestAnswerSource = answerSource;
            latestUsedFallback = usedFallback;
            latestFailureCode = failureCode;
            lastTurnAtUtc = DateTimeOffset.UtcNow;
        }
    }

    public JarvisHealthResponse GetSnapshot()
    {
        lock (sync)
        {
            var status = ResolveStatus();
            return new JarvisHealthResponse(
                Status: status,
                SemanticKernelAvailable: semanticKernelAvailable,
                LatestAnswerSource: latestAnswerSource,
                LatestUsedFallback: latestUsedFallback,
                LatestFailureCode: latestFailureCode,
                LastTurnAtUtc: lastTurnAtUtc?.ToString("O"));
        }
    }

    private string ResolveStatus()
    {
        if (!semanticKernelAvailable && latestUsedFallback && !string.IsNullOrWhiteSpace(latestFailureCode))
        {
            return "unavailable";
        }

        if (latestUsedFallback || !semanticKernelAvailable)
        {
            return "degraded";
        }

        return "healthy";
    }
}
