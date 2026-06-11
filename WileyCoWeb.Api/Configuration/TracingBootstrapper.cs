namespace WileyCoWeb.Api.Configuration;

/// <summary>
/// No-op (AWS X-Ray and cloud tracing fully removed for lean local-only stack).
/// </summary>
public static class TracingBootstrapper
{
    public static void InitializeTracing(WebApplicationBuilder builder)
    {
        // No-op. Local logging is handled elsewhere.
    }
}
