using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace WileyWidget.Services.Abstractions;

public interface IWorkspaceKnowledgeService
{
    Task<WorkspaceKnowledgeResult> BuildAsync(string enterpriseName, int fiscalYear, CancellationToken cancellationToken = default);

    Task<WorkspaceKnowledgeResult> BuildAsync(WorkspaceKnowledgeInput input, CancellationToken cancellationToken = default);
}

public sealed record WorkspaceKnowledgeInput(
    string SelectedEnterprise,
    int SelectedFiscalYear,
    decimal CurrentRate,
    decimal TotalCosts,
    decimal ProjectedVolume,
    decimal ScenarioCostTotal = 0m,
    int TopVarianceCount = 5,
    int ForecastYears = 5,
    // Global or per-enterprise overhead % (Town + mgmt + labor). From AppSettings or override.
    // Applied as: NetContribution = Revenue - DirectCosts - (OverheadPercent * DirectCosts / 100)
    decimal OverheadPercent = 0m);

public sealed record WorkspaceKnowledgeInsight(
    string Label,
    string Value,
    string Description);

public sealed record WorkspaceKnowledgeAction(
    string Title,
    string Description,
    string Priority);

public sealed record WorkspaceKnowledgeVariance(
    string AccountName,
    decimal BudgetedAmount,
    decimal ActualAmount,
    decimal VarianceAmount,
    decimal VariancePercentage);

public sealed record WorkspaceKnowledgeResult(
    string SelectedEnterprise,
    int SelectedFiscalYear,
    string OperationalStatus,
    string ExecutiveSummary,
    string RateRationale,
    decimal CurrentRate,
    decimal TotalCosts,
    decimal ProjectedVolume,
    decimal ScenarioCostTotal,
    decimal BreakEvenRate,
    decimal AdjustedBreakEvenRate,
    decimal RateGap,
    decimal AdjustedRateGap,
    decimal MonthlyRevenue,
    decimal NetPosition,
    decimal CoverageRatio,
    decimal CurrentReserveBalance,
    decimal RecommendedReserveLevel,
    string ReserveRiskAssessment,
    DateTime GeneratedAtUtc,
    IReadOnlyList<WorkspaceKnowledgeInsight> Insights,
    IReadOnlyList<WorkspaceKnowledgeAction> RecommendedActions,
    IReadOnlyList<WorkspaceKnowledgeVariance> TopVariances,
    // Overhead-adjusted (post-categorization, using global % from AppSettings or per override in input).
    // DirectCosts = TotalCosts (from categorized ledger). Enterprise Share base = DirectCosts.
    // NetContribution = Revenue - DirectCosts - (OverheadPercent * DirectCosts / 100)
    decimal DirectCosts,
    decimal OverheadBurden,
    decimal NetContribution,
    bool HoldsItsOwn,
    decimal VampireImpact);

public sealed class WorkspaceKnowledgeNotFoundException : InvalidOperationException
{
    public WorkspaceKnowledgeNotFoundException(string message)
        : base(message)
    {
    }

    public WorkspaceKnowledgeNotFoundException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

public sealed class WorkspaceKnowledgeUnavailableException : InvalidOperationException
{
    public WorkspaceKnowledgeUnavailableException(string message)
        : base(message)
    {
    }

    public WorkspaceKnowledgeUnavailableException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}