namespace WileyCoWeb.Contracts;

public sealed record WorkspaceScenarioItemData(Guid Id, string Name, decimal Cost);

public sealed record ApartmentUnitTypeData(Guid Id, string Name, int BedroomCount, int UnitCount, decimal MonthlyRent);

public sealed record BreakEvenSeriesPoint(string PeriodLabel, decimal RevenuePerCustomer, decimal ExpensesPerCustomer, decimal BreakEvenPerCustomer);

public sealed record BreakEvenQuadrantData(
    string EnterpriseName,
    string EnterpriseType,
    decimal CurrentRate,
    decimal MonthlyExpenses,
    decimal MonthlyRevenue,
    decimal MonthlyBalance,
    decimal BreakEvenRate,
    decimal EffectiveCustomerCount,
    List<BreakEvenSeriesPoint> SeriesPoints);

public sealed record CustomerRow(string Name, string Service, string CityLimits);

public sealed record ProjectionRow(string Year, decimal Rate);

public sealed record WorkspaceReserveTrajectoryPointData(DateTime DateUtc, decimal PredictedReserves, decimal ConfidenceInterval);

public sealed record WorkspaceReserveTrajectoryData(
    decimal CurrentReserves,
    decimal RecommendedReserveLevel,
    string RiskAssessment,
    List<WorkspaceReserveTrajectoryPointData> ForecastPoints);

public sealed record WorkspaceSnapshotSaveResponse(long SnapshotId, string SnapshotName, string SavedAtUtc);

public sealed record WorkspaceScenarioSaveRequest(
    string ScenarioName,
    string? Description,
    WorkspaceBootstrapData Snapshot);

public sealed record WorkspaceScenarioSummaryResponse(
    long SnapshotId,
    string ScenarioName,
    string SelectedEnterprise,
    int SelectedFiscalYear,
    string CreatedAtUtc,
    decimal? CurrentRate,
    decimal? TotalCosts,
    decimal? ProjectedVolume,
    decimal ScenarioCostTotal,
    int ScenarioItemCount,
    string? Description);

public sealed record WorkspaceScenarioCollectionResponse(List<WorkspaceScenarioSummaryResponse> Scenarios);

public sealed record WorkspaceBaselineUpdateRequest(
    string SelectedEnterprise,
    int SelectedFiscalYear,
    decimal CurrentRate,
    decimal TotalCosts,
    decimal ProjectedVolume);

public sealed record WorkspaceBaselineUpdateResponse(
    string SelectedEnterprise,
    int SelectedFiscalYear,
    string SavedAtUtc,
    string Message,
    WorkspaceBootstrapData Snapshot);

public sealed record WorkspaceNavigationClickRequest(
    string PanelKey,
    string PanelRoute,
    string ActivePanelKey,
    string SelectedEnterprise,
    int SelectedFiscalYear,
    bool IsLoadingWorkspace,
    bool IsSidebarOpen,
    string ClickedAtUtc);

public sealed record WorkspaceSnapshotArtifactRequest(List<string>? DocumentKinds, bool ReplaceExisting = false);

public sealed record WorkspaceSnapshotArtifactSummary(
    long ArtifactId,
    long SnapshotId,
    string DocumentKind,
    string FileName,
    string ContentType,
    long SizeBytes,
    string CreatedAtUtc,
    string DownloadUrl);

public sealed record WorkspaceSnapshotArtifactBatchResponse(
    long SnapshotId,
    string SnapshotName,
    List<WorkspaceSnapshotArtifactSummary> Artifacts);

public sealed record WorkspaceBootstrapData(
    string SelectedEnterprise,
    int SelectedFiscalYear,
    string ActiveScenarioName,
    decimal? CurrentRate,
    decimal? TotalCosts,
    decimal? ProjectedVolume,
    string? LastUpdatedUtc)
{
    public List<string>? EnterpriseOptions { get; init; }
    public List<int>? FiscalYearOptions { get; init; }
    public List<string>? CustomerServiceOptions { get; init; }
    public List<string>? CustomerCityLimitOptions { get; init; }
    public List<CustomerRow>? CustomerRows { get; init; }
    public List<ProjectionRow>? ProjectionRows { get; init; }
    public List<WorkspaceScenarioItemData>? ScenarioItems { get; init; }
    public List<BreakEvenQuadrantData>? BreakEvenQuadrants { get; init; }
    public List<ApartmentUnitTypeData>? ApartmentUnitTypes { get; init; }
    public WorkspaceReserveTrajectoryData? ReserveTrajectory { get; init; }
}