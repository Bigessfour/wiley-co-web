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
        Console.WriteLine("[API Startup] AWS X-Ray tracing initialized (service: WileyCoWeb.Api).");
    }
}