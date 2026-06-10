using Amazon.XRay.Recorder.Core;
using Microsoft.Extensions.Configuration;

namespace WileyCoWeb.Api.Configuration;

public static class TracingBootstrapper
{
    public static void InitializeTracing(WebApplicationBuilder builder)
    {
        // AWS X-Ray: distributed tracing for all incoming requests.
        // Credentials are resolved from the IAM execution role (Amplify / ECS task role) — no connection string needed.
        AWSXRayRecorder.InitializeInstance(builder.Configuration);

        // Only log the initialization message outside Development to reduce noise for local runs
        // that have no AWS credentials / X-Ray daemon.
        var env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
            ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
            ?? builder.Environment?.EnvironmentName;

        if (!string.Equals(env, "Development", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine("[API Startup] AWS X-Ray tracing initialized (service: WileyCoWeb.Api).");
        }
    }
}