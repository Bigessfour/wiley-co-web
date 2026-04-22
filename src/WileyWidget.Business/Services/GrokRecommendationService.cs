using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using WileyWidget.Business.Interfaces;
using WileyWidget.Services.Abstractions;

namespace WileyWidget.Business.Services;

/// <summary>
/// Lightweight Grok recommendation service built on Semantic Kernel 1.74.0.
/// Uses a direct chat-completion flow and falls back to deterministic rules when the model is unavailable.
/// </summary>
public sealed class GrokRecommendationService : IGrokRecommendationService, IHealthCheck
{
    private static readonly HashSet<string> KnownDepartments = new(StringComparer.OrdinalIgnoreCase)
    {
        "Water",
        "Sewer",
        "Trash",
        "Apartments",
        "Electric",
        "Gas"
    };

    private readonly ILogger<GrokRecommendationService> _logger;
    private readonly IConfiguration _configuration;
    private readonly string _model;
    private readonly bool _enabled;
    private readonly string? _apiKey;
    private readonly Uri _endpoint;
    private readonly IChatCompletionService? _chatService;

    public GrokRecommendationService(
        IGrokApiKeyProvider? apiKeyProvider,
        ILogger<GrokRecommendationService> logger,
        IConfiguration configuration)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));

        _apiKey = apiKeyProvider?.ApiKey
            ?? GetConfiguredString("XaiApiKey", "XAI:ApiKey", "xAI:ApiKey", "XAI_API_KEY");

        _enabled = GetConfiguredBoolean(true, "EnableAI", "XAI:Enabled");
        _model = GetConfiguredString("XaiModel", "XAI:Model", "Grok:Model") ?? "grok-4.1";
        _endpoint = NormalizeChatCompletionsEndpoint(GetConfiguredString("XAI:ChatEndpoint", "XAI:Endpoint", "XaiApiEndpoint", "XaiBaseUrl"));

        if (_enabled && !string.IsNullOrWhiteSpace(_apiKey))
        {
            var kernelBuilder = Kernel.CreateBuilder();
            kernelBuilder.AddOpenAIChatCompletion(
                modelId: _model,
                apiKey: _apiKey,
                endpoint: _endpoint);

            var kernel = kernelBuilder.Build();
            _chatService = kernel.GetRequiredService<IChatCompletionService>();

            _logger.LogInformation(
                "Grok recommendation service initialized with Semantic Kernel (model: {Model}, apiKeySource: {Source})",
                _model,
                apiKeyProvider?.GetConfigurationSource() ?? "configuration");
        }
        else
        {
            _logger.LogWarning("Grok recommendation service is using rule-based fallback (enabled: {Enabled}, apiKeyPresent: {HasKey})", _enabled, !string.IsNullOrWhiteSpace(_apiKey));
        }
    }

    public async Task<RecommendationResult> GetRecommendedAdjustmentFactorsAsync(
        Dictionary<string, decimal> departmentExpenses,
        decimal targetProfitMargin = 15.0m,
        CancellationToken cancellationToken = default)
    {
        ValidateInput(departmentExpenses, targetProfitMargin);

        if (_chatService == null)
        {
            return CreateRuleBasedResult(departmentExpenses, targetProfitMargin);
        }

        try
        {
            var chatHistory = new ChatHistory();
            chatHistory.AddSystemMessage(BuildSystemPrompt());
            chatHistory.AddUserMessage(BuildRecommendationPrompt(departmentExpenses, targetProfitMargin));

            var response = await _chatService.GetChatMessageContentAsync(chatHistory, cancellationToken: cancellationToken).ConfigureAwait(false);
            var content = response?.Content?.Trim();

            if (string.IsNullOrWhiteSpace(content))
            {
                _logger.LogWarning("Grok returned an empty recommendation response; using fallback.");
                return CreateRuleBasedResult(departmentExpenses, targetProfitMargin);
            }

            if (TryParseAiResponse(content, departmentExpenses.Keys, out var parsedResult, out var parseWarnings))
            {
                return parsedResult with
                {
                    FromGrokApi = true,
                    ApiModelUsed = _model,
                    Warnings = parseWarnings
                };
            }

            _logger.LogWarning("Grok recommendation response could not be parsed; using fallback.");
            return CreateRuleBasedResult(departmentExpenses, targetProfitMargin);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Grok recommendation request failed; using fallback.");
            return CreateRuleBasedResult(departmentExpenses, targetProfitMargin, ex.Message);
        }
    }

    public async Task<string> GetRecommendationExplanationAsync(
        Dictionary<string, decimal> departmentExpenses,
        decimal targetProfitMargin = 15.0m,
        CancellationToken cancellationToken = default)
    {
        var recommendation = await GetRecommendedAdjustmentFactorsAsync(departmentExpenses, targetProfitMargin, cancellationToken).ConfigureAwait(false);
        return recommendation.Explanation;
    }

    public void ClearCache()
    {
        _logger.LogDebug("Grok recommendation service cache is disabled in the lightweight implementation.");
    }

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        if (_chatService != null)
        {
            return Task.FromResult(HealthCheckResult.Healthy($"Semantic Kernel ready for {_model}"));
        }

        if (!_enabled)
        {
            return Task.FromResult(HealthCheckResult.Healthy("Grok recommendation service disabled; using rule-based fallback."));
        }

        return Task.FromResult(HealthCheckResult.Degraded("Grok recommendation service is running without an API key; using rule-based fallback."));
    }

    private RecommendationResult CreateRuleBasedResult(
        Dictionary<string, decimal> departmentExpenses,
        decimal targetProfitMargin,
        params string[] warnings)
    {
        var factors = CalculateRuleBasedRecommendations(departmentExpenses, targetProfitMargin);

        return new RecommendationResult(
            AdjustmentFactors: factors,
            Explanation: GenerateRuleBasedExplanation(departmentExpenses, targetProfitMargin),
            FromGrokApi: false,
            ApiModelUsed: "rule-based",
            Warnings: warnings.Where(w => !string.IsNullOrWhiteSpace(w)).ToArray());
    }

    private static Dictionary<string, decimal> CalculateRuleBasedRecommendations(
        Dictionary<string, decimal> departmentExpenses,
        decimal targetProfitMargin)
    {
        var baseFactor = 1.0m + (targetProfitMargin / 100m);
        var result = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);

        foreach (var department in departmentExpenses.Keys.OrderBy(key => key, StringComparer.OrdinalIgnoreCase))
        {
            var adjustment = department switch
            {
                "Water" => 0.00m,
                "Sewer" => 0.02m,
                "Trash" => -0.05m,
                "Apartments" => 0.03m,
                "Electric" => 0.04m,
                "Gas" => 0.01m,
                _ => 0.00m
            };

            result[department] = Math.Round(baseFactor + adjustment, 4);
        }

        return result;
    }

    private static string GenerateRuleBasedExplanation(Dictionary<string, decimal> departmentExpenses, decimal targetProfitMargin)
    {
        var totalExpenses = departmentExpenses.Values.Sum();
        var departmentCount = departmentExpenses.Count;

        return $"Based on monthly expenses totaling ${totalExpenses:N2} across {departmentCount} departments and a target profit margin of {targetProfitMargin}%, the recommended adjustments support full cost recovery and reserve stability. Water, Sewer, Trash, Apartments, Electric, and Gas are weighted with small operating differences to reflect typical municipal cost structures. This fallback result is deterministic and safe to use when Grok is unavailable.";
    }

    private string? GetConfiguredString(params string[] keys)
    {
        foreach (var key in keys)
        {
            var value = _configuration[key];
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }

    private bool GetConfiguredBoolean(bool fallback, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (bool.TryParse(_configuration[key], out var parsed))
            {
                return parsed;
            }
        }

        return fallback;
    }

    private static string BuildSystemPrompt()
    {
        return "You are JARVIS for municipal finance. Return only valid JSON with this shape: {\"adjustmentFactors\": {\"Water\": 1.15}, \"explanation\": \"...\"}. Do not include markdown, code fences, or commentary. Use concise professional language.";
    }

    private static string BuildRecommendationPrompt(Dictionary<string, decimal> expenses, decimal margin)
    {
        var expenseList = string.Join(", ", expenses.OrderBy(e => e.Key, StringComparer.OrdinalIgnoreCase)
            .Select(e => $"{e.Key}: ${e.Value:N2}"));

        return $"Monthly departmental expenses: {expenseList}. Target profit margin: {margin}%. Return JSON only.";
    }

    private static bool TryParseAiResponse(
        string content,
        IEnumerable<string> expectedDepartments,
        out RecommendationResult result,
        out string[] warnings)
    {
        warnings = Array.Empty<string>();

        try
        {
            var json = ExtractJsonObject(content);
            if (json == null)
            {
                result = default!;
                return false;
            }

            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            var factors = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
            if (root.TryGetProperty("adjustmentFactors", out var factorsElement) || root.TryGetProperty("factors", out factorsElement))
            {
                foreach (var property in factorsElement.EnumerateObject())
                {
                    if (property.Value.ValueKind == JsonValueKind.Number && property.Value.TryGetDecimal(out var value))
                    {
                        factors[property.Name] = Math.Round(value, 4);
                    }
                    else if (decimal.TryParse(property.Value.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsedValue))
                    {
                        factors[property.Name] = Math.Round(parsedValue, 4);
                    }
                }
            }

            var explanation = root.TryGetProperty("explanation", out var explanationElement)
                ? explanationElement.GetString()?.Trim() ?? string.Empty
                : string.Empty;

            if (factors.Count == 0 || string.IsNullOrWhiteSpace(explanation))
            {
                result = default!;
                return false;
            }

            var missingDepartments = expectedDepartments
                .Where(department => !factors.ContainsKey(department))
                .ToArray();

            warnings = missingDepartments.Length == 0
                ? Array.Empty<string>()
                : new[] { $"Missing recommendations for: {string.Join(", ", missingDepartments)}" };

            result = new RecommendationResult(
                AdjustmentFactors: factors,
                Explanation: explanation,
                FromGrokApi: true,
                ApiModelUsed: "",
                Warnings: warnings);
            return true;
        }
        catch
        {
            result = default!;
            warnings = Array.Empty<string>();
            return false;
        }
    }

    private static string? ExtractJsonObject(string content)
    {
        var trimmed = content.Trim();

        if (trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            var firstBrace = trimmed.IndexOf('{');
            var lastBrace = trimmed.LastIndexOf('}');
            if (firstBrace >= 0 && lastBrace > firstBrace)
            {
                return trimmed.Substring(firstBrace, lastBrace - firstBrace + 1);
            }
        }

        var start = trimmed.IndexOf('{');
        var end = trimmed.LastIndexOf('}');
        if (start >= 0 && end > start)
        {
            return trimmed.Substring(start, end - start + 1);
        }

        return null;
    }

    private void ValidateInput(Dictionary<string, decimal> expenses, decimal margin)
    {
        if (expenses == null || expenses.Count == 0)
        {
            throw new ArgumentException("Department expenses cannot be null or empty.", nameof(expenses));
        }

        if (margin < 0 || margin > 50)
        {
            throw new ArgumentOutOfRangeException(nameof(margin), "Profit margin must be between 0% and 50%.");
        }

        foreach (var expense in expenses)
        {
            if (string.IsNullOrWhiteSpace(expense.Key))
            {
                throw new ArgumentException("Department names cannot be empty.", nameof(expenses));
            }

            if (expense.Value <= 0)
            {
                throw new ArgumentException($"Expense for {expense.Key} must be greater than zero.", nameof(expenses));
            }

            if (!KnownDepartments.Contains(expense.Key))
            {
                throw new ArgumentException($"Unknown department '{expense.Key}'. Known departments are: {string.Join(", ", KnownDepartments.OrderBy(item => item))}", nameof(expenses));
            }
        }
    }

    private static Uri NormalizeChatCompletionsEndpoint(string? endpoint)
    {
        var candidate = (endpoint ?? "https://api.x.ai/v1").Trim().TrimEnd('/');
        if (candidate.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase))
        {
            return new Uri(candidate, UriKind.Absolute);
        }

        if (candidate.EndsWith("/responses", StringComparison.OrdinalIgnoreCase))
        {
            candidate = candidate[..^"/responses".Length];
        }

        return new Uri($"{candidate}/chat/completions", UriKind.Absolute);
    }
}
