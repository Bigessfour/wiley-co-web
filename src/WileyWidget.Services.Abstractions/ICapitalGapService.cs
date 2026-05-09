using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace WileyWidget.Services.Abstractions;

public interface ICapitalGapService
{
    Task<CapitalGapResult> BuildAsync(string enterpriseName, int fiscalYear, CancellationToken cancellationToken = default);
}

public sealed record CapitalGapItemPoint(
    string Label,
    string Tag,
    decimal BudgetedAmount,
    decimal ActualAmount,
    decimal CumulativeGap,
    string DepartmentName,
    string AccountName);

public sealed record CapitalGapResult(
    string SelectedEnterprise,
    int SelectedFiscalYear,
    decimal AnnualRateRevenue,
    decimal AnnualCapitalNeed,
    decimal RateRevenueGap,
    decimal CapitalNeedCoverageRatio,
    int CapitalItemCount,
    string CapitalStatus,
    string ExecutiveSummary,
    DateTime GeneratedAtUtc,
    IReadOnlyList<CapitalGapItemPoint> CapitalItems);

public sealed class CapitalGapNotFoundException : InvalidOperationException
{
    public CapitalGapNotFoundException(string message)
        : base(message)
    {
    }

    public CapitalGapNotFoundException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

public sealed class CapitalGapUnavailableException : InvalidOperationException
{
    public CapitalGapUnavailableException()
    {
    }

    public CapitalGapUnavailableException(string message)
        : base(message)
    {
    }

    public CapitalGapUnavailableException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}