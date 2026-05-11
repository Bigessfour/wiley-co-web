using System.Collections.Generic;

namespace WileyCoWeb.Contracts;

public sealed record CapitalGapRequest(
    string SelectedEnterprise,
    int SelectedFiscalYear);

public sealed record CapitalGapItemPoint(
    string Label,
    string Tag,
    decimal BudgetedAmount,
    decimal ActualAmount,
    decimal CumulativeGap,
    string DepartmentName,
    string AccountName);

public sealed record CapitalGapResponse(
    string SelectedEnterprise,
    int SelectedFiscalYear,
    decimal AnnualRateRevenue,
    decimal AnnualCapitalNeed,
    decimal RateRevenueGap,
    decimal CapitalNeedCoverageRatio,
    int CapitalItemCount,
    string CapitalStatus,
    string ExecutiveSummary,
    string GeneratedAtUtc,
    IReadOnlyList<CapitalGapItemPoint> CapitalItems);