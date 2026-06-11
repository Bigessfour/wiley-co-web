using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace WileyWidget.Services.Abstractions;

public static class EnterpriseCostSources
{
    public const string Ledger = "ledger";
    public const string Baseline = "baseline";
    public const string Snapshot = "snapshot";
}

public sealed record EnterpriseLedgerCostResult(
    string EnterpriseName,
    int FiscalYear,
    bool HasLedgerData,
    int MatchedRowCount,
    decimal AnnualOperatingExpenses,
    decimal MonthlyOperatingExpenses);

public interface IEnterpriseLedgerCostService
{
    Task<EnterpriseLedgerCostResult> ComputeAsync(string enterpriseName, int fiscalYear, CancellationToken cancellationToken = default);

    Task<Dictionary<string, EnterpriseLedgerCostResult>> ComputeForCanonicalEnterprisesAsync(int fiscalYear, CancellationToken cancellationToken = default);

    Task<int> RefreshEnterpriseMonthlyExpensesAsync(int fiscalYear, CancellationToken cancellationToken = default);

    Task<int> RefreshEnterpriseMonthlyExpensesForSourceFileAsync(long sourceFileId, CancellationToken cancellationToken = default);
}
