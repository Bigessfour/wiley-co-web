namespace WileyCoWeb.Contracts;

public sealed record WorkspaceKnowledgeRequest(
    WorkspaceBootstrapData Snapshot,
    int TopVarianceCount = 5,
    int ForecastYears = 5);

public sealed record WorkspaceKnowledgeInsightResponse(
    string Label,
    string Value,
    string Description);

public sealed record WorkspaceKnowledgeActionResponse(
    string Title,
    string Description,
    string Priority);

public sealed record WorkspaceKnowledgeVarianceResponse(
    string AccountName,
    decimal BudgetedAmount,
    decimal ActualAmount,
    decimal VarianceAmount,
    decimal VariancePercentage);

public sealed record WorkspaceKnowledgeResponse(
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
    string GeneratedAtUtc,
    IReadOnlyList<WorkspaceKnowledgeInsightResponse> Insights,
    IReadOnlyList<WorkspaceKnowledgeActionResponse> RecommendedActions,
    IReadOnlyList<WorkspaceKnowledgeVarianceResponse> TopVariances);