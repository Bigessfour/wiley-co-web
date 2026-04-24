using System.Collections.Generic;

namespace WileyCoWeb.Contracts;

public sealed record DebtCoverageRequest(
    string SelectedEnterprise,
    int SelectedFiscalYear);

public sealed record DebtCoverageWaterfallPoint(
    string Label,
    double Value);

public sealed record DebtCoverageResponse(
    string SelectedEnterprise,
    int SelectedFiscalYear,
    decimal AnnualRevenue,
    decimal AnnualDebtService,
    decimal ReserveHeadroom,
    decimal DebtServiceCoverageRatio,
    decimal CovenantThreshold,
    decimal CovenantHeadroom,
    string CovenantStatus,
    string ExecutiveSummary,
    string GeneratedAtUtc,
    IReadOnlyList<DebtCoverageWaterfallPoint> WaterfallPoints);