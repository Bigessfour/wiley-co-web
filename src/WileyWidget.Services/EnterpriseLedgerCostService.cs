using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using WileyWidget.Data;
using WileyWidget.Models;
using WileyWidget.Models.ImportSchema;
using WileyWidget.Services.Abstractions;

namespace WileyWidget.Services;

public sealed class EnterpriseLedgerCostService : IEnterpriseLedgerCostService
{
    private static readonly Regex AccountCodeRegex = new(@"(?<code>\d{3,5}(?:\.\d{2})?)", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly IDbContextFactory<AppDbContext> contextFactory;
    private readonly IConfiguration configuration;
    private readonly ILogger<EnterpriseLedgerCostService> logger;

    public EnterpriseLedgerCostService(
        IDbContextFactory<AppDbContext> contextFactory,
        IConfiguration configuration,
        ILogger<EnterpriseLedgerCostService> logger)
    {
        this.contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
        this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<EnterpriseLedgerCostResult> ComputeAsync(string enterpriseName, int fiscalYear, CancellationToken cancellationToken = default)
    {
        if (!WorkspaceEnterpriseCatalog.TryNormalizeEnterpriseName(enterpriseName, out var normalizedName))
        {
            normalizedName = enterpriseName.Trim();
        }

        var all = await ComputeForCanonicalEnterprisesAsync(fiscalYear, cancellationToken).ConfigureAwait(false);
        return all.TryGetValue(normalizedName, out var result)
            ? result
            : BuildEmptyResult(normalizedName, fiscalYear);
    }

    public async Task<Dictionary<string, EnterpriseLedgerCostResult>> ComputeForCanonicalEnterprisesAsync(int fiscalYear, CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var ledgerRows = await context.LedgerEntries
            .AsNoTracking()
            .Include(entry => entry.SourceFile)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var results = new Dictionary<string, EnterpriseLedgerCostResult>(StringComparer.OrdinalIgnoreCase);
        foreach (var enterpriseName in WorkspaceEnterpriseCatalog.CanonicalEnterpriseOrder)
        {
            var scopedRows = FilterRowsForEnterprise(ledgerRows, enterpriseName, fiscalYear);
            results[enterpriseName] = BuildResult(enterpriseName, fiscalYear, scopedRows);
        }

        return results;
    }

    public async Task<int> RefreshEnterpriseMonthlyExpensesAsync(int fiscalYear, CancellationToken cancellationToken = default)
    {
        var costs = await ComputeForCanonicalEnterprisesAsync(fiscalYear, cancellationToken).ConfigureAwait(false);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var enterprises = await context.Enterprises
            .Where(enterprise => !enterprise.IsDeleted)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var updatedCount = 0;
        var now = DateTime.UtcNow;
        foreach (var enterprise in enterprises)
        {
            if (!costs.TryGetValue(enterprise.Name, out var rollup) || !rollup.HasLedgerData)
            {
                continue;
            }

            enterprise.MonthlyExpenses = rollup.MonthlyOperatingExpenses;
            enterprise.ModifiedDate = now;
            enterprise.LastModified = now;
            enterprise.ModifiedBy = nameof(EnterpriseLedgerCostService);
            updatedCount++;

            logger.LogInformation(
                "Updated {Enterprise} MonthlyExpenses to {MonthlyCosts} from ledger FY {FiscalYear} ({RowCount} rows, annual {AnnualCosts}).",
                enterprise.Name,
                rollup.MonthlyOperatingExpenses,
                fiscalYear,
                rollup.MatchedRowCount,
                rollup.AnnualOperatingExpenses);
        }

        if (updatedCount > 0)
        {
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return updatedCount;
    }

    public async Task<int> RefreshEnterpriseMonthlyExpensesForSourceFileAsync(long sourceFileId, CancellationToken cancellationToken = default)
    {
        if (sourceFileId <= 0)
        {
            return 0;
        }

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var sourceFile = await context.SourceFiles
            .AsNoTracking()
            .Include(file => file.LedgerEntries)
            .FirstOrDefaultAsync(file => file.Id == sourceFileId, cancellationToken)
            .ConfigureAwait(false);

        if (sourceFile is null)
        {
            return 0;
        }

        var fiscalYears = ResolveFiscalYearsForSourceFile(sourceFile);
        var updatedCount = 0;
        foreach (var fiscalYear in fiscalYears)
        {
            updatedCount += await RefreshEnterpriseMonthlyExpensesAsync(fiscalYear, cancellationToken).ConfigureAwait(false);
        }

        return updatedCount;
    }

    internal static HashSet<int> ResolveFiscalYearsForSourceFile(SourceFile sourceFile)
    {
        var fiscalYears = new HashSet<int>();
        var fromFileName = TryResolveFiscalYearFromFileName(sourceFile.OriginalFileName);
        if (fromFileName.HasValue)
        {
            fiscalYears.Add(fromFileName.Value);
        }

        foreach (var entry in sourceFile.LedgerEntries)
        {
            if (entry.EntryDate.HasValue)
            {
                fiscalYears.Add(entry.EntryDate.Value.Year);
            }
        }

        if (fiscalYears.Count == 0)
        {
            fiscalYears.Add(DateTime.UtcNow.Year);
        }

        return fiscalYears;
    }

    internal static List<LedgerEntry> FilterRowsForEnterprise(IReadOnlyList<LedgerEntry> rows, string enterpriseName, int fiscalYear)
    {
        var aliases = WorkspaceEnterpriseCatalog.GetAliases(enterpriseName)
            .Select(alias => alias.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return rows
            .Where(row => MatchesEntryScope(row.EntryScope, aliases))
            .Where(row => MatchesFiscalYear(row, fiscalYear))
            .Where(row => CountsTowardOperatingExpense(row))
            .ToList();
    }

    internal static EnterpriseLedgerCostResult BuildResult(string enterpriseName, int fiscalYear, IReadOnlyList<LedgerEntry> rows)
    {
        if (rows.Count == 0)
        {
            return BuildEmptyResult(enterpriseName, fiscalYear);
        }

        var annual = decimal.Round(rows.Sum(row => Math.Abs(row.Amount ?? 0m)), 2, MidpointRounding.AwayFromZero);
        var monthly = decimal.Round(annual / 12m, 2, MidpointRounding.AwayFromZero);

        return new EnterpriseLedgerCostResult(
            enterpriseName,
            fiscalYear,
            true,
            rows.Count,
            annual,
            monthly);
    }

    private static EnterpriseLedgerCostResult BuildEmptyResult(string enterpriseName, int fiscalYear)
        => new(enterpriseName, fiscalYear, false, 0, 0m, 0m);

    private static bool MatchesEntryScope(string? entryScope, IReadOnlyList<string> aliases)
    {
        if (string.IsNullOrWhiteSpace(entryScope))
        {
            return false;
        }

        return aliases.Any(alias => string.Equals(entryScope.Trim(), alias, StringComparison.OrdinalIgnoreCase));
    }

    private static bool MatchesFiscalYear(LedgerEntry row, int fiscalYear)
    {
        if (row.EntryDate.HasValue)
        {
            return row.EntryDate.Value.Year == fiscalYear;
        }

        var fileName = row.SourceFile?.OriginalFileName;
        return TryResolveFiscalYearFromFileName(fileName) == fiscalYear;
    }

    internal static bool CountsTowardOperatingExpense(LedgerEntry row)
    {
        if (ShouldExcludeByEntryType(row.EntryType))
        {
            return false;
        }

        var primaryCode = ExtractAccountCode(row.AccountName);
        var splitCode = ExtractAccountCode(row.SplitAccount);

        // QuickBooks general-ledger exports often post against cash (1xx) with the offset on Split.
        if (primaryCode is not null && IsBalanceSheetAccount(primaryCode) && splitCode is not null)
        {
            return ClassifiesAsOperatingExpense(splitCode);
        }

        var accountCode = primaryCode ?? splitCode;
        if (accountCode is null)
        {
            return false;
        }

        return ClassifiesAsOperatingExpense(accountCode);
    }

    private static bool ClassifiesAsOperatingExpense(string accountCode)
    {
        if (IsBalanceSheetAccount(accountCode) || IsRevenueAccount(accountCode))
        {
            return false;
        }

        return IsExpenseAccount(accountCode) || IsWileyOperatingExpenseAccount(accountCode);
    }

    private static bool ShouldExcludeByEntryType(string? entryType)
    {
        if (string.IsNullOrWhiteSpace(entryType))
        {
            return false;
        }

        return entryType.Contains("deposit", StringComparison.OrdinalIgnoreCase)
            || entryType.Contains("transfer", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsExpenseAccount(string accountCode)
        => accountCode.StartsWith('5') || accountCode.StartsWith('6');

    private static bool IsWileyOperatingExpenseAccount(string accountCode)
        => accountCode.Length >= 2
            && accountCode[0] == '4'
            && accountCode[1] >= '5';

    private static bool IsRevenueAccount(string accountCode)
        => accountCode.Length >= 2
            && accountCode[0] == '4'
            && accountCode[1] <= '2';

    private static bool IsBalanceSheetAccount(string accountCode)
        => accountCode.StartsWith('1') || accountCode.StartsWith('2') || accountCode.StartsWith('3');

    private static string? ExtractAccountCode(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var match = AccountCodeRegex.Match(value);
        return match.Success ? match.Groups["code"].Value : null;
    }

    private static int? TryResolveFiscalYearFromFileName(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return null;
        }

        var match = Regex.Match(fileName, @"FY(?<year>\d{2,4})", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (!match.Success || !int.TryParse(match.Groups["year"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var fiscalYear))
        {
            return null;
        }

        return fiscalYear < 100 ? 2000 + fiscalYear : fiscalYear;
    }
}
