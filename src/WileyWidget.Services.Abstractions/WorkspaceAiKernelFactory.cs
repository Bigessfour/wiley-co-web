using System;
using System.Globalization;
using System.Net.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace WileyWidget.Services.Abstractions;

public interface IWorkspaceAiKernelProvider
{
    WorkspaceAiServiceConfiguration GetConfiguration();

    WorkspaceAiKernelInitializationResult GetInitializationResult();
}

public sealed record WorkspaceAiApiKeyResolution(
    string? ApiKey,
    string ApiKeySource,
    bool ProviderPresent,
    bool ConfigNamedPresent,
    bool ConfigDirectPresent,
    bool EnvironmentPresent,
    string? SecretName);

public sealed record WorkspaceAiServiceConfiguration(
    WorkspaceAiApiKeyResolution ApiKeyResolution,
    bool Enabled,
    string? Model,
    bool StoreResponses,
    Uri ChatCompletionEndpoint,
    Uri LegacyResponsesEndpoint)
{
    public string ResolveModelOrDefault(string fallbackModel)
        => string.IsNullOrWhiteSpace(Model) ? fallbackModel : Model.Trim();
}

public sealed record WorkspaceAiKernelContext(Kernel Kernel, IChatCompletionService ChatService);

public sealed record WorkspaceAiKernelInitializationResult(
    WorkspaceAiKernelContext? Context,
    bool IsAvailable,
    bool IsApiKeyVisibleToProcess,
    string ApiKeySource,
    string StatusCode,
    string StatusMessage);

public static class WorkspaceAiKernelFactory
{
    public const string DefaultSemanticKernelModel = "grok-4-1-fast-reasoning";
    public const string HttpClientName = "WorkspaceAiTransport";

    private static readonly Uri DirectResponsesEndpoint = NormalizeResponsesEndpoint("https://api.x.ai/v1");
    private static readonly Uri DirectChatCompletionEndpoint = NormalizeChatCompletionEndpoint("https://api.x.ai/v1");

    public static WorkspaceAiServiceConfiguration ResolveConfiguration(IConfiguration configuration, IGrokApiKeyProvider? apiKeyProvider = null)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        return new WorkspaceAiServiceConfiguration(
            ResolveApiKeyResolution(configuration, apiKeyProvider),
            GetConfiguredBoolean(configuration, true, "EnableAI", "XAI:Enabled"),
            GetConfiguredString(configuration, "XaiModel", "XAI:Model", "Grok:Model"),
            GetConfiguredBoolean(configuration, false, "XAI:StoreResponses"),
            ResolveSemanticKernelChatEndpoint(configuration),
            ResolveLegacyXaiEndpoint(configuration));
    }

    public static WorkspaceAiApiKeyResolution ResolveApiKeyResolution(IConfiguration configuration, IGrokApiKeyProvider? apiKeyProvider = null)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var environmentApiKey = Environment.GetEnvironmentVariable("XAI_API_KEY");
        var providerApiKey = apiKeyProvider?.ApiKey;
        var directConfigApiKey = configuration["XaiApiKey"] ?? configuration["XAI_API_KEY"];
        var namedConfigApiKey = configuration["XAI:ApiKey"] ?? configuration["xAI:ApiKey"];
        var secretName = configuration["XAI:SecretName"] ?? configuration["XAI_SECRET_NAME"];

        if (!string.IsNullOrWhiteSpace(environmentApiKey))
        {
            return new WorkspaceAiApiKeyResolution(environmentApiKey, "env:XAI_API_KEY", !string.IsNullOrWhiteSpace(providerApiKey), !string.IsNullOrWhiteSpace(namedConfigApiKey), !string.IsNullOrWhiteSpace(directConfigApiKey), true, secretName);
        }

        if (!string.IsNullOrWhiteSpace(providerApiKey))
        {
            return new WorkspaceAiApiKeyResolution(providerApiKey, apiKeyProvider?.GetConfigurationSource() ?? "provider", true, !string.IsNullOrWhiteSpace(namedConfigApiKey), !string.IsNullOrWhiteSpace(directConfigApiKey), false, secretName);
        }

        if (!string.IsNullOrWhiteSpace(directConfigApiKey))
        {
            return new WorkspaceAiApiKeyResolution(directConfigApiKey, "config:XAI_API_KEY", false, !string.IsNullOrWhiteSpace(namedConfigApiKey), true, false, secretName);
        }

        if (!string.IsNullOrWhiteSpace(namedConfigApiKey))
        {
            return new WorkspaceAiApiKeyResolution(namedConfigApiKey, "config:XAI:ApiKey", false, true, false, false, secretName);
        }

        return new WorkspaceAiApiKeyResolution(null, "not-found", !string.IsNullOrWhiteSpace(providerApiKey), !string.IsNullOrWhiteSpace(namedConfigApiKey), !string.IsNullOrWhiteSpace(directConfigApiKey), !string.IsNullOrWhiteSpace(environmentApiKey), secretName);
    }

    public static Uri ResolveSemanticKernelChatEndpoint(IConfiguration configuration)
        => ResolvePreferredXaiEndpoint(
            configuration,
            primaryKey: "XAI:ChatEndpoint",
            secondaryKey: "XAI:Endpoint",
            normalizeEndpoint: NormalizeChatCompletionEndpoint,
            directEndpoint: DirectChatCompletionEndpoint);

    public static Uri ResolveLegacyXaiEndpoint(IConfiguration configuration)
        => ResolvePreferredXaiEndpoint(
            configuration,
            primaryKey: "XAI:Endpoint",
            secondaryKey: null,
            normalizeEndpoint: NormalizeResponsesEndpoint,
            directEndpoint: DirectResponsesEndpoint);

    public static WorkspaceAiKernelContext CreateKernelContext(
        WorkspaceAiServiceConfiguration configuration,
        string fallbackModel,
        IHttpClientFactory? httpClientFactory = null,
        Action<IKernelBuilder>? configureBuilder = null)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        if (string.IsNullOrWhiteSpace(configuration.ApiKeyResolution.ApiKey))
        {
            throw new InvalidOperationException("No usable XAI_API_KEY value was visible to the process.");
        }

        var builder = Kernel.CreateBuilder();
        var httpClient = httpClientFactory?.CreateClient(HttpClientName);

        builder.AddOpenAIChatCompletion(
            modelId: configuration.ResolveModelOrDefault(fallbackModel),
            apiKey: configuration.ApiKeyResolution.ApiKey,
            endpoint: configuration.ChatCompletionEndpoint,
            httpClient: httpClient);

        configureBuilder?.Invoke(builder);

        var kernel = builder.Build();
        var chatService = kernel.GetRequiredService<IChatCompletionService>();
        return new WorkspaceAiKernelContext(kernel, chatService);
    }

    public static Uri NormalizeResponsesEndpoint(string? endpoint)
    {
        var candidate = (endpoint ?? "https://api.x.ai/v1").Trim().TrimEnd('/');
        if (candidate.EndsWith("/responses", StringComparison.OrdinalIgnoreCase))
        {
            return new Uri(candidate, UriKind.Absolute);
        }

        if (candidate.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase))
        {
            candidate = candidate[..^"/chat/completions".Length];
        }

        return new Uri($"{candidate}/responses", UriKind.Absolute);
    }

    public static Uri NormalizeChatCompletionEndpoint(string? endpoint)
    {
        var candidate = (endpoint ?? "https://api.x.ai/v1").Trim().TrimEnd('/');

        if (candidate.EndsWith("/responses", StringComparison.OrdinalIgnoreCase))
        {
            candidate = candidate[..^"/responses".Length];
        }

        if (candidate.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase))
        {
            candidate = candidate[..^"/chat/completions".Length];
        }

        return new Uri(candidate, UriKind.Absolute);
    }

    private static Uri ResolvePreferredXaiEndpoint(
        IConfiguration configuration,
        string primaryKey,
        string? secondaryKey,
        Func<string?, Uri> normalizeEndpoint,
        Uri directEndpoint)
    {
        var primaryValue = configuration[primaryKey];
        if (!string.IsNullOrWhiteSpace(primaryValue))
        {
            var primaryEndpoint = normalizeEndpoint(primaryValue);
            if (!string.Equals(primaryEndpoint.Host, directEndpoint.Host, StringComparison.OrdinalIgnoreCase)
                || (string.IsNullOrWhiteSpace(configuration["XaiApiEndpoint"]) && string.IsNullOrWhiteSpace(configuration["XaiBaseUrl"])))
            {
                return primaryEndpoint;
            }
        }

        if (!string.IsNullOrWhiteSpace(secondaryKey))
        {
            var secondaryValue = configuration[secondaryKey];
            if (!string.IsNullOrWhiteSpace(secondaryValue))
            {
                return normalizeEndpoint(secondaryValue);
            }
        }

        var aliasValue = GetConfiguredString(configuration, "XaiApiEndpoint", "XaiBaseUrl");
        if (!string.IsNullOrWhiteSpace(aliasValue))
        {
            return normalizeEndpoint(aliasValue);
        }

        return directEndpoint;
    }

    private static string? GetConfiguredString(IConfiguration configuration, params string[] keys)
    {
        foreach (var key in keys)
        {
            var value = configuration[key];
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }

    private static bool GetConfiguredBoolean(IConfiguration configuration, bool fallback, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (bool.TryParse(configuration[key], out var parsed))
            {
                return parsed;
            }
        }

        return fallback;
    }

}