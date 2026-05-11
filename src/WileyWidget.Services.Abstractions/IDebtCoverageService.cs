using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WileyWidget.Models;

namespace WileyWidget.Services.Abstractions;

public interface IDebtCoverageService
{
    Task<DebtCoverageResult> BuildAsync(string enterpriseName, int fiscalYear, CancellationToken cancellationToken = default);
}

public sealed record DebtCoverageWaterfallPoint(
    string Label,
    double Value);

public sealed record DebtCoverageResult(
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
    DateTime GeneratedAtUtc,
    IReadOnlyList<DebtCoverageWaterfallPoint> WaterfallPoints);

public sealed class DebtCoverageNotFoundException : InvalidOperationException
{
    public DebtCoverageNotFoundException(string message)
        : base(message)
    {
    }

    public DebtCoverageNotFoundException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

public sealed class DebtCoverageUnavailableException : InvalidOperationException
{
    public DebtCoverageUnavailableException()
    {
    }

    public DebtCoverageUnavailableException(string message)
        : base(message)
    {
    }

    public DebtCoverageUnavailableException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}