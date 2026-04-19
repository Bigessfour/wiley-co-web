using System.Globalization;
using System.IO.Compression;
using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using WorkspaceReferenceDataImportRequest = WileyCoWeb.Contracts.WorkspaceReferenceDataImportRequest;
using WorkspaceReferenceDataImportResponse = WileyCoWeb.Contracts.WorkspaceReferenceDataImportResponse;
using WileyWidget.Business.Interfaces;
using WileyWidget.Data;
using WileyWidget.Models;
using WileyWidget.Services;

namespace WileyCoWeb.Api;

internal sealed partial class WorkspaceReferenceDataImportService
{

    private readonly IDbContextFactory<AppDbContext> contextFactory;
    private readonly IBudgetRepository budgetRepository;
    private readonly QuickBooksImportService quickBooksImportService;
    private readonly IConfiguration configuration;
    private readonly ILogger<WorkspaceReferenceDataImportService> logger;

    public WorkspaceReferenceDataImportService(
        IDbContextFactory<AppDbContext> contextFactory,
        IBudgetRepository budgetRepository,
        QuickBooksImportService quickBooksImportService,
        IConfiguration configuration,
        ILogger<WorkspaceReferenceDataImportService> logger)
    {
        this.contextFactory = Require(contextFactory, nameof(contextFactory));
        this.budgetRepository = Require(budgetRepository, nameof(budgetRepository));
        this.quickBooksImportService = Require(quickBooksImportService, nameof(quickBooksImportService));
        this.configuration = Require(configuration, nameof(configuration));
        this.logger = Require(logger, nameof(logger));
    }

    private static T Require<T>(T? value, string parameterName)
        where T : class
        => value ?? throw new ArgumentNullException(parameterName);

    private async Task<UtilityCustomerImportSummary> ImportUtilityCustomersAsync(
        AppDbContext context,
        string importDataPath,
        DateTime importedAtUtc,
        CancellationToken cancellationToken)
        => await ImportUtilityCustomersCoreAsync(context, importDataPath, importedAtUtc, cancellationToken).ConfigureAwait(false);

    private async Task<UtilityCustomerImportSummary> ImportUtilityCustomersCoreAsync(
        AppDbContext context,
        string importDataPath,
        DateTime importedAtUtc,
        CancellationToken cancellationToken)
    {
        var customerWorkbooks = EnumerateCustomerWorkbooks(importDataPath).ToList();

        if (customerWorkbooks.Count == 0)
        {
            return CreateNoCustomerWorkbookSummary();
        }

        var customersByAccountNumber = await LoadExistingUtilityCustomersAsync(context, cancellationToken).ConfigureAwait(false);

        var synchronizedAccountNumbers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var stats = new UtilityCustomerImportStats();

        ProcessCustomerWorkbooks(
            customerWorkbooks,
            importedAtUtc,
            context,
            customersByAccountNumber,
            synchronizedAccountNumbers,
            stats,
            cancellationToken);

        return BuildUtilityCustomerImportSummary(stats);
    }

    private void ProcessCustomerWorkbooks(IReadOnlyList<string> customerWorkbooks, DateTime importedAtUtc, AppDbContext context, IDictionary<string, UtilityCustomer> customersByAccountNumber, HashSet<string> synchronizedAccountNumbers, UtilityCustomerImportStats stats, CancellationToken cancellationToken) { foreach (var filePath in customerWorkbooks) { cancellationToken.ThrowIfCancellationRequested(); ProcessCustomerWorkbook(filePath, importedAtUtc, context, customersByAccountNumber, synchronizedAccountNumbers, stats); } }

    private static IEnumerable<string> EnumerateCustomerWorkbooks(string importDataPath)
    {
        return Directory.EnumerateFiles(importDataPath, "*", SearchOption.TopDirectoryOnly)
            .Where(path => Path.GetExtension(path).Equals(".xlsx", StringComparison.OrdinalIgnoreCase))
            .Where(path => IsCustomerWorkbook(Path.GetFileName(path)))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase);
    }

    private static UtilityCustomerImportSummary CreateNoCustomerWorkbookSummary()
    {
        return new UtilityCustomerImportSummary(0, 0, 0, 0, 0, 0, 0, 0, "No QuickBooks customer workbook was found. UtilityCustomers were left unchanged.");
    }

    private async Task<Dictionary<string, UtilityCustomer>> LoadExistingUtilityCustomersAsync(
        AppDbContext context,
        CancellationToken cancellationToken)
    {
        return await context.UtilityCustomers
            .ToDictionaryAsync(customer => customer.AccountNumber, StringComparer.OrdinalIgnoreCase, cancellationToken)
            .ConfigureAwait(false);
    }

    private void ProcessCustomerWorkbook(string filePath, DateTime importedAtUtc, AppDbContext context, IDictionary<string, UtilityCustomer> customersByAccountNumber, HashSet<string> synchronizedAccountNumbers, UtilityCustomerImportStats stats) { if (!TryReadCustomerWorkbookRows(filePath, out var workbookMetadata, out var workbookRows)) { return; } stats.WorkbookCount++; stats.RowCount += workbookRows.Count; ProcessCustomerWorkbookRows(filePath, workbookMetadata!, workbookRows, importedAtUtc, context, customersByAccountNumber, synchronizedAccountNumbers, stats); }

    private bool TryReadCustomerWorkbookRows(string filePath, out WorkbookMetadata? workbookMetadata, out IReadOnlyList<CustomerWorkbookRow> workbookRows) { workbookMetadata = TryValidateWorkbookMetadata(filePath); if (workbookMetadata is null) { workbookRows = []; return false; } workbookRows = LoadCustomerRowsFromWorkbook(filePath, workbookMetadata); return workbookRows.Count > 0; }

    private WorkbookMetadata? TryValidateWorkbookMetadata(string filePath) { var workbookMetadata = TryReadWorkbookMetadata(filePath); if (workbookMetadata is null) { logger.LogWarning("Skipping QuickBooks customer import for {FileName} because the workbook metadata could not be read.", Path.GetFileName(filePath)); } return workbookMetadata; }

    private IReadOnlyList<CustomerWorkbookRow> LoadCustomerRowsFromWorkbook(string filePath, WorkbookMetadata workbookMetadata)
    {
        var workbookData = ReadCustomerWorkbookData(filePath, workbookMetadata);
        return workbookData.Rows.Count == 0 ? [] : workbookData.Rows;
    }

    private void ProcessCustomerWorkbookRows(string filePath, WorkbookMetadata workbookMetadata, IReadOnlyList<CustomerWorkbookRow> workbookRows, DateTime importedAtUtc, AppDbContext context, IDictionary<string, UtilityCustomer> customersByAccountNumber, HashSet<string> synchronizedAccountNumbers, UtilityCustomerImportStats stats) => ProcessCustomerRows(filePath, ResolveEnterpriseNameOrDefault(workbookMetadata.CompanyFileName, Path.GetFileName(filePath)), workbookRows, importedAtUtc, context, customersByAccountNumber, synchronizedAccountNumbers, stats);

    private static string ResolveEnterpriseNameOrDefault(string? companyFileName, string fileName)
        => ResolveEnterpriseName(companyFileName, fileName) ?? "Water Utility";

    private void ProcessCustomerRows(string filePath, string enterpriseName, IReadOnlyList<CustomerWorkbookRow> workbookRows, DateTime importedAtUtc, AppDbContext context, IDictionary<string, UtilityCustomer> customersByAccountNumber, HashSet<string> synchronizedAccountNumbers, UtilityCustomerImportStats stats) { foreach (var row in workbookRows) { ProcessCustomerRow(filePath, enterpriseName, row, importedAtUtc, context, customersByAccountNumber, synchronizedAccountNumbers, stats); } }

    private void ProcessCustomerRow(string filePath, string enterpriseName, CustomerWorkbookRow row, DateTime importedAtUtc, AppDbContext context, IDictionary<string, UtilityCustomer> customersByAccountNumber, HashSet<string> synchronizedAccountNumbers, UtilityCustomerImportStats stats) { var preparedCustomer = PrepareUtilityCustomer(filePath, enterpriseName, row, importedAtUtc); if (preparedCustomer is null) { stats.SkippedCount++; return; } ProcessPreparedCustomerRow(preparedCustomer, synchronizedAccountNumbers, importedAtUtc, context, customersByAccountNumber, stats); }

    private static void ProcessPreparedCustomerRow(PreparedUtilityCustomer? preparedCustomer, HashSet<string> synchronizedAccountNumbers, DateTime importedAtUtc, AppDbContext context, IDictionary<string, UtilityCustomer> customersByAccountNumber, UtilityCustomerImportStats stats) { if (!TryTrackPreparedCustomerImport(preparedCustomer!, synchronizedAccountNumbers)) { return; } RecordPreparedCustomerImport(preparedCustomer!, importedAtUtc, context, customersByAccountNumber, stats); }

    private static bool TryTrackPreparedCustomerImport(
        PreparedUtilityCustomer preparedCustomer,
        HashSet<string> synchronizedAccountNumbers)
        => synchronizedAccountNumbers.Add(preparedCustomer.Customer.AccountNumber);

    private static void RecordPreparedCustomerImport(PreparedUtilityCustomer preparedCustomer, DateTime importedAtUtc, AppDbContext context, IDictionary<string, UtilityCustomer> customersByAccountNumber, UtilityCustomerImportStats stats) { IncrementPreparedCustomerImportAddressCount(preparedCustomer, stats); if (TryUpdateExistingPreparedCustomer(preparedCustomer, importedAtUtc, customersByAccountNumber, stats)) { return; } AddPreparedCustomer(context, preparedCustomer, customersByAccountNumber, stats); }

    private static void IncrementPreparedCustomerImportAddressCount(PreparedUtilityCustomer preparedCustomer, UtilityCustomerImportStats stats) { if (preparedCustomer.UsedFallbackAddress) { stats.FallbackAddressCount++; return; } stats.StructuredAddressCount++; }

    private static bool TryUpdateExistingPreparedCustomer(PreparedUtilityCustomer preparedCustomer, DateTime importedAtUtc, IDictionary<string, UtilityCustomer> customersByAccountNumber, UtilityCustomerImportStats stats) { if (!customersByAccountNumber.TryGetValue(preparedCustomer.Customer.AccountNumber, out var existingCustomer)) { return false; } CopyImportedCustomer(preparedCustomer.Customer, existingCustomer, importedAtUtc); stats.UpdatedCount++; return true; }

    private static void AddPreparedCustomer(
        AppDbContext context,
        PreparedUtilityCustomer preparedCustomer,
        IDictionary<string, UtilityCustomer> customersByAccountNumber,
        UtilityCustomerImportStats stats)
    {
        context.UtilityCustomers.Add(preparedCustomer.Customer);
        customersByAccountNumber.Add(preparedCustomer.Customer.AccountNumber, preparedCustomer.Customer);
        stats.InsertedCount++;
    }

    private static UtilityCustomerImportSummary BuildUtilityCustomerImportSummary(UtilityCustomerImportStats stats)
    {
        var importedCount = stats.InsertedCount + stats.UpdatedCount;
        return new UtilityCustomerImportSummary(importedCount, stats.InsertedCount, stats.UpdatedCount, stats.SkippedCount, stats.WorkbookCount, stats.RowCount, stats.StructuredAddressCount, stats.FallbackAddressCount, BuildUtilityCustomerImportStatus(importedCount, stats.InsertedCount, stats.UpdatedCount, stats.SkippedCount, stats.WorkbookCount, stats.RowCount, stats.StructuredAddressCount, stats.FallbackAddressCount));
    }

    private sealed class UtilityCustomerImportStats
    {
        public int WorkbookCount { get; set; }

        public int RowCount { get; set; }

        public int InsertedCount { get; set; }

        public int UpdatedCount { get; set; }

        public int SkippedCount { get; set; }

        public int StructuredAddressCount { get; set; }

        public int FallbackAddressCount { get; set; }
    }

    private async Task<LedgerImportSummary> ImportSampleLedgerDataAsync(string importDataPath, CancellationToken cancellationToken) { var ledgerFiles = EnumerateSampleLedgerFiles(importDataPath).ToList(); if (ledgerFiles.Count == 0) { return new LedgerImportSummary(0, 0, "No sample QuickBooks ledger files were found in the import folder."); } var stats = new SampleLedgerImportStats(); foreach (var filePath in ledgerFiles) { cancellationToken.ThrowIfCancellationRequested(); await ProcessSampleLedgerFileAsync(filePath, cancellationToken, stats).ConfigureAwait(false); } stats.UpdatedBudgetRowCount = await RefreshBudgetActualsForFiscalYearsAsync(stats.FiscalYears, cancellationToken).ConfigureAwait(false); return BuildSampleLedgerImportSummary(stats); }

    private static IEnumerable<string> EnumerateSampleLedgerFiles(string importDataPath) => Directory.EnumerateFiles(importDataPath, "*", SearchOption.TopDirectoryOnly).Where(IsSampleLedgerFile).GroupBy(path => Path.GetFileNameWithoutExtension(Path.GetFileName(path)), StringComparer.OrdinalIgnoreCase).Select(group => group.OrderByDescending(path => GetSampleLedgerFilePreference(path)).ThenBy(path => path, StringComparer.OrdinalIgnoreCase).First()).OrderBy(path => path, StringComparer.OrdinalIgnoreCase);

    private async Task ProcessSampleLedgerFileAsync(string filePath, CancellationToken cancellationToken, SampleLedgerImportStats stats) { var importContext = ResolveSampleLedgerImportContext(filePath); stats.FiscalYears.Add(importContext.FiscalYear); if (string.IsNullOrWhiteSpace(importContext.EnterpriseName)) { logger.LogWarning("Skipping sample QuickBooks ledger import for {FileName} because no enterprise mapping could be resolved.", importContext.FileName); return; } await CommitSampleLedgerImportAsync(filePath, importContext, cancellationToken, stats).ConfigureAwait(false); }

    private SampleLedgerImportContext ResolveSampleLedgerImportContext(string filePath)
    {
        var fileName = Path.GetFileName(filePath);
        return new SampleLedgerImportContext(fileName, ResolveFiscalYear(fileName), ResolveEnterpriseName(TryReadWorkbookMetadata(filePath)?.CompanyFileName, fileName));
    }

    private async Task CommitSampleLedgerImportAsync(string filePath, SampleLedgerImportContext importContext, CancellationToken cancellationToken, SampleLedgerImportStats stats) { var fileBytes = await File.ReadAllBytesAsync(filePath, cancellationToken).ConfigureAwait(false); var commitResult = await quickBooksImportService.CommitAsync(fileBytes, importContext.FileName, importContext.EnterpriseName!, importContext.FiscalYear, cancellationToken).ConfigureAwait(false); if (commitResult.IsDuplicate) { stats.DuplicateFileCount++; return; } stats.ImportedFileCount++; stats.ImportedRowCount += commitResult.ImportedRows; }

    private sealed record SampleLedgerImportContext(string FileName, int FiscalYear, string? EnterpriseName);

    private async Task<int> RefreshBudgetActualsForFiscalYearsAsync(IEnumerable<int> fiscalYears, CancellationToken cancellationToken)
    {
        var updatedBudgetRowCount = 0;
        foreach (var fiscalYear in fiscalYears.OrderBy(year => year))
        {
            updatedBudgetRowCount += await RefreshBudgetActualsFromImportedLedgersAsync(fiscalYear, cancellationToken).ConfigureAwait(false);
        }

        return updatedBudgetRowCount;
    }

    private static LedgerImportSummary BuildSampleLedgerImportSummary(SampleLedgerImportStats stats) => new(stats.ImportedFileCount, stats.ImportedRowCount, stats.ImportedFileCount > 0 ? $"Imported {stats.ImportedRowCount} QuickBooks ledger rows from {stats.ImportedFileCount} sample file(s) and refreshed {stats.UpdatedBudgetRowCount} budget actual row(s)." : stats.DuplicateFileCount > 0 ? $"Skipped {stats.DuplicateFileCount} duplicate QuickBooks sample ledger file(s) and refreshed {stats.UpdatedBudgetRowCount} budget actual row(s)." : "No sample QuickBooks ledger rows were imported.");

    private sealed class SampleLedgerImportStats
    {
        public HashSet<int> FiscalYears { get; } = new();

        public int ImportedFileCount { get; set; }

        public int ImportedRowCount { get; set; }

        public int DuplicateFileCount { get; set; }

        public int UpdatedBudgetRowCount { get; set; }
    }

    private async Task<int> RefreshBudgetActualsFromImportedLedgersAsync(int fiscalYear, CancellationToken cancellationToken)
    {
        var budgetEntries = await LoadBudgetEntriesForFiscalYearAsync(fiscalYear, cancellationToken).ConfigureAwait(false);

        if (budgetEntries.Count == 0)
        {
            return 0;
        }

        var actualsByBudgetAccount = await BuildActualsByBudgetAccountForFiscalYearAsync(budgetEntries, fiscalYear, cancellationToken).ConfigureAwait(false);

        if (actualsByBudgetAccount.Count == 0)
        {
            return 0;
        }

        return await UpdateBudgetActualsAsync(actualsByBudgetAccount, fiscalYear, cancellationToken).ConfigureAwait(false);
    }

    private async Task<Dictionary<string, decimal>> BuildActualsByBudgetAccountForFiscalYearAsync(
        IReadOnlyList<BudgetEntrySnapshot> budgetEntries,
        int fiscalYear,
        CancellationToken cancellationToken)
    {
        var ledgerRows = await LoadImportedLedgerRowsAsync(cancellationToken).ConfigureAwait(false);
        var actualsByNormalizedCode = BuildActualsByNormalizedCode(ledgerRows, fiscalYear);

        if (actualsByNormalizedCode.Count == 0)
        {
            logger.LogInformation("No imported general-ledger actuals matched FY {FiscalYear} budget rows.", fiscalYear);
            return new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        }

        var actualsByBudgetAccount = BuildActualsByBudgetAccount(budgetEntries, actualsByNormalizedCode);

        if (actualsByBudgetAccount.Count == 0)
        {
            logger.LogInformation("No imported general-ledger actuals aligned to exact FY {FiscalYear} budget accounts.", fiscalYear);
            return new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        }

        return actualsByBudgetAccount;
    }

    private async Task<int> UpdateBudgetActualsAsync(IDictionary<string, decimal> actualsByBudgetAccount, int fiscalYear, CancellationToken cancellationToken)
    {
        var updatedRows = await budgetRepository.BulkUpdateActualsAsync(actualsByBudgetAccount, fiscalYear, cancellationToken).ConfigureAwait(false);
        logger.LogInformation("Refreshed {UpdatedRows} budget actual row(s) from imported general-ledger samples for FY {FiscalYear}.", updatedRows, fiscalYear);
        return updatedRows;
    }

    private async Task<List<BudgetEntrySnapshot>> LoadBudgetEntriesForFiscalYearAsync(int fiscalYear, CancellationToken cancellationToken)
    {
        return (await budgetRepository.GetByFiscalYearAsync(fiscalYear, cancellationToken).ConfigureAwait(false))
            .Where(entry => !string.IsNullOrWhiteSpace(entry.AccountNumber))
            .Select(entry => new BudgetEntrySnapshot(entry.AccountNumber!, NormalizeAccountNumber(entry.AccountNumber)))
            .ToList();
    }

    private async Task<List<ImportedLedgerRow>> LoadImportedLedgerRowsAsync(CancellationToken cancellationToken) { await using var context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false); return await context.LedgerEntries.AsNoTracking().Select(entry => new ImportedLedgerRow(entry.AccountName ?? string.Empty, entry.Amount ?? 0m, entry.SourceFile.OriginalFileName)).ToListAsync(cancellationToken).ConfigureAwait(false); }

    private static Dictionary<string, decimal> BuildActualsByNormalizedCode(IEnumerable<ImportedLedgerRow> ledgerRows, int fiscalYear) => ledgerRows.Where(row => LooksLikeGeneralLedgerFile(row.OriginalFileName)).Where(row => row.OriginalFileName is not null && ResolveFiscalYear(row.OriginalFileName) == fiscalYear).Select(row => new { NormalizedCode = NormalizeAccountNumber(ExtractAccountCode(row.AccountName)), row.Amount }).Where(row => row.NormalizedCode != null).GroupBy(row => row.NormalizedCode!, StringComparer.OrdinalIgnoreCase).ToDictionary(group => group.Key, group => Math.Abs(group.Sum(row => row.Amount)), StringComparer.OrdinalIgnoreCase);

    private static Dictionary<string, decimal> BuildActualsByBudgetAccount(IEnumerable<BudgetEntrySnapshot> budgetEntries, IReadOnlyDictionary<string, decimal> actualsByNormalizedCode)
    => budgetEntries.Where(entry => entry.NormalizedCode != null && actualsByNormalizedCode.ContainsKey(entry.NormalizedCode)).ToDictionary(entry => entry.AccountNumber!, entry => actualsByNormalizedCode[entry.NormalizedCode!], StringComparer.OrdinalIgnoreCase);

    private sealed record BudgetEntrySnapshot(string AccountNumber, string? NormalizedCode);

    private sealed record ImportedLedgerRow(string AccountName, decimal Amount, string? OriginalFileName);

    private static string? ExtractAccountCode(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var match = AccountCodeRegex.Match(value);
        return match.Success ? match.Groups["code"].Value : null;
    }

    private static string? NormalizeAccountNumber(string? accountNumber) => string.IsNullOrWhiteSpace(accountNumber) ? null : NormalizeAccountNumberCore(accountNumber);

    private static string NormalizeAccountNumberCore(string accountNumber) { var trimmed = accountNumber.Trim(); var separatorIndex = trimmed.IndexOf('.'); return separatorIndex < 0 ? trimmed : NormalizeFractionalAccountNumber(trimmed, separatorIndex); }

    private static string NormalizeFractionalAccountNumber(string trimmed, int separatorIndex)
    {
        var wholePart = trimmed[..separatorIndex];
        var fractionalPart = trimmed[(separatorIndex + 1)..].TrimEnd('0');
        return fractionalPart.Length == 0 ? wholePart : $"{wholePart}.{fractionalPart}";
    }

    private static bool LooksLikeGeneralLedgerFile(string? fileName)
        => !string.IsNullOrWhiteSpace(fileName)
            && fileName.Contains("GeneralLedger", StringComparison.OrdinalIgnoreCase);

    private static int GetSampleLedgerFilePreference(string filePath)
        => Path.GetExtension(filePath).ToLowerInvariant() switch
        {
            ".xlsx" => 2,
            ".xls" => 1,
            _ => 0
        };

    private static List<EnterpriseReferenceSource> DiscoverEnterpriseSources(string importDataPath)
    {
        var groupedSources = new Dictionary<string, EnterpriseReferenceSourceBuilder>(StringComparer.OrdinalIgnoreCase);

        foreach (var filePath in EnumerateEnterpriseReferenceFiles(importDataPath))
        {
            ProcessEnterpriseSourceFile(filePath, groupedSources);
        }

        return groupedSources.Values.Select(builder => builder.Build()).ToList();
    }

    private static IEnumerable<string> EnumerateEnterpriseReferenceFiles(string importDataPath)
    {
        return Directory.EnumerateFiles(importDataPath, "*", SearchOption.TopDirectoryOnly)
            .Where(IsEnterpriseReferenceFile);
    }

    private static bool IsEnterpriseReferenceFile(string filePath)
    {
        var extension = Path.GetExtension(filePath);
        return extension.Equals(".xlsx", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".csv", StringComparison.OrdinalIgnoreCase);
    }

    private static void ProcessEnterpriseSourceFile(string filePath, IDictionary<string, EnterpriseReferenceSourceBuilder> groupedSources) { var fileName = Path.GetFileName(filePath); var workbookMetadata = TryReadWorkbookMetadata(filePath); if (!TryResolveEnterpriseName(fileName, workbookMetadata, out var enterpriseName)) { return; } var builder = GetOrCreateEnterpriseReferenceSourceBuilder(groupedSources, enterpriseName); builder.AddSourceFile(fileName); TryRecordCustomerWorkbookSource(filePath, fileName, workbookMetadata, builder); }

    private static void TryProcessEnterpriseSourceFile(string filePath, IDictionary<string, EnterpriseReferenceSourceBuilder> groupedSources)
    {
        ProcessEnterpriseSourceFile(filePath, groupedSources);
    }

    private static bool TryResolveEnterpriseName(string fileName, WorkbookMetadata? workbookMetadata, out string enterpriseName) { enterpriseName = ResolveEnterpriseName(workbookMetadata?.CompanyFileName, fileName) ?? string.Empty; return !string.IsNullOrWhiteSpace(enterpriseName); }

    private static void TryRecordCustomerWorkbookSource(string filePath, string fileName, WorkbookMetadata? workbookMetadata, EnterpriseReferenceSourceBuilder builder) { if (workbookMetadata is null || !IsCustomerWorkbook(fileName)) { return; } builder.RecordCustomerWorkbook(InspectCustomerWorkbook(filePath, workbookMetadata)); }

    private static EnterpriseReferenceSourceBuilder GetOrCreateEnterpriseReferenceSourceBuilder(IDictionary<string, EnterpriseReferenceSourceBuilder> groupedSources, string enterpriseName) { if (groupedSources.TryGetValue(enterpriseName, out var builder)) { return builder; } builder = new EnterpriseReferenceSourceBuilder(enterpriseName, DeriveEnterpriseType(enterpriseName)); groupedSources.Add(enterpriseName, builder); return builder; }

    private string ResolveImportDataPath(string? requestedPath, string contentRootPath) => FindExistingImportDataPath(BuildImportDataPathCandidates(ResolveRequestedImportDataPath(requestedPath), contentRootPath));

    private string? ResolveRequestedImportDataPath(string? requestedPath) { requestedPath = ApplyConfiguredDefaultImportDataPath(requestedPath); EnsureExplicitImportDataPathRequirement(requestedPath); return requestedPath; }

    private string? ApplyConfiguredDefaultImportDataPath(string? requestedPath) => !string.IsNullOrWhiteSpace(requestedPath) ? requestedPath : string.IsNullOrWhiteSpace(configuration["WorkspaceReferenceData:DefaultImportDataPath"]) ? requestedPath : configuration["WorkspaceReferenceData:DefaultImportDataPath"];

    private void EnsureExplicitImportDataPathRequirement(string? requestedPath) { if (string.IsNullOrWhiteSpace(requestedPath) && configuration.GetValue<bool>("WorkspaceReferenceData:RequireExplicitImportDataPath")) { throw new InvalidOperationException("Workspace reference-data import requires an explicit importDataPath or WorkspaceReferenceData:DefaultImportDataPath. Production containers do not assume a bundled Import Data folder."); } }

    private static string FindExistingImportDataPath(IReadOnlyList<string> candidates) { foreach (var candidate in candidates) { var fullPath = Path.GetFullPath(candidate); if (Directory.Exists(fullPath)) { return fullPath; } } return Path.GetFullPath(candidates[0]); }

    private static List<string> BuildImportDataPathCandidates(string? requestedPath, string contentRootPath) => string.IsNullOrWhiteSpace(requestedPath) ? BuildDefaultImportDataPathCandidates(contentRootPath) : BuildRequestedImportDataPathCandidates(requestedPath, contentRootPath);

    private static List<string> BuildRequestedImportDataPathCandidates(string requestedPath, string contentRootPath)
        => Path.IsPathRooted(requestedPath)
            ? [requestedPath]
            : [Path.Combine(contentRootPath, requestedPath), Path.Combine(Directory.GetCurrentDirectory(), requestedPath), Path.Combine(contentRootPath, "..", requestedPath)];

    private static List<string> BuildDefaultImportDataPathCandidates(string contentRootPath)
        => [Path.Combine(contentRootPath, "Import Data"), Path.Combine(contentRootPath, "..", "Import Data"), Path.Combine(Directory.GetCurrentDirectory(), "Import Data")];

    private static WorkbookMetadata? TryReadWorkbookMetadata(string filePath) => IsExcelWorkbook(filePath) ? TryReadWorkbookMetadataCoreSafe(filePath) : null;

    private static WorkbookMetadata? TryReadWorkbookMetadataCoreSafe(string filePath)
    {
        try
        {
            return ReadWorkbookMetadataCore(filePath);
        }
        catch (InvalidDataException)
        {
            return null;
        }
    }

    private static WorkbookMetadata? ReadWorkbookMetadataCore(string filePath) { using var archive = ZipFile.OpenRead(filePath); var workbookDocument = LoadWorkbookDocument(archive); return workbookDocument is null ? null : new WorkbookMetadata(ExtractCompanyFileName(workbookDocument), ParseWorksheets(workbookDocument, ParseRelationshipTargets(LoadRelationshipsDocument(archive)))); }

    private static bool IsExcelWorkbook(string filePath)
        => Path.GetExtension(filePath).Equals(".xlsx", StringComparison.OrdinalIgnoreCase);

    private static XDocument? LoadWorkbookDocument(ZipArchive archive)
        => archive.GetEntry("xl/workbook.xml") is { } workbookEntry ? XDocument.Load(workbookEntry.Open()) : null;

    private static XDocument? LoadRelationshipsDocument(ZipArchive archive)
        => archive.GetEntry("xl/_rels/workbook.xml.rels") is { } relationshipsEntry ? XDocument.Load(relationshipsEntry.Open()) : null;

    private static IReadOnlyDictionary<string, string> ParseRelationshipTargets(XDocument? relationshipsDocument) { var targets = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase); if (relationshipsDocument?.Root is null) { return targets; } foreach (var relationship in relationshipsDocument.Root.Elements()) { AddRelationshipTarget(relationship, targets); } return targets; }

    private static void AddRelationshipTarget(XElement relationship, IDictionary<string, string> targets)
    {
        if (!TryReadRelationshipTarget(relationship, out var relationshipId, out var target))
        {
            return;
        }

        targets[relationshipId] = target;
    }

    private static void TryAddRelationshipTarget(XElement relationship, IDictionary<string, string> targets)
    {
        AddRelationshipTarget(relationship, targets);
    }

    private static bool TryReadRelationshipTarget(XElement relationship, out string relationshipId, out string target) { relationshipId = string.Empty; target = string.Empty; if (!IsRelationshipElement(relationship)) { return false; } if (!TryGetRelationshipTargetValues(relationship, out relationshipId, out target)) { return false; } return true; }

    private static bool IsRelationshipElement(XElement relationship)
        => string.Equals(relationship.Name.LocalName, "Relationship", StringComparison.Ordinal);

    private static bool TryGetRelationshipTargetValues(XElement relationship, out string relationshipId, out string target)
    {
        relationshipId = GetRelationshipAttributeValue(relationship, "Id");
        target = GetRelationshipAttributeValue(relationship, "Target");
        return HasRelationshipTargetValues(relationshipId, target);
    }

    private static string GetRelationshipAttributeValue(XElement relationship, string attributeName)
        => (string?)relationship.Attribute(attributeName) ?? string.Empty;

    private static bool HasRelationshipTargetValues(string relationshipId, string target)
        => !string.IsNullOrWhiteSpace(relationshipId) && !string.IsNullOrWhiteSpace(target);

    private static List<WorksheetMetadata> ParseWorksheets(XDocument workbookDocument, IReadOnlyDictionary<string, string> relationshipTargets) => workbookDocument.Root is null ? [] : workbookDocument.Root.Descendants(SpreadsheetNamespace + "sheet").Select(sheet => CreateWorksheetMetadata(sheet, relationshipTargets)).Where(sheet => !string.IsNullOrWhiteSpace(sheet.EntryName)).ToList();

    private static WorksheetMetadata CreateWorksheetMetadata(XElement sheet, IReadOnlyDictionary<string, string> relationshipTargets)
        => new((string?)sheet.Attribute("name") ?? string.Empty, GetWorksheetTarget(sheet, relationshipTargets));

    private static string GetWorksheetTarget(XElement sheet, IReadOnlyDictionary<string, string> relationshipTargets) { var relationshipId = (string?)sheet.Attribute(RelationshipNamespace + "id") ?? string.Empty; relationshipTargets.TryGetValue(relationshipId, out var target); return NormalizeWorksheetTarget(target); }

    private static string? ExtractCompanyFileName(XDocument workbookDocument)
        => NormalizeExtractedCompanyFileName(FindWorkbookCompanyFileName(workbookDocument));

    private static XElement? FindWorkbookCompanyFileName(XDocument workbookDocument)
        => workbookDocument.Root?
            .Descendants(SpreadsheetNamespace + "definedName")
            .FirstOrDefault(element => string.Equals((string?)element.Attribute("name"), "QBCOMPANYFILENAME", StringComparison.OrdinalIgnoreCase));

    private static string? NormalizeExtractedCompanyFileName(XElement? definedName)
    {
        if (definedName is null)
        {
            return null;
        }

        var normalizedValue = definedName.Value.Trim().Trim('"');
        return string.IsNullOrWhiteSpace(normalizedValue) ? null : normalizedValue;
    }

    private static CustomerWorkbookInspection InspectCustomerWorkbook(string filePath, WorkbookMetadata workbookMetadata)
    {
        var workbookData = ReadCustomerWorkbookData(filePath, workbookMetadata);
        return new CustomerWorkbookInspection(
            workbookData.Rows.Count,
            workbookData.Rows.Count > 0,
            workbookData.Headers);
    }

    private static CustomerWorkbookData ReadCustomerWorkbookData(string filePath, WorkbookMetadata workbookMetadata) => GetCustomerWorkbookWorksheet(workbookMetadata) is { } worksheet ? BuildCustomerWorkbookDataOrEmpty(ReadCustomerWorkbookRows(filePath, worksheet)) : EmptyCustomerWorkbookData;

    private static List<Dictionary<string, string>> ReadCustomerWorkbookRows(string filePath, WorksheetMetadata worksheet)
    { using var archive = ZipFile.OpenRead(filePath); var worksheetEntry = archive.GetEntry(worksheet.EntryName); if (worksheetEntry == null) { return []; } var sharedStrings = ReadSharedStrings(archive); return ReadWorksheetRows(worksheetEntry, sharedStrings); }

    private static CustomerWorkbookData BuildCustomerWorkbookData(IReadOnlyList<Dictionary<string, string>> rows)
    {
        var headerRow = rows[0];
        var headers = BuildWorkbookHeaders(headerRow);
        var normalizedHeaderColumns = BuildNormalizedHeaderColumns(headerRow);
        return new CustomerWorkbookData(headers, BuildWorkbookRows(rows, normalizedHeaderColumns));
    }

    private static CustomerWorkbookData BuildCustomerWorkbookDataOrEmpty(IReadOnlyList<Dictionary<string, string>> rows)
        => rows.Count == 0 ? EmptyCustomerWorkbookData : BuildCustomerWorkbookData(rows);

    private static WorksheetMetadata? GetCustomerWorkbookWorksheet(WorkbookMetadata workbookMetadata)
        => workbookMetadata.Worksheets.FirstOrDefault(sheet => !sheet.Name.Contains("Tips", StringComparison.OrdinalIgnoreCase));

    private static List<Dictionary<string, string>> ReadWorksheetRows(ZipArchiveEntry worksheetEntry, IReadOnlyList<string> sharedStrings)
    {
        var worksheetDocument = XDocument.Load(worksheetEntry.Open());
        return worksheetDocument.Root?
            .Descendants(SpreadsheetNamespace + "row")
            .Select(row => ReadWorksheetRow(row, sharedStrings))
            .Where(values => values.Count > 0)
            .ToList()
            ?? [];
    }

    private static string[] BuildWorkbookHeaders(IReadOnlyDictionary<string, string> headerRow)
        => headerRow.Values
            .Select(value => NormalizeWhitespace(value))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToArray();

    private static IReadOnlyDictionary<string, string> BuildNormalizedHeaderColumns(IReadOnlyDictionary<string, string> headerRow)
        => headerRow
            .Where(entry => !string.IsNullOrWhiteSpace(entry.Value))
            .GroupBy(entry => NormalizeHeader(entry.Value), StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First().Key, StringComparer.Ordinal);

    private static List<CustomerWorkbookRow> BuildWorkbookRows(IReadOnlyList<Dictionary<string, string>> rows, IReadOnlyDictionary<string, string> normalizedHeaderColumns) { var workbookRows = new List<CustomerWorkbookRow>(); foreach (var row in rows.Skip(1)) { var workbookRow = BuildWorkbookRow(row, normalizedHeaderColumns); if (workbookRow is not null) { workbookRows.Add(workbookRow); } } return workbookRows; }

    private static CustomerWorkbookRow? BuildWorkbookRow(IReadOnlyDictionary<string, string> row, IReadOnlyDictionary<string, string> normalizedHeaderColumns) => string.IsNullOrWhiteSpace(GetWorkbookValue(row, normalizedHeaderColumns, "customer", "C")) ? null : new CustomerWorkbookRow(GetWorkbookValue(row, normalizedHeaderColumns, "customer", "C"), GetWorkbookValue(row, normalizedHeaderColumns, "billto", "E"), GetWorkbookValue(row, normalizedHeaderColumns, "primarycontact", "G"), GetWorkbookValue(row, normalizedHeaderColumns, "mainphone", "I"), GetWorkbookValue(row, normalizedHeaderColumns, "fax", "K"), ParseWorkbookDecimal(GetWorkbookValue(row, normalizedHeaderColumns, "balancetotal", "M")));

    private static List<string> ReadSharedStrings(ZipArchive archive)
    {
        return TryLoadSharedStringsDocument(archive) is { } sharedStringsDocument
            ? ExtractSharedStrings(sharedStringsDocument)
            : [];
    }

    private static XDocument? TryLoadSharedStringsDocument(ZipArchive archive)
    => archive.GetEntry("xl/sharedStrings.xml") is { } sharedStringsEntry ? XDocument.Load(sharedStringsEntry.Open()) : null;

    private static List<string> ExtractSharedStrings(XDocument sharedStringsDocument)
    {
        return sharedStringsDocument.Root?
            .Descendants(SpreadsheetNamespace + "si")
            .Select(item => string.Concat(item.Descendants(SpreadsheetNamespace + "t").Select(textNode => textNode.Value)))
            .ToList()
            ?? [];
    }

    private static string ReadCellValue(XElement cell, IReadOnlyList<string> sharedStrings)
        => ReadCellValueCore((string?)cell.Attribute("t"), cell, sharedStrings);

    private static string ReadCellValueCore(string? cellType, XElement cell, IReadOnlyList<string> sharedStrings)
        => ResolveCellValueReader(cellType ?? string.Empty)(cell, sharedStrings);

    private static Func<XElement, IReadOnlyList<string>, string> ResolveCellValueReader(string cellType)
        => CellValueReaders.TryGetValue(cellType, out var reader) ? reader : ReadDefaultCellValue;

    private static string ReadDefaultCellValue(XElement cell, IReadOnlyList<string> sharedStrings)
        => TryReadDefaultCellValue(cell) ?? string.Empty;

    private static string? TryReadDefaultCellValue(XElement cell)
        => cell.Element(SpreadsheetNamespace + "v")?.Value;

    private static string ReadSharedStringValue(XElement cell, IReadOnlyList<string> sharedStrings)
        => TryReadSharedStringIndex(cell, sharedStrings.Count, out var sharedStringIndex) ? sharedStrings[sharedStringIndex] : string.Empty;

    private static string ReadInlineStringCellValue(XElement cell)
        => string.Concat(cell.Descendants(SpreadsheetNamespace + "t").Select(node => node.Value));

    private static bool TryReadSharedStringCellValue(XElement cell, IReadOnlyList<string> sharedStrings, out string value)
        => TryGetSharedStringValue(cell, sharedStrings, out value);

    private static bool TryGetSharedStringValue(XElement cell, IReadOnlyList<string> sharedStrings, out string value) { if (TryGetSharedStringIndex(cell, sharedStrings.Count, out var sharedStringIndex)) { value = sharedStrings[sharedStringIndex]; return true; } value = string.Empty; return false; }

    private static bool TryGetSharedStringIndex(XElement cell, int sharedStringCount, out int sharedStringIndex)
        => TryReadSharedStringIndex(cell, sharedStringCount, out sharedStringIndex);

    private static bool TryReadSharedStringIndex(XElement cell, int sharedStringCount, out int sharedStringIndex)
    {
        sharedStringIndex = -1;
        return TryReadSharedStringIndexCore(cell, sharedStringCount, out sharedStringIndex);
    }

    private static bool TryReadSharedStringIndexCore(XElement cell, int sharedStringCount, out int sharedStringIndex)
    {
        sharedStringIndex = -1;
        return TryReadSharedStringIndexCoreValue(cell, sharedStringCount, out sharedStringIndex);
    }

    private static bool TryReadSharedStringIndexCoreValue(XElement cell, int sharedStringCount, out int sharedStringIndex)
    {
        sharedStringIndex = -1;

        if (!IsSharedStringCell(cell))
        {
            return false;
        }

        return TryParseSharedStringIndex(GetSharedStringCellValue(cell), sharedStringCount, out sharedStringIndex);
    }

    private static string GetSharedStringCellValue(XElement cell)
    {
        return cell.Element(SpreadsheetNamespace + "v")?.Value ?? string.Empty;
    }

    private static bool IsSharedStringCell(XElement cell)
        => string.Equals((string?)cell.Attribute("t"), "s", StringComparison.OrdinalIgnoreCase);

    private static bool TryParseSharedStringIndex(string rawValue, int sharedStringCount, out int sharedStringIndex) => int.TryParse(rawValue, out sharedStringIndex) && sharedStringIndex >= 0 && sharedStringIndex < sharedStringCount;

    private static Dictionary<string, string> ReadWorksheetRow(XElement row, IReadOnlyList<string> sharedStrings)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var cell in row.Elements(SpreadsheetNamespace + "c"))
        {
            TryAddWorksheetCellValue(cell, sharedStrings, values);
        }

        return values;
    }

    private static void TryAddWorksheetCellValue(XElement cell, IReadOnlyList<string> sharedStrings, IDictionary<string, string> values)
    {
        if (!TryGetWorksheetCellColumnName(cell, out var columnName))
        {
            return;
        }

        var value = NormalizeWhitespace(ReadCellValue(cell, sharedStrings));
        if (!string.IsNullOrWhiteSpace(value))
        {
            values[columnName] = value;
        }
    }

    private static bool TryGetWorksheetCellColumnName(XElement cell, out string columnName)
    {
        var reference = (string?)cell.Attribute("r");
        if (string.IsNullOrWhiteSpace(reference))
        {
            columnName = string.Empty;
            return false;
        }

        columnName = GetColumnName(reference);
        return !string.IsNullOrWhiteSpace(columnName);
    }

    private static string GetColumnName(string cellReference)
        => new(cellReference.TakeWhile(char.IsLetter).ToArray());

    private static string GetWorkbookValue(
        IReadOnlyDictionary<string, string> row,
        IReadOnlyDictionary<string, string> normalizedHeaderColumns,
        string normalizedHeader,
        string fallbackColumn)
        => TryGetWorkbookValue(row, normalizedHeaderColumns, normalizedHeader, fallbackColumn, out var value)
            ? value
            : string.Empty;

    private static bool TryGetWorkbookValue(IReadOnlyDictionary<string, string> row, IReadOnlyDictionary<string, string> normalizedHeaderColumns, string normalizedHeader, string fallbackColumn, out string value) { if (TryGetWorkbookHeaderValue(row, normalizedHeaderColumns, normalizedHeader, out value)) { return true; } return TryGetWorkbookFallbackValue(row, fallbackColumn, out value); }

    private static bool TryGetWorkbookHeaderValue(IReadOnlyDictionary<string, string> row, IReadOnlyDictionary<string, string> normalizedHeaderColumns, string normalizedHeader, out string value) { if (!normalizedHeaderColumns.TryGetValue(normalizedHeader, out var columnName)) { value = string.Empty; return false; } return TryReadWorkbookCellValue(row, columnName, out value); }

    private static bool TryGetWorkbookFallbackValue(IReadOnlyDictionary<string, string> row, string fallbackColumn, out string value) => TryReadWorkbookCellValue(row, fallbackColumn, out value);

    private static bool TryReadWorkbookCellValue(IReadOnlyDictionary<string, string> row, string columnName, out string value) { var hasValue = row.TryGetValue(columnName, out var lookedUpValue) && lookedUpValue is not null; value = lookedUpValue ?? string.Empty; return hasValue; }

    private static string NormalizeHeader(string value)
        => NonAlphaNumericRegex.Replace(value, string.Empty).ToLowerInvariant();

    private static decimal ParseWorkbookDecimal(string value) => decimal.TryParse(value, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0m;

    private static string NormalizeWorksheetTarget(string? target) => string.IsNullOrWhiteSpace(target) ? string.Empty : target.Replace('\\', '/').StartsWith("xl/", StringComparison.OrdinalIgnoreCase) ? target.Replace('\\', '/') : $"xl/{target.Replace('\\', '/').TrimStart('/')}";

    private static string? ResolveEnterpriseName(string? companyFileName, string fileName)
    {
        return NormalizeCompanyName(companyFileName) ?? ResolveEnterpriseNameFromFileName(fileName);
    }

    private static string? NormalizeCompanyName(string? companyFileName)
    => string.IsNullOrWhiteSpace(companyFileName) ? null : NormalizeCompanyNameCore(companyFileName);

    private static string NormalizeCompanyNameCore(string companyFileName) { var rawName = Path.GetFileNameWithoutExtension(companyFileName.Trim()); rawName = rawName.Replace('_', ' ').Trim(); rawName = System.Text.RegularExpressions.Regex.Replace(rawName, "\\s+\\d{2,4}$", string.Empty).Trim(); var overrideName = CompanyNameOverrides.FirstOrDefault(item => rawName.Contains(item.Pattern, StringComparison.OrdinalIgnoreCase)).Name; return string.IsNullOrWhiteSpace(overrideName) ? CultureInfo.InvariantCulture.TextInfo.ToTitleCase(rawName.ToLowerInvariant()) : overrideName; }

    private static string? ResolveEnterpriseNameFromFileName(string fileName)
        => EnterpriseNameOverrides
            .FirstOrDefault(item => fileName.Contains(item.Pattern, StringComparison.OrdinalIgnoreCase))
            .Name;

    private static string DeriveEnterpriseType(string enterpriseName) => ResolveEnterpriseTypeOverride(enterpriseName) ?? "Water";

    private static string? ResolveEnterpriseTypeOverride(string enterpriseName)
        => EnterpriseTypeOverrides
            .FirstOrDefault(item => enterpriseName.Contains(item.Pattern, StringComparison.OrdinalIgnoreCase))
            .Type;

    private static bool IsCustomerWorkbook(string fileName)
        => fileName.Contains("Customer", StringComparison.OrdinalIgnoreCase);

    private static bool IsSampleLedgerFile(string filePath) { var fileName = Path.GetFileName(filePath); return IsSupportedSampleLedgerExtension(fileName) && !IsCustomerWorkbook(fileName) && fileName.Contains("GeneralLedger", StringComparison.OrdinalIgnoreCase); }

    private static bool IsSupportedSampleLedgerExtension(string fileName) { var extension = Path.GetExtension(fileName); return extension.Equals(".csv", StringComparison.OrdinalIgnoreCase) || extension.Equals(".xlsx", StringComparison.OrdinalIgnoreCase) || extension.Equals(".xls", StringComparison.OrdinalIgnoreCase); }

    private static int ResolveFiscalYear(string fileName) => TryResolveFiscalYear(fileName) is { } fiscalYear ? NormalizeFiscalYear(fiscalYear) : DateTime.UtcNow.Year;

    private static int? TryResolveFiscalYear(string fileName) => int.TryParse(System.Text.RegularExpressions.Regex.Match(fileName, @"FY(?<year>\d{2,4})", System.Text.RegularExpressions.RegexOptions.IgnoreCase).Groups["year"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var fiscalYear) ? fiscalYear : null;

    private static int NormalizeFiscalYear(int fiscalYear) => fiscalYear < 100 ? 2000 + fiscalYear : fiscalYear;

    private static bool ApplyDefaultBaseline(Enterprise enterprise) => DefaultEnterpriseBaselines.TryGetValue(enterprise.Name, out var baseline) && ApplyDefaultBaselineCore(enterprise, baseline);

    private static bool ApplyDefaultBaselineCore(Enterprise enterprise, EnterpriseBaselineSeed baseline) => ApplyDefaultCurrentRateBaseline(enterprise, baseline) | ApplyDefaultMonthlyExpensesBaseline(enterprise, baseline) | ApplyDefaultCitizenCountBaseline(enterprise, baseline);

    private static bool ApplyDefaultCurrentRateBaseline(Enterprise enterprise, EnterpriseBaselineSeed baseline)
    { if (enterprise.CurrentRate > 0m) { return false; } enterprise.CurrentRate = baseline.CurrentRate; return true; }

    private static bool ApplyDefaultMonthlyExpensesBaseline(Enterprise enterprise, EnterpriseBaselineSeed baseline)
    { if (enterprise.MonthlyExpenses > 0m) { return false; } enterprise.MonthlyExpenses = baseline.MonthlyExpenses; return true; }

    private static bool ApplyDefaultCitizenCountBaseline(Enterprise enterprise, EnterpriseBaselineSeed baseline)
    { if (enterprise.CitizenCount >= baseline.CitizenCount) { return false; } enterprise.CitizenCount = baseline.CitizenCount; return true; }

    private static string BuildEnterpriseNotes(EnterpriseReferenceSource source) => $"Source files: {BuildEnterpriseFileSummary(source)}. {BuildEnterpriseCustomerNote(source)}";

    private static string BuildEnterpriseFileSummary(EnterpriseReferenceSource source)
    {
        var fileSummary = string.Join(", ", source.SourceFiles.OrderBy(name => name, StringComparer.OrdinalIgnoreCase).Take(6));
        return source.SourceFiles.Count > 6 ? $"{fileSummary}, +{source.SourceFiles.Count - 6} more" : fileSummary;
    }

    private static string BuildEnterpriseCustomerNote(EnterpriseReferenceSource source)
        => source.DiscoveredCustomerReferenceRows > 0
            ? $"Detected {source.DiscoveredCustomerReferenceRows} QuickBooks customer reference rows for this enterprise. UtilityCustomer records can be synthesized from the customer name and bill-to fields when structured utility account data is missing."
            : "No QuickBooks utility-customer roster was imported for this enterprise.";

    private PreparedUtilityCustomer? PrepareUtilityCustomer(string filePath, string enterpriseName, CustomerWorkbookRow row, DateTime importedAtUtc)
    {
        var displayName = NormalizeWhitespace(row.DisplayName);
        if (string.IsNullOrWhiteSpace(displayName))
        {
            return null;
        }

        var preparedCustomer = BuildPreparedUtilityCustomer(filePath, enterpriseName, row, importedAtUtc, displayName);

        if (!TryValidateUtilityCustomer(preparedCustomer.Customer, out var validationErrors))
        {
            logger.LogWarning(
                "Skipping imported QuickBooks customer row '{CustomerName}' from {FileName} because the synthesized UtilityCustomer record was invalid: {ValidationErrors}",
                displayName,
                Path.GetFileName(filePath),
                validationErrors);
            return null;
        }

        return preparedCustomer;
    }

    private static PreparedUtilityCustomer BuildPreparedUtilityCustomer(string filePath, string enterpriseName, CustomerWorkbookRow row, DateTime importedAtUtc, string displayName) { var identity = ResolveCustomerIdentity(displayName, row.PrimaryContact); var address = ResolveCustomerAddress(displayName, row.BillTo); var customer = CreateUtilityCustomer(filePath, enterpriseName, row, importedAtUtc, displayName, identity, address); return new PreparedUtilityCustomer(customer, address.UsedFallbackAddress); }

    private static UtilityCustomer CreateUtilityCustomer(string filePath, string enterpriseName, CustomerWorkbookRow row, DateTime importedAtUtc, string displayName, ParsedCustomerIdentity identity, ParsedCustomerAddress address) { var customer = new UtilityCustomer(); ApplyUtilityCustomerIdentity(customer, identity); ApplyUtilityCustomerAddress(customer, address); ApplyUtilityCustomerImportMetadata(customer, filePath, enterpriseName, row, importedAtUtc, displayName, address); return customer; }

    private static void ApplyUtilityCustomerIdentity(UtilityCustomer customer, ParsedCustomerIdentity identity) { ApplyUtilityCustomerName(customer, identity); customer.CustomerType = identity.CustomerType; }

    private static void ApplyUtilityCustomerName(UtilityCustomer customer, ParsedCustomerIdentity identity) { customer.FirstName = Truncate(identity.FirstName, 50) ?? "Imported"; customer.LastName = Truncate(identity.LastName, 50) ?? "Customer"; customer.CompanyName = Truncate(identity.CompanyName, 100); }

    private static void ApplyUtilityCustomerAddress(UtilityCustomer customer, ParsedCustomerAddress address) { ApplyUtilityCustomerServiceAddress(customer, address); ApplyUtilityCustomerMailingAddress(customer, address); customer.ServiceLocation = address.ServiceLocation; }

    private static void ApplyUtilityCustomerServiceAddress(UtilityCustomer customer, ParsedCustomerAddress address)
    {
        ApplyUtilityCustomerServiceStreet(customer, address);
        ApplyUtilityCustomerServiceLocation(customer, address);
    }

    private static void ApplyUtilityCustomerServiceStreet(UtilityCustomer customer, ParsedCustomerAddress address)
    {
        customer.ServiceAddress = Truncate(address.ServiceAddress, 200) ?? "Imported customer reference";
    }

    private static void ApplyUtilityCustomerServiceLocation(UtilityCustomer customer, ParsedCustomerAddress address)
    {
        ApplyUtilityCustomerServiceCity(customer, address);
        ApplyUtilityCustomerServiceState(customer, address);
        ApplyUtilityCustomerServiceZipCode(customer, address);
    }

    private static void ApplyUtilityCustomerServiceCity(UtilityCustomer customer, ParsedCustomerAddress address)
    {
        customer.ServiceCity = Truncate(address.ServiceCity, 50) ?? "Wiley";
    }

    private static void ApplyUtilityCustomerServiceState(UtilityCustomer customer, ParsedCustomerAddress address)
    {
        customer.ServiceState = Truncate(address.ServiceState, 2) ?? "CO";
    }

    private static void ApplyUtilityCustomerServiceZipCode(UtilityCustomer customer, ParsedCustomerAddress address)
    {
        customer.ServiceZipCode = Truncate(address.ServiceZipCode, 10) ?? "81092";
    }

    private static void ApplyUtilityCustomerMailingAddress(UtilityCustomer customer, ParsedCustomerAddress address)
    {
        customer.MailingAddress = Truncate(address.MailingAddress, 200);
        customer.MailingCity = Truncate(address.MailingCity, 50);
        customer.MailingState = Truncate(address.MailingState, 2);
        customer.MailingZipCode = Truncate(address.MailingZipCode, 10);
    }

    private static void ApplyUtilityCustomerImportMetadata(UtilityCustomer customer, string filePath, string enterpriseName, CustomerWorkbookRow row, DateTime importedAtUtc, string displayName, ParsedCustomerAddress address) { customer.AccountNumber = BuildSyntheticAccountNumber(enterpriseName, displayName, row.BillTo); customer.PhoneNumber = NormalizePhoneNumber(row.MainPhone); customer.Status = CustomerStatus.Active; customer.AccountOpenDate = importedAtUtc.Date; customer.CurrentBalance = row.BalanceTotal; customer.Notes = BuildCustomerImportNotes(Path.GetFileName(filePath), enterpriseName, row, address.UsedFallbackAddress); customer.CreatedDate = importedAtUtc; customer.LastModifiedDate = importedAtUtc; }

    private static ParsedCustomerIdentity ResolveCustomerIdentity(string displayName, string primaryContact)
        => CustomerIdentityFactories.Select(factory => factory(displayName, primaryContact)).First(identity => identity is not null)!;

    private static ParsedCustomerIdentity? TryCreateResidentialCustomerIdentity(string displayName, string primaryContact)
        => LooksLikeIndividualName(displayName) ? CreateResidentialCustomerIdentity(displayName) : null;

    private static ParsedCustomerIdentity? TryCreateContactCustomerIdentity(string displayName, string primaryContact)
        => LooksLikeIndividualName(primaryContact) ? CreateContactCustomerIdentity(displayName, primaryContact) : null;

    private static ParsedCustomerIdentity CreateResidentialCustomerIdentity(string displayName)
    {
        var (firstName, lastName) = SplitPersonName(displayName);
        return new ParsedCustomerIdentity(firstName, lastName, null, CustomerType.Residential);
    }

    private static ParsedCustomerIdentity CreateContactCustomerIdentity(string displayName, string primaryContact)
    {
        var (firstName, lastName) = SplitPersonName(primaryContact);
        return new ParsedCustomerIdentity(firstName, lastName, displayName, ResolveCustomerType(displayName));
    }

    private static ParsedCustomerIdentity CreateDefaultCustomerIdentity(string displayName)
        => new("Imported", "Customer", displayName, ResolveCustomerType(displayName));

    private static CustomerType ResolveCustomerType(string displayName)
        => CustomerTypeResolvers
            .Select(resolver => resolver(displayName))
            .First(customerType => customerType is not null)
            .GetValueOrDefault();

    private static CustomerType? ResolveGovernmentCustomerType(string displayName)
        => IsGovernmentCustomer(displayName) ? CustomerType.Government : null;

    private static CustomerType? ResolveInstitutionalCustomerType(string displayName)
        => IsInstitutionalCustomer(displayName) ? CustomerType.Institutional : null;

    private static CustomerType? ResolveMultiFamilyCustomerType(string displayName)
        => IsMultiFamilyCustomer(displayName) ? CustomerType.MultiFamily : null;

    private static CustomerType? ResolveDefaultCustomerTypeAsNullable(string displayName)
        => LooksLikeIndividualName(displayName) ? CustomerType.Residential : CustomerType.Commercial;

    private static bool IsGovernmentCustomer(string displayName)
    {
        return ContainsAnyKeyword(displayName, GovernmentKeywords);
    }

    private static bool IsInstitutionalCustomer(string displayName)
    {
        return ContainsAnyKeyword(displayName, InstitutionalKeywords);
    }

    private static bool IsMultiFamilyCustomer(string displayName)
    {
        return ContainsAnyKeyword(displayName, MultiFamilyKeywords);
    }

    private static bool LooksLikeIndividualName(string? value)
        => IndividualNameRules.All(rule => rule(NormalizeWhitespace(value)));

    private static bool HasAllowedIndividualNameCharacters(string normalized)
        => normalized.IndexOfAny([',', '&', '/', '\\', '(', ')']) < 0;

    private static bool HasNoCorporateKeywords(string normalized)
        => !ContainsAnyKeyword(normalized, CorporateKeywords);

    private static bool HasValidIndividualNameTokens(string normalized)
    {
        var tokens = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return tokens.Length is >= 2 and <= 4 && tokens.All(token => PersonNameTokenRegex.IsMatch(token));
    }

    private static bool ContainsAnyKeyword(string value, IReadOnlyList<string> keywords)
        => keywords.Any(keyword => value.Contains(keyword, StringComparison.OrdinalIgnoreCase));

    private static (string FirstName, string LastName) SplitPersonName(string value)
    {
        var tokens = NormalizeWhitespace(value).Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (tokens.Length == 0)
        {
            return ("Imported", "Customer");
        }

        if (tokens.Length == 1)
        {
            return (tokens[0], "Customer");
        }

        return (tokens[0], string.Join(' ', tokens.Skip(1)));
    }

    private static ParsedCustomerAddress ResolveCustomerAddress(string displayName, string billTo)
    {
        var normalizedDisplayName = NormalizeWhitespace(displayName);
        var normalizedBillTo = NormalizeWhitespace(billTo);
        var trimmedBillTo = RemoveLeadingDisplayName(normalizedBillTo, normalizedDisplayName);

        return TryResolveStructuredCustomerAddress(trimmedBillTo)
            ?? TryResolveStructuredCustomerAddress(normalizedBillTo)
            ?? CreateFallbackCustomerAddress(displayName, normalizedBillTo, trimmedBillTo);
    }

    private static ParsedCustomerAddress? TryResolveStructuredCustomerAddress(string candidate) => TryParseStructuredCustomerAddress(candidate, out var street, out var city, out var state, out var zip) ? CreateStructuredCustomerAddress(street, city, state, zip) : null;

    private static bool TryMatchStructuredCustomerAddress(string candidate, out Match match)
    {
        match = CityStateZipRegex.Match(candidate);
        return match.Success;
    }

    private static bool TryParseStructuredCustomerAddress(string candidate, out string street, out string city, out string state, out string zip)
    { street = city = state = zip = string.Empty; return TryMatchStructuredCustomerAddress(candidate, out var match) && TryBuildStructuredCustomerAddress(match, out street, out city, out state, out zip); }

    private static bool TryBuildStructuredCustomerAddress(Match match, out string street, out string city, out string state, out string zip)
    {
        (street, city, state, zip, var isValid) = BuildStructuredCustomerAddressParts(match);
        return isValid;
    }

    private static (string Street, string City, string State, string Zip, bool IsValid) BuildStructuredCustomerAddressParts(Match match)
        => (NormalizeWhitespace(match.Groups["street"].Value.Trim(',', ' ')), NormalizeWhitespace(match.Groups["city"].Value), match.Groups["state"].Value.ToUpperInvariant(), match.Groups["zip"].Value, HasStructuredCustomerAddressParts(NormalizeWhitespace(match.Groups["street"].Value.Trim(',', ' ')), NormalizeWhitespace(match.Groups["city"].Value)));

    private static bool HasStructuredCustomerAddressParts(string street, string city)
        => !string.IsNullOrWhiteSpace(street) && !string.IsNullOrWhiteSpace(city);

    private static ParsedCustomerAddress CreateStructuredCustomerAddress(string street, string city, string state, string zip)
        => new ParsedCustomerAddress(street, city, state, zip, street, city, state, zip, city.Contains("Wiley", StringComparison.OrdinalIgnoreCase) ? ServiceLocation.InsideCityLimits : ServiceLocation.OutsideCityLimits, false);

    private static ParsedCustomerAddress CreateFallbackCustomerAddress(string displayName, string normalizedBillTo, string trimmedBillTo)
        => new ParsedCustomerAddress(ResolveFallbackCustomerAddressLine(displayName, trimmedBillTo), "Wiley", "CO", "81092", string.IsNullOrWhiteSpace(normalizedBillTo) ? null : normalizedBillTo, null, null, null, ServiceLocation.InsideCityLimits, true);

    private static string ResolveFallbackCustomerAddressLine(string displayName, string trimmedBillTo)
        => !string.IsNullOrWhiteSpace(trimmedBillTo)
            && !string.Equals(trimmedBillTo, displayName, StringComparison.OrdinalIgnoreCase)
            ? trimmedBillTo
            : "Imported customer reference";

    private static string RemoveLeadingDisplayName(string billTo, string displayName) => billTo.StartsWith(displayName, StringComparison.OrdinalIgnoreCase) ? billTo[displayName.Length..].TrimStart(',', ' ') : billTo;

    private static string BuildSyntheticAccountNumber(string enterpriseName, string displayName, string billTo)
    {
        var normalizedSource = $"{ResolveEnterpriseCode(enterpriseName)}|{NormalizeWhitespace(displayName)}|{NormalizeWhitespace(billTo)}";
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalizedSource)));
        return $"QB-{ResolveEnterpriseCode(enterpriseName)}-{hash[..10]}";
    }

    private static string ResolveEnterpriseCode(string enterpriseName)
    { var overrideCode = EnterpriseCodeOverrides.FirstOrDefault(item => enterpriseName.Contains(item.Pattern, StringComparison.OrdinalIgnoreCase)).Code; return string.IsNullOrWhiteSpace(overrideCode) ? BuildFallbackEnterpriseCode(enterpriseName) : overrideCode; }

    private static string BuildFallbackEnterpriseCode(string enterpriseName) { var normalized = new string(enterpriseName.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant(); return normalized.Length >= 3 ? normalized[..3] : normalized.PadRight(3, 'X'); }

    private static string? NormalizePhoneNumber(string value) { var digits = new string(value.Where(char.IsDigit).ToArray()); return digits.Length switch { 7 => $"{digits[..3]}-{digits[3..]}", 10 => $"({digits[..3]}) {digits[3..6]}-{digits[6..]}", 11 when digits[0] == '1' => $"+1 {digits[1..4]}-{digits[4..7]}-{digits[7..]}", _ => null }; }

    private static string BuildCustomerImportNotes(string fileName, string enterpriseName, CustomerWorkbookRow row, bool usedFallbackAddress)
    {
        var noteParts = BuildOptionalCustomerImportNoteParts(row, usedFallbackAddress).ToArray();
        var note = $"Imported from QuickBooks customer workbook '{fileName}' for {enterpriseName}.";
        return Truncate(noteParts.Length == 0 ? note : $"{note} {string.Join(' ', noteParts)}", 500) ?? string.Empty;
    }

    private static IEnumerable<string> BuildOptionalCustomerImportNoteParts(CustomerWorkbookRow row, bool usedFallbackAddress)
        => BuildOptionalCustomerImportNotePartsCore(row, usedFallbackAddress);

    private static IEnumerable<string> BuildOptionalCustomerImportNotePartsCore(CustomerWorkbookRow row, bool usedFallbackAddress)
    {
        var noteParts = new[]
        {
            BuildBillToNote(row),
            BuildFaxNote(row),
            usedFallbackAddress ? "Structured utility service-address fields were not present in the workbook, so the service location defaults were inferred." : null
        };

        return noteParts.Where(note => !string.IsNullOrWhiteSpace(note)).Select(note => note!);
    }

    private static bool TryBuildBillToNotePart(CustomerWorkbookRow row, out string note) => TryBuildCustomerWorkbookNotePart(BuildBillToNote(row), out note);

    private static string? BuildBillToNote(CustomerWorkbookRow row)
    {
        var billTo = NormalizeWhitespace(row.BillTo);
        return !string.IsNullOrWhiteSpace(billTo) && !string.Equals(billTo, NormalizeWhitespace(row.DisplayName), StringComparison.OrdinalIgnoreCase) ? $"Bill-to: {billTo}." : null;
    }

    private static bool TryBuildFaxNotePart(CustomerWorkbookRow row, out string note) => TryBuildCustomerWorkbookNotePart(BuildFaxNote(row), out note);

    private static string? BuildFaxNote(CustomerWorkbookRow row)
        => string.IsNullOrWhiteSpace(row.Fax) ? null : $"Fax: {NormalizeWhitespace(row.Fax)}.";

    private static bool TryBuildCustomerWorkbookNotePart(string? note, out string value) { value = note ?? string.Empty; return note is not null; }

    private static bool TryValidateUtilityCustomer(UtilityCustomer customer, out string validationErrors) { var results = new List<ValidationResult>(); var isValid = Validator.TryValidateObject(customer, new ValidationContext(customer), results, validateAllProperties: true); validationErrors = string.Join("; ", results.Select(result => result.ErrorMessage).Where(message => !string.IsNullOrWhiteSpace(message)).Select(message => message!)); return isValid; }

    private static void CopyImportedCustomer(UtilityCustomer source, UtilityCustomer target, DateTime importedAtUtc) { CopyUtilityCustomerIdentity(source, target); CopyUtilityCustomerAddress(source, target); CopyUtilityCustomerImportMetadata(source, target, importedAtUtc); }

    private static void CopyUtilityCustomerIdentity(UtilityCustomer source, UtilityCustomer target)
    {
        target.FirstName = source.FirstName;
        target.LastName = source.LastName;
        target.CompanyName = source.CompanyName;
        target.CustomerType = source.CustomerType;
    }

    private static void CopyUtilityCustomerAddress(UtilityCustomer source, UtilityCustomer target) { target.ServiceAddress = source.ServiceAddress; target.ServiceCity = source.ServiceCity; target.ServiceState = source.ServiceState; target.ServiceZipCode = source.ServiceZipCode; target.MailingAddress = source.MailingAddress; target.MailingCity = source.MailingCity; target.MailingState = source.MailingState; target.MailingZipCode = source.MailingZipCode; target.ServiceLocation = source.ServiceLocation; }

    private static void CopyUtilityCustomerImportMetadata(UtilityCustomer source, UtilityCustomer target, DateTime importedAtUtc) { target.AccountNumber = source.AccountNumber; target.PhoneNumber = source.PhoneNumber; target.Status = source.Status; target.AccountOpenDate = source.AccountOpenDate; target.CurrentBalance = source.CurrentBalance; target.Notes = source.Notes; if (target.CreatedDate == default) { target.CreatedDate = importedAtUtc; } target.LastModifiedDate = importedAtUtc; }

    private static string BuildUtilityCustomerImportStatus(int importedCount, int insertedCount, int updatedCount, int skippedCount, int workbookCount, int rowCount, int structuredAddressCount, int fallbackAddressCount) => workbookCount == 0 ? "No QuickBooks customer workbook was found. UtilityCustomers were left unchanged." : importedCount == 0 ? BuildNoImportedCustomerStatus(rowCount, workbookCount) : BuildImportedCustomerStatus(importedCount, insertedCount, updatedCount, skippedCount, workbookCount, structuredAddressCount, fallbackAddressCount);

    private static string BuildNoImportedCustomerStatus(int rowCount, int workbookCount)
        => $"Detected {rowCount} QuickBooks customer row(s) across {workbookCount} workbook(s), but none could be synchronized into UtilityCustomers.";

    private static string BuildImportedCustomerStatus(int importedCount, int insertedCount, int updatedCount, int skippedCount, int workbookCount, int structuredAddressCount, int fallbackAddressCount) { var status = $"Imported {importedCount} UtilityCustomer record(s) from {workbookCount} QuickBooks customer workbook(s) ({insertedCount} inserted, {updatedCount} updated)."; status = AppendStructuredAddressStatus(status, structuredAddressCount); status = AppendFallbackAddressStatus(status, fallbackAddressCount); return AppendSkippedRowStatus(status, skippedCount); }

    private static string AppendStructuredAddressStatus(string status, int structuredAddressCount)
        => structuredAddressCount > 0 ? $"{status} Parsed {structuredAddressCount} structured address row(s)." : status;

    private static string AppendFallbackAddressStatus(string status, int fallbackAddressCount)
        => fallbackAddressCount > 0
            ? $"{status} {fallbackAddressCount} row(s) used inferred Wiley service defaults because the workbook did not include structured utility service-address fields."
            : status;

    private static string AppendSkippedRowStatus(string status, int skippedCount)
        => skippedCount > 0 ? $"{status} Skipped {skippedCount} invalid or empty row(s)." : status;

    private static string NormalizeWhitespace(string? value)
            => string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : MultiWhitespaceRegex.Replace(value.Trim(), " ");

    private static string? Truncate(string? value, int maximumLength)
            => string.IsNullOrWhiteSpace(value) || value.Length <= maximumLength ? value : value[..maximumLength];

    private sealed class EnterpriseReferenceSourceBuilder
    {
        private readonly HashSet<string> sourceFiles = new(PathComparer);
        private readonly HashSet<string> customerHeaders = new(StringComparer.OrdinalIgnoreCase);

        public EnterpriseReferenceSourceBuilder(string name, string type)
        {
            Name = name;
            Type = type;
        }

        public string Name { get; }

        public string Type { get; }

        public int EstimatedCitizenCount { get; private set; }

        public int DiscoveredCustomerReferenceRows { get; private set; }

        public void AddSourceFile(string fileName)
            => sourceFiles.Add(fileName);

        public void RecordCustomerWorkbook(CustomerWorkbookInspection inspection)
        {
            if (inspection.RowCount > DiscoveredCustomerReferenceRows)
            {
                DiscoveredCustomerReferenceRows = inspection.RowCount;
            }

            if (inspection.RowCount > EstimatedCitizenCount)
            {
                EstimatedCitizenCount = inspection.RowCount;
            }

            foreach (var header in inspection.Headers)
            {
                customerHeaders.Add(header);
            }
        }

        public EnterpriseReferenceSource Build()
            => new(Name, Type, sourceFiles.OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToArray(), EstimatedCitizenCount, DiscoveredCustomerReferenceRows, customerHeaders.OrderBy(header => header, StringComparer.OrdinalIgnoreCase).ToArray());
    }

    private sealed record WorkbookMetadata(string? CompanyFileName, IReadOnlyList<WorksheetMetadata> Worksheets);

    private sealed record WorksheetMetadata(string Name, string EntryName);

    private sealed record CustomerWorkbookInspection(int RowCount, bool CanPopulateUtilityCustomers, IReadOnlyList<string> Headers);

    private sealed record CustomerWorkbookData(IReadOnlyList<string> Headers, IReadOnlyList<CustomerWorkbookRow> Rows);

    private static readonly CustomerWorkbookData EmptyCustomerWorkbookData = new([], []);

    private sealed record CustomerWorkbookRow(
        string DisplayName,
        string BillTo,
        string PrimaryContact,
        string MainPhone,
        string Fax,
        decimal BalanceTotal);

    private sealed record ParsedCustomerIdentity(string FirstName, string LastName, string? CompanyName, CustomerType CustomerType);

    private sealed record ParsedCustomerAddress(
        string ServiceAddress,
        string ServiceCity,
        string ServiceState,
        string ServiceZipCode,
        string? MailingAddress,
        string? MailingCity,
        string? MailingState,
        string? MailingZipCode,
        ServiceLocation ServiceLocation,
        bool UsedFallbackAddress);

    private sealed record PreparedUtilityCustomer(UtilityCustomer Customer, bool UsedFallbackAddress);

    private sealed record EnterpriseReferenceSource(
        string Name,
        string Type,
        IReadOnlyList<string> SourceFiles,
        int EstimatedCitizenCount,
        int DiscoveredCustomerReferenceRows,
        IReadOnlyList<string> CustomerWorkbookHeaders);

    private sealed record EnterpriseBaselineSeed(decimal CurrentRate, decimal MonthlyExpenses, int CitizenCount);

    private sealed record UtilityCustomerImportSummary(
        int ImportedCount,
        int InsertedCount,
        int UpdatedCount,
        int SkippedCount,
        int WorkbookCount,
        int RowCount,
        int StructuredAddressCount,
        int FallbackAddressCount,
        string Status);

    private sealed record LedgerImportSummary(int ImportedFileCount, int ImportedRowCount, string Status)
    {
        public static LedgerImportSummary NotRequested()
            => new(0, 0, "Sample ledger import was not requested.");
    }
}