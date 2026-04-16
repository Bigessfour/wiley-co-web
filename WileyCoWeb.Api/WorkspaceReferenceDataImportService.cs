using System.Globalization;
using System.IO.Compression;
using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using WileyCoWeb.Contracts;
using WileyWidget.Business.Interfaces;
using WileyWidget.Data;
using WileyWidget.Models;
using WileyWidget.Services;

namespace WileyCoWeb.Api;

internal sealed class WorkspaceReferenceDataImportService
{
    private static readonly Regex AccountCodeRegex = new(@"^\s*(?<code>\d+(?:\.\d+)?)\b", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex MultiWhitespaceRegex = new(@"\s+", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex CityStateZipRegex = new(@"^(?<street>.*?)(?<city>[A-Za-z][A-Za-z .'-]+),\s*(?<state>[A-Za-z]{2})\s+(?<zip>\d{5}(?:-\d{4})?)\s*$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex NonAlphaNumericRegex = new(@"[^A-Za-z0-9]+", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex PersonNameTokenRegex = new(@"^[A-Za-z][A-Za-z'.-]*$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly StringComparer PathComparer = StringComparer.OrdinalIgnoreCase;
    private static readonly XNamespace SpreadsheetNamespace = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private static readonly XNamespace RelationshipNamespace = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private static readonly string[] CorporateKeywords =
    [
        "LLC",
        "INC",
        "CORP",
        "COMPANY",
        "COBANK",
        "BANK",
        "TREE",
        "TRIMMING",
        "SERVICE",
        "SERVICES",
        "UTILITY",
        "DISTRICT",
        "TOWN OF",
        "CITY OF",
        "STATE OF",
        "COUNTY OF",
        "RANCH",
        "FARM",
        "SHOP",
        "STORE",
        "MARKET"
    ];
    private static readonly IReadOnlyDictionary<string, EnterpriseBaselineSeed> DefaultEnterpriseBaselines =
        new Dictionary<string, EnterpriseBaselineSeed>(StringComparer.OrdinalIgnoreCase)
        {
            ["Water Utility"] = new(31.25m, 98000m, 4500),
            ["Wiley Sanitation District"] = new(21.50m, 72000m, 3200),
            ["Sanitation Utility"] = new(21.50m, 72000m, 3200)
        };

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
        this.contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
        this.budgetRepository = budgetRepository ?? throw new ArgumentNullException(nameof(budgetRepository));
        this.quickBooksImportService = quickBooksImportService ?? throw new ArgumentNullException(nameof(quickBooksImportService));
        this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<WorkspaceReferenceDataImportResponse> ImportAsync(
        WorkspaceReferenceDataImportRequest? request,
        string contentRootPath,
        CancellationToken cancellationToken)
    {
        var resolvedImportPath = ResolveImportDataPath(request?.ImportDataPath, contentRootPath);
        if (!Directory.Exists(resolvedImportPath))
        {
            throw new DirectoryNotFoundException($"Import data folder '{resolvedImportPath}' was not found.");
        }

        var sources = DiscoverEnterpriseSources(resolvedImportPath);
        if (sources.Count == 0)
        {
            throw new InvalidOperationException($"No supported QuickBooks reference files were discovered in '{resolvedImportPath}'.");
        }

        var importedEnterpriseCount = 0;
        var updatedEnterpriseCount = 0;
        var seededEnterpriseBaselineCount = 0;
        var importedAtUtc = DateTime.UtcNow;
        var applyDefaultEnterpriseBaselines = request?.ApplyDefaultEnterpriseBaselines == true;

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        foreach (var source in sources.OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase))
        {
            var enterprise = await context.Enterprises
                .FirstOrDefaultAsync(item => item.Name == source.Name, cancellationToken)
                .ConfigureAwait(false);

            if (enterprise == null)
            {
                enterprise = new Enterprise
                {
                    Name = source.Name,
                    CurrentRate = 0m,
                    MonthlyExpenses = 0m,
                    CitizenCount = Math.Max(1, source.EstimatedCitizenCount),
                    CreatedDate = importedAtUtc,
                    ModifiedDate = importedAtUtc,
                    LastModified = importedAtUtc,
                    CreatedBy = nameof(WorkspaceReferenceDataImportService),
                    ModifiedBy = nameof(WorkspaceReferenceDataImportService),
                    IsDeleted = false
                };

                context.Enterprises.Add(enterprise);
                importedEnterpriseCount++;
            }
            else
            {
                updatedEnterpriseCount++;
                enterprise.ModifiedDate = importedAtUtc;
                enterprise.ModifiedBy = nameof(WorkspaceReferenceDataImportService);
                enterprise.LastModified = importedAtUtc;
                enterprise.IsDeleted = false;
                if (enterprise.CitizenCount <= 0)
                {
                    enterprise.CitizenCount = Math.Max(1, source.EstimatedCitizenCount);
                }
            }

            enterprise.Type = source.Type;
            enterprise.Description = Truncate($"Imported from QuickBooks reference data in {resolvedImportPath}.", 500);
            enterprise.Notes = Truncate(BuildEnterpriseNotes(source), 500);

            if (applyDefaultEnterpriseBaselines && ApplyDefaultBaseline(enterprise))
            {
                seededEnterpriseBaselineCount++;
            }
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var utilityCustomerImportSummary = await ImportUtilityCustomersAsync(context, resolvedImportPath, importedAtUtc, cancellationToken).ConfigureAwait(false);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var ledgerImportSummary = request?.IncludeSampleLedgerData == true
            ? await ImportSampleLedgerDataAsync(resolvedImportPath, cancellationToken).ConfigureAwait(false)
            : LedgerImportSummary.NotRequested();

        var discoveredCustomerRows = sources.Sum(item => item.DiscoveredCustomerReferenceRows);
        var utilityCustomerImportStatus = utilityCustomerImportSummary.Status;

        logger.LogInformation(
            "Imported workspace reference data from {ImportPath}: imported={Imported}, updated={Updated}, customerRows={CustomerRows}, importedCustomers={ImportedCustomers}, ledgerFiles={LedgerFiles}, ledgerRows={LedgerRows}, baselines={BaselineCount}",
            resolvedImportPath,
            importedEnterpriseCount,
            updatedEnterpriseCount,
            discoveredCustomerRows,
            utilityCustomerImportSummary.ImportedCount,
            ledgerImportSummary.ImportedFileCount,
            ledgerImportSummary.ImportedRowCount,
            seededEnterpriseBaselineCount);

        return new WorkspaceReferenceDataImportResponse(
            resolvedImportPath,
            importedAtUtc.ToString("O", CultureInfo.InvariantCulture),
            importedEnterpriseCount,
            updatedEnterpriseCount,
            discoveredCustomerRows,
            utilityCustomerImportSummary.ImportedCount,
            utilityCustomerImportStatus,
            sources.Select(item => item.Name).OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToArray(),
            ledgerImportSummary.ImportedFileCount,
            ledgerImportSummary.ImportedRowCount,
            ledgerImportSummary.Status,
            seededEnterpriseBaselineCount);
    }

    private async Task<UtilityCustomerImportSummary> ImportUtilityCustomersAsync(
        AppDbContext context,
        string importDataPath,
        DateTime importedAtUtc,
        CancellationToken cancellationToken)
    {
        var customerWorkbooks = Directory.EnumerateFiles(importDataPath, "*", SearchOption.TopDirectoryOnly)
            .Where(path => Path.GetExtension(path).Equals(".xlsx", StringComparison.OrdinalIgnoreCase))
            .Where(path => IsCustomerWorkbook(Path.GetFileName(path)))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (customerWorkbooks.Count == 0)
        {
            return new UtilityCustomerImportSummary(
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                "No QuickBooks customer workbook was found. UtilityCustomers were left unchanged.");
        }

        var customersByAccountNumber = await context.UtilityCustomers
            .ToDictionaryAsync(customer => customer.AccountNumber, StringComparer.OrdinalIgnoreCase, cancellationToken)
            .ConfigureAwait(false);

        var synchronizedAccountNumbers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var workbookCount = 0;
        var rowCount = 0;
        var insertedCount = 0;
        var updatedCount = 0;
        var skippedCount = 0;
        var structuredAddressCount = 0;
        var fallbackAddressCount = 0;

        foreach (var filePath in customerWorkbooks)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var workbookMetadata = TryReadWorkbookMetadata(filePath);
            if (workbookMetadata is null)
            {
                logger.LogWarning("Skipping QuickBooks customer import for {FileName} because the workbook metadata could not be read.", Path.GetFileName(filePath));
                continue;
            }

            var workbookData = ReadCustomerWorkbookData(filePath, workbookMetadata);
            if (workbookData.Rows.Count == 0)
            {
                continue;
            }

            workbookCount++;
            rowCount += workbookData.Rows.Count;

            var enterpriseName = ResolveEnterpriseName(workbookMetadata.CompanyFileName, Path.GetFileName(filePath)) ?? "Water Utility";

            foreach (var row in workbookData.Rows)
            {
                var preparedCustomer = PrepareUtilityCustomer(filePath, enterpriseName, row, importedAtUtc);
                if (preparedCustomer is null)
                {
                    skippedCount++;
                    continue;
                }

                if (!synchronizedAccountNumbers.Add(preparedCustomer.Customer.AccountNumber))
                {
                    continue;
                }

                if (preparedCustomer.UsedFallbackAddress)
                {
                    fallbackAddressCount++;
                }
                else
                {
                    structuredAddressCount++;
                }

                if (customersByAccountNumber.TryGetValue(preparedCustomer.Customer.AccountNumber, out var existingCustomer))
                {
                    CopyImportedCustomer(preparedCustomer.Customer, existingCustomer, importedAtUtc);
                    updatedCount++;
                }
                else
                {
                    context.UtilityCustomers.Add(preparedCustomer.Customer);
                    customersByAccountNumber.Add(preparedCustomer.Customer.AccountNumber, preparedCustomer.Customer);
                    insertedCount++;
                }
            }
        }

        var importedCount = insertedCount + updatedCount;
        return new UtilityCustomerImportSummary(
            importedCount,
            insertedCount,
            updatedCount,
            skippedCount,
            workbookCount,
            rowCount,
            structuredAddressCount,
            fallbackAddressCount,
            BuildUtilityCustomerImportStatus(importedCount, insertedCount, updatedCount, skippedCount, workbookCount, rowCount, structuredAddressCount, fallbackAddressCount));
    }

    private async Task<LedgerImportSummary> ImportSampleLedgerDataAsync(string importDataPath, CancellationToken cancellationToken)
    {
        var ledgerFiles = Directory.EnumerateFiles(importDataPath, "*", SearchOption.TopDirectoryOnly)
            .Where(IsSampleLedgerFile)
            .GroupBy(path => Path.GetFileNameWithoutExtension(Path.GetFileName(path)), StringComparer.OrdinalIgnoreCase)
            .Select(group => group
                .OrderByDescending(path => GetSampleLedgerFilePreference(path))
                .ThenBy(path => path, StringComparer.OrdinalIgnoreCase)
                .First())
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (ledgerFiles.Count == 0)
        {
            return new LedgerImportSummary(0, 0, "No sample QuickBooks ledger files were found in the import folder.");
        }

        var importedFileCount = 0;
        var importedRowCount = 0;
        var duplicateFileCount = 0;
        var fiscalYears = new HashSet<int>();

        foreach (var filePath in ledgerFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var fileName = Path.GetFileName(filePath);
            var fiscalYear = ResolveFiscalYear(fileName);
            fiscalYears.Add(fiscalYear);
            var enterpriseName = ResolveEnterpriseName(TryReadWorkbookMetadata(filePath)?.CompanyFileName, fileName);
            if (string.IsNullOrWhiteSpace(enterpriseName))
            {
                logger.LogWarning("Skipping sample QuickBooks ledger import for {FileName} because no enterprise mapping could be resolved.", fileName);
                continue;
            }

            var fileBytes = await File.ReadAllBytesAsync(filePath, cancellationToken).ConfigureAwait(false);
            var commitResult = await quickBooksImportService.CommitAsync(
                fileBytes,
                fileName,
                enterpriseName,
                fiscalYear,
                cancellationToken).ConfigureAwait(false);

            if (commitResult.IsDuplicate)
            {
                duplicateFileCount++;
                continue;
            }

            importedFileCount++;
            importedRowCount += commitResult.ImportedRows;
        }

        var updatedBudgetRowCount = 0;
        foreach (var fiscalYear in fiscalYears.OrderBy(year => year))
        {
            updatedBudgetRowCount += await RefreshBudgetActualsFromImportedLedgersAsync(fiscalYear, cancellationToken).ConfigureAwait(false);
        }

        var status = importedFileCount > 0
            ? $"Imported {importedRowCount} QuickBooks ledger rows from {importedFileCount} sample file(s) and refreshed {updatedBudgetRowCount} budget actual row(s)."
            : duplicateFileCount > 0
                ? $"Skipped {duplicateFileCount} duplicate QuickBooks sample ledger file(s) and refreshed {updatedBudgetRowCount} budget actual row(s)."
                : "No sample QuickBooks ledger rows were imported.";

        return new LedgerImportSummary(importedFileCount, importedRowCount, status);
    }

    private async Task<int> RefreshBudgetActualsFromImportedLedgersAsync(int fiscalYear, CancellationToken cancellationToken)
    {
        var budgetEntries = (await budgetRepository.GetByFiscalYearAsync(fiscalYear, cancellationToken).ConfigureAwait(false))
            .Where(entry => !string.IsNullOrWhiteSpace(entry.AccountNumber))
            .ToList();

        if (budgetEntries.Count == 0)
        {
            return 0;
        }

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var ledgerRows = await context.LedgerEntries
            .AsNoTracking()
            .Select(entry => new
            {
                entry.AccountName,
                Amount = entry.Amount ?? 0m,
                OriginalFileName = entry.SourceFile.OriginalFileName
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var actualsByNormalizedCode = ledgerRows
            .Where(row => LooksLikeGeneralLedgerFile(row.OriginalFileName))
            .Where(row => ResolveFiscalYear(row.OriginalFileName) == fiscalYear)
            .Select(row => new
            {
                NormalizedCode = NormalizeAccountNumber(ExtractAccountCode(row.AccountName)),
                row.Amount
            })
            .Where(row => row.NormalizedCode != null)
            .GroupBy(row => row.NormalizedCode!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => Math.Abs(group.Sum(row => row.Amount)), StringComparer.OrdinalIgnoreCase);

        if (actualsByNormalizedCode.Count == 0)
        {
            logger.LogInformation("No imported general-ledger actuals matched FY {FiscalYear} budget rows.", fiscalYear);
            return 0;
        }

        var actualsByBudgetAccount = budgetEntries
            .Select(entry => new
            {
                entry.AccountNumber,
                NormalizedCode = NormalizeAccountNumber(entry.AccountNumber)
            })
            .Where(entry => entry.NormalizedCode != null && actualsByNormalizedCode.ContainsKey(entry.NormalizedCode))
            .ToDictionary(
                entry => entry.AccountNumber!,
                entry => actualsByNormalizedCode[entry.NormalizedCode!],
                StringComparer.OrdinalIgnoreCase);

        if (actualsByBudgetAccount.Count == 0)
        {
            logger.LogInformation("No imported general-ledger actuals aligned to exact FY {FiscalYear} budget accounts.", fiscalYear);
            return 0;
        }

        var updatedRows = await budgetRepository.BulkUpdateActualsAsync(actualsByBudgetAccount, fiscalYear, cancellationToken).ConfigureAwait(false);
        logger.LogInformation("Refreshed {UpdatedRows} budget actual row(s) from imported general-ledger samples for FY {FiscalYear}.", updatedRows, fiscalYear);
        return updatedRows;
    }

    private static string? ExtractAccountCode(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var match = AccountCodeRegex.Match(value);
        return match.Success ? match.Groups["code"].Value : null;
    }

    private static string? NormalizeAccountNumber(string? accountNumber)
    {
        if (string.IsNullOrWhiteSpace(accountNumber))
        {
            return null;
        }

        var trimmed = accountNumber.Trim();
        var separatorIndex = trimmed.IndexOf('.');
        if (separatorIndex < 0)
        {
            return trimmed;
        }

        var wholePart = trimmed[..separatorIndex];
        var fractionalPart = trimmed[(separatorIndex + 1)..].TrimEnd('0');
        return fractionalPart.Length == 0 ? wholePart : $"{wholePart}.{fractionalPart}";
    }

    private static bool LooksLikeGeneralLedgerFile(string? fileName)
        => !string.IsNullOrWhiteSpace(fileName)
            && fileName.Contains("GeneralLedger", StringComparison.OrdinalIgnoreCase);

    private static int GetSampleLedgerFilePreference(string filePath)
    {
        var extension = Path.GetExtension(filePath);
        return extension.ToLowerInvariant() switch
        {
            ".xlsx" => 2,
            ".xls" => 1,
            _ => 0
        };
    }

    private static List<EnterpriseReferenceSource> DiscoverEnterpriseSources(string importDataPath)
    {
        var files = Directory.EnumerateFiles(importDataPath, "*", SearchOption.TopDirectoryOnly)
            .Where(path =>
            {
                var extension = Path.GetExtension(path);
                return extension.Equals(".xlsx", StringComparison.OrdinalIgnoreCase)
                    || extension.Equals(".csv", StringComparison.OrdinalIgnoreCase);
            })
            .ToList();

        var groupedSources = new Dictionary<string, EnterpriseReferenceSourceBuilder>(StringComparer.OrdinalIgnoreCase);

        foreach (var filePath in files)
        {
            var fileName = Path.GetFileName(filePath);
            var workbookMetadata = TryReadWorkbookMetadata(filePath);
            var enterpriseName = ResolveEnterpriseName(workbookMetadata?.CompanyFileName, fileName);
            if (string.IsNullOrWhiteSpace(enterpriseName))
            {
                continue;
            }

            if (!groupedSources.TryGetValue(enterpriseName, out var builder))
            {
                builder = new EnterpriseReferenceSourceBuilder(enterpriseName, DeriveEnterpriseType(enterpriseName));
                groupedSources.Add(enterpriseName, builder);
            }

            builder.AddSourceFile(fileName);

            if (IsCustomerWorkbook(fileName) && workbookMetadata is not null)
            {
                var inspection = InspectCustomerWorkbook(filePath, workbookMetadata);
                builder.RecordCustomerWorkbook(inspection);
            }
        }

        return groupedSources.Values.Select(builder => builder.Build()).ToList();
    }

    private string ResolveImportDataPath(string? requestedPath, string contentRootPath)
    {
        var configuredDefaultPath = configuration["WorkspaceReferenceData:DefaultImportDataPath"];
        var requireExplicitImportDataPath = configuration.GetValue<bool>("WorkspaceReferenceData:RequireExplicitImportDataPath");

        if (string.IsNullOrWhiteSpace(requestedPath) && !string.IsNullOrWhiteSpace(configuredDefaultPath))
        {
            requestedPath = configuredDefaultPath;
        }

        if (string.IsNullOrWhiteSpace(requestedPath) && requireExplicitImportDataPath)
        {
            throw new InvalidOperationException("Workspace reference-data import requires an explicit importDataPath or WorkspaceReferenceData:DefaultImportDataPath. Production containers do not assume a bundled Import Data folder.");
        }

        var candidates = BuildImportDataPathCandidates(requestedPath, contentRootPath);

        foreach (var candidate in candidates)
        {
            var fullPath = Path.GetFullPath(candidate);
            if (Directory.Exists(fullPath))
            {
                return fullPath;
            }
        }

        return Path.GetFullPath(candidates[0]);
    }

    private static List<string> BuildImportDataPathCandidates(string? requestedPath, string contentRootPath)
    {
        var candidates = new List<string>();
        if (!string.IsNullOrWhiteSpace(requestedPath))
        {
            if (Path.IsPathRooted(requestedPath))
            {
                candidates.Add(requestedPath);
            }
            else
            {
                candidates.Add(Path.Combine(contentRootPath, requestedPath));
                candidates.Add(Path.Combine(Directory.GetCurrentDirectory(), requestedPath));
                candidates.Add(Path.Combine(contentRootPath, "..", requestedPath));
            }

            return candidates;
        }

        candidates.Add(Path.Combine(contentRootPath, "Import Data"));
        candidates.Add(Path.Combine(contentRootPath, "..", "Import Data"));
        candidates.Add(Path.Combine(Directory.GetCurrentDirectory(), "Import Data"));
        return candidates;
    }

    private static WorkbookMetadata? TryReadWorkbookMetadata(string filePath)
    {
        if (!Path.GetExtension(filePath).Equals(".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        try
        {
            using var archive = ZipFile.OpenRead(filePath);
            var workbookEntry = archive.GetEntry("xl/workbook.xml");
            if (workbookEntry == null)
            {
                return null;
            }

            var workbookDocument = XDocument.Load(workbookEntry.Open());
            var relationshipsEntry = archive.GetEntry("xl/_rels/workbook.xml.rels");
            var relationshipsDocument = relationshipsEntry == null ? null : XDocument.Load(relationshipsEntry.Open());

            var relationshipTargets = relationshipsDocument?.Root?
                .Elements()
                .Where(element => string.Equals(element.Name.LocalName, "Relationship", StringComparison.Ordinal))
                .ToDictionary(
                    element => (string?)element.Attribute("Id") ?? string.Empty,
                    element => (string?)element.Attribute("Target") ?? string.Empty,
                    StringComparer.OrdinalIgnoreCase)
                ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            var worksheets = workbookDocument.Root?
                .Descendants(SpreadsheetNamespace + "sheet")
                .Select(sheet =>
                {
                    var relationshipId = (string?)sheet.Attribute(RelationshipNamespace + "id") ?? string.Empty;
                    relationshipTargets.TryGetValue(relationshipId, out var target);
                    return new WorksheetMetadata(
                        (string?)sheet.Attribute("name") ?? string.Empty,
                        NormalizeWorksheetTarget(target));
                })
                .Where(sheet => !string.IsNullOrWhiteSpace(sheet.EntryName))
                .ToList()
                ?? [];

            var companyFileName = workbookDocument.Root?
                .Descendants(SpreadsheetNamespace + "definedName")
                .FirstOrDefault(element => string.Equals((string?)element.Attribute("name"), "QBCOMPANYFILENAME", StringComparison.OrdinalIgnoreCase))?
                .Value
                .Trim()
                .Trim('"');

            return new WorkbookMetadata(companyFileName, worksheets);
        }
        catch (InvalidDataException)
        {
            return null;
        }
    }

    private static CustomerWorkbookInspection InspectCustomerWorkbook(string filePath, WorkbookMetadata workbookMetadata)
    {
        var workbookData = ReadCustomerWorkbookData(filePath, workbookMetadata);
        return new CustomerWorkbookInspection(
            workbookData.Rows.Count,
            workbookData.Rows.Count > 0,
            workbookData.Headers);
    }

    private static CustomerWorkbookData ReadCustomerWorkbookData(string filePath, WorkbookMetadata workbookMetadata)
    {
        var worksheet = workbookMetadata.Worksheets.FirstOrDefault(sheet => !sheet.Name.Contains("Tips", StringComparison.OrdinalIgnoreCase));
        if (worksheet == null)
        {
            return new CustomerWorkbookData([], []);
        }

        using var archive = ZipFile.OpenRead(filePath);
        var worksheetEntry = archive.GetEntry(worksheet.EntryName);
        if (worksheetEntry == null)
        {
            return new CustomerWorkbookData([], []);
        }

        var sharedStrings = ReadSharedStrings(archive);
        var worksheetDocument = XDocument.Load(worksheetEntry.Open());
        var rows = worksheetDocument.Root?
            .Descendants(SpreadsheetNamespace + "row")
            .Select(row => ReadWorksheetRow(row, sharedStrings))
            .Where(values => values.Count > 0)
            .ToList()
            ?? [];

        if (rows.Count == 0)
        {
            return new CustomerWorkbookData([], []);
        }

        var headerRow = rows[0];
        var headers = headerRow.Values
            .Select(value => NormalizeWhitespace(value))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToArray();

        var normalizedHeaderColumns = headerRow
            .Where(entry => !string.IsNullOrWhiteSpace(entry.Value))
            .GroupBy(entry => NormalizeHeader(entry.Value), StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First().Key, StringComparer.Ordinal);

        var workbookRows = new List<CustomerWorkbookRow>();
        foreach (var row in rows.Skip(1))
        {
            var displayName = GetWorkbookValue(row, normalizedHeaderColumns, "customer", "C");
            if (string.IsNullOrWhiteSpace(displayName))
            {
                continue;
            }

            workbookRows.Add(new CustomerWorkbookRow(
                displayName,
                GetWorkbookValue(row, normalizedHeaderColumns, "billto", "E"),
                GetWorkbookValue(row, normalizedHeaderColumns, "primarycontact", "G"),
                GetWorkbookValue(row, normalizedHeaderColumns, "mainphone", "I"),
                GetWorkbookValue(row, normalizedHeaderColumns, "fax", "K"),
                ParseWorkbookDecimal(GetWorkbookValue(row, normalizedHeaderColumns, "balancetotal", "M"))));
        }

        return new CustomerWorkbookData(headers, workbookRows);
    }

    private static List<string> ReadSharedStrings(ZipArchive archive)
    {
        var sharedStringsEntry = archive.GetEntry("xl/sharedStrings.xml");
        if (sharedStringsEntry == null)
        {
            return [];
        }

        var sharedStringsDocument = XDocument.Load(sharedStringsEntry.Open());
        return sharedStringsDocument.Root?
            .Descendants(SpreadsheetNamespace + "si")
            .Select(item => string.Concat(item.Descendants(SpreadsheetNamespace + "t").Select(textNode => textNode.Value)))
            .ToList()
            ?? [];
    }

    private static string ReadCellValue(XElement cell, IReadOnlyList<string> sharedStrings)
    {
        var dataType = (string?)cell.Attribute("t");
        if (string.Equals(dataType, "inlineStr", StringComparison.OrdinalIgnoreCase))
        {
            return string.Concat(cell.Descendants(SpreadsheetNamespace + "t").Select(node => node.Value));
        }

        var rawValue = cell.Element(SpreadsheetNamespace + "v")?.Value ?? string.Empty;
        if (string.Equals(dataType, "s", StringComparison.OrdinalIgnoreCase) && int.TryParse(rawValue, out var sharedStringIndex))
        {
            if (sharedStringIndex >= 0 && sharedStringIndex < sharedStrings.Count)
            {
                return sharedStrings[sharedStringIndex];
            }
        }

        return rawValue;
    }

    private static Dictionary<string, string> ReadWorksheetRow(XElement row, IReadOnlyList<string> sharedStrings)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var cell in row.Elements(SpreadsheetNamespace + "c"))
        {
            var reference = (string?)cell.Attribute("r");
            if (string.IsNullOrWhiteSpace(reference))
            {
                continue;
            }

            var columnName = GetColumnName(reference);
            if (string.IsNullOrWhiteSpace(columnName))
            {
                continue;
            }

            var value = NormalizeWhitespace(ReadCellValue(cell, sharedStrings));
            if (!string.IsNullOrWhiteSpace(value))
            {
                values[columnName] = value;
            }
        }

        return values;
    }

    private static string GetColumnName(string cellReference)
        => new(cellReference.TakeWhile(char.IsLetter).ToArray());

    private static string GetWorkbookValue(
        IReadOnlyDictionary<string, string> row,
        IReadOnlyDictionary<string, string> normalizedHeaderColumns,
        string normalizedHeader,
        string fallbackColumn)
    {
        if (normalizedHeaderColumns.TryGetValue(normalizedHeader, out var columnName)
            && row.TryGetValue(columnName, out var headerValue))
        {
            return headerValue;
        }

        return row.TryGetValue(fallbackColumn, out var fallbackValue)
            ? fallbackValue
            : string.Empty;
    }

    private static string NormalizeHeader(string value)
        => NonAlphaNumericRegex.Replace(value, string.Empty).ToLowerInvariant();

    private static decimal ParseWorkbookDecimal(string value)
    {
        return decimal.TryParse(
            value,
            NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign | NumberStyles.AllowThousands,
            CultureInfo.InvariantCulture,
            out var parsed)
            ? parsed
            : 0m;
    }

    private static string NormalizeWorksheetTarget(string? target)
    {
        if (string.IsNullOrWhiteSpace(target))
        {
            return string.Empty;
        }

        var normalizedTarget = target.Replace('\\', '/');
        return normalizedTarget.StartsWith("xl/", StringComparison.OrdinalIgnoreCase)
            ? normalizedTarget
            : $"xl/{normalizedTarget.TrimStart('/')}";
    }

    private static string? ResolveEnterpriseName(string? companyFileName, string fileName)
    {
        var normalizedCompanyName = NormalizeCompanyName(companyFileName);
        if (!string.IsNullOrWhiteSpace(normalizedCompanyName))
        {
            return normalizedCompanyName;
        }

        if (fileName.Contains("WSD", StringComparison.OrdinalIgnoreCase)
            || fileName.Contains("sanitation", StringComparison.OrdinalIgnoreCase))
        {
            return "Wiley Sanitation District";
        }

        if (fileName.Contains("Util", StringComparison.OrdinalIgnoreCase)
            || fileName.Contains("utility", StringComparison.OrdinalIgnoreCase))
        {
            return "Water Utility";
        }

        return null;
    }

    private static string? NormalizeCompanyName(string? companyFileName)
    {
        if (string.IsNullOrWhiteSpace(companyFileName))
        {
            return null;
        }

        var rawName = Path.GetFileNameWithoutExtension(companyFileName.Trim());
        rawName = rawName.Replace('_', ' ').Trim();
        rawName = System.Text.RegularExpressions.Regex.Replace(rawName, "\\s+\\d{2,4}$", string.Empty).Trim();

        if (rawName.Contains("sanitation", StringComparison.OrdinalIgnoreCase)
            || rawName.Contains("sewer", StringComparison.OrdinalIgnoreCase)
            || rawName.Contains("wsd", StringComparison.OrdinalIgnoreCase))
        {
            return "Wiley Sanitation District";
        }

        if (rawName.Contains("utility", StringComparison.OrdinalIgnoreCase)
            || rawName.Contains("tow utility account", StringComparison.OrdinalIgnoreCase)
            || rawName.Contains("town utility account", StringComparison.OrdinalIgnoreCase))
        {
            return "Water Utility";
        }

        return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(rawName.ToLowerInvariant());
    }

    private static string DeriveEnterpriseType(string enterpriseName)
    {
        if (enterpriseName.Contains("sanitation", StringComparison.OrdinalIgnoreCase)
            || enterpriseName.Contains("sewer", StringComparison.OrdinalIgnoreCase))
        {
            return "Sewer";
        }

        if (enterpriseName.Contains("trash", StringComparison.OrdinalIgnoreCase))
        {
            return "Trash";
        }

        return "Water";
    }

    private static bool IsCustomerWorkbook(string fileName)
        => fileName.Contains("Customer", StringComparison.OrdinalIgnoreCase);

    private static bool IsSampleLedgerFile(string filePath)
    {
        var fileName = Path.GetFileName(filePath);
        var extension = Path.GetExtension(fileName);
        var isSupportedExtension = extension.Equals(".csv", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".xlsx", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".xls", StringComparison.OrdinalIgnoreCase);

        return isSupportedExtension
            && !IsCustomerWorkbook(fileName)
            && fileName.Contains("GeneralLedger", StringComparison.OrdinalIgnoreCase);
    }

    private static int ResolveFiscalYear(string fileName)
    {
        var match = System.Text.RegularExpressions.Regex.Match(fileName, @"FY(?<year>\d{2,4})", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (match.Success && int.TryParse(match.Groups["year"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var fiscalYear))
        {
            return fiscalYear < 100 ? 2000 + fiscalYear : fiscalYear;
        }

        return DateTime.UtcNow.Year;
    }

    private static bool ApplyDefaultBaseline(Enterprise enterprise)
    {
        if (!DefaultEnterpriseBaselines.TryGetValue(enterprise.Name, out var baseline))
        {
            return false;
        }

        var updated = false;

        if (enterprise.CurrentRate <= 0m)
        {
            enterprise.CurrentRate = baseline.CurrentRate;
            updated = true;
        }

        if (enterprise.MonthlyExpenses <= 0m)
        {
            enterprise.MonthlyExpenses = baseline.MonthlyExpenses;
            updated = true;
        }

        if (enterprise.CitizenCount < baseline.CitizenCount)
        {
            enterprise.CitizenCount = baseline.CitizenCount;
            updated = true;
        }

        return updated;
    }

    private static string BuildEnterpriseNotes(EnterpriseReferenceSource source)
    {
        var fileSummary = string.Join(", ", source.SourceFiles.OrderBy(name => name, StringComparer.OrdinalIgnoreCase).Take(6));
        if (source.SourceFiles.Count > 6)
        {
            fileSummary = $"{fileSummary}, +{source.SourceFiles.Count - 6} more";
        }

        var customerNote = source.DiscoveredCustomerReferenceRows > 0
            ? $"Detected {source.DiscoveredCustomerReferenceRows} QuickBooks customer reference rows for this enterprise. UtilityCustomer records can be synthesized from the customer name and bill-to fields when structured utility account data is missing."
            : "No QuickBooks utility-customer roster was imported for this enterprise.";

        return $"Source files: {fileSummary}. {customerNote}";
    }

    private PreparedUtilityCustomer? PrepareUtilityCustomer(string filePath, string enterpriseName, CustomerWorkbookRow row, DateTime importedAtUtc)
    {
        var displayName = NormalizeWhitespace(row.DisplayName);
        if (string.IsNullOrWhiteSpace(displayName))
        {
            return null;
        }

        var identity = ResolveCustomerIdentity(displayName, row.PrimaryContact);
        var address = ResolveCustomerAddress(displayName, row.BillTo);
        var accountNumber = BuildSyntheticAccountNumber(enterpriseName, displayName, row.BillTo);
        var customer = new UtilityCustomer
        {
            AccountNumber = accountNumber,
            FirstName = Truncate(identity.FirstName, 50) ?? "Imported",
            LastName = Truncate(identity.LastName, 50) ?? "Customer",
            CompanyName = Truncate(identity.CompanyName, 100),
            CustomerType = identity.CustomerType,
            ServiceAddress = Truncate(address.ServiceAddress, 200) ?? "Imported customer reference",
            ServiceCity = Truncate(address.ServiceCity, 50) ?? "Wiley",
            ServiceState = Truncate(address.ServiceState, 2) ?? "CO",
            ServiceZipCode = Truncate(address.ServiceZipCode, 10) ?? "81092",
            MailingAddress = Truncate(address.MailingAddress, 200),
            MailingCity = Truncate(address.MailingCity, 50),
            MailingState = Truncate(address.MailingState, 2),
            MailingZipCode = Truncate(address.MailingZipCode, 10),
            PhoneNumber = NormalizePhoneNumber(row.MainPhone),
            ServiceLocation = address.ServiceLocation,
            Status = CustomerStatus.Active,
            AccountOpenDate = importedAtUtc.Date,
            CurrentBalance = row.BalanceTotal,
            Notes = BuildCustomerImportNotes(Path.GetFileName(filePath), enterpriseName, row, address.UsedFallbackAddress),
            CreatedDate = importedAtUtc,
            LastModifiedDate = importedAtUtc
        };

        if (!TryValidateUtilityCustomer(customer, out var validationErrors))
        {
            logger.LogWarning(
                "Skipping imported QuickBooks customer row '{CustomerName}' from {FileName} because the synthesized UtilityCustomer record was invalid: {ValidationErrors}",
                displayName,
                Path.GetFileName(filePath),
                validationErrors);
            return null;
        }

        return new PreparedUtilityCustomer(customer, address.UsedFallbackAddress);
    }

    private static ParsedCustomerIdentity ResolveCustomerIdentity(string displayName, string primaryContact)
    {
        if (LooksLikeIndividualName(displayName))
        {
            var (firstName, lastName) = SplitPersonName(displayName);
            return new ParsedCustomerIdentity(firstName, lastName, null, CustomerType.Residential);
        }

        var customerType = ResolveCustomerType(displayName);
        if (LooksLikeIndividualName(primaryContact))
        {
            var (firstName, lastName) = SplitPersonName(primaryContact);
            return new ParsedCustomerIdentity(firstName, lastName, displayName, customerType);
        }

        return new ParsedCustomerIdentity("Imported", "Customer", displayName, customerType);
    }

    private static CustomerType ResolveCustomerType(string displayName)
    {
        if (displayName.Contains("TOWN OF", StringComparison.OrdinalIgnoreCase)
            || displayName.Contains("CITY OF", StringComparison.OrdinalIgnoreCase)
            || displayName.Contains("STATE OF", StringComparison.OrdinalIgnoreCase)
            || displayName.Contains("COUNTY OF", StringComparison.OrdinalIgnoreCase)
            || displayName.Contains("DISTRICT", StringComparison.OrdinalIgnoreCase))
        {
            return CustomerType.Government;
        }

        if (displayName.Contains("SCHOOL", StringComparison.OrdinalIgnoreCase)
            || displayName.Contains("CHURCH", StringComparison.OrdinalIgnoreCase)
            || displayName.Contains("HOSPITAL", StringComparison.OrdinalIgnoreCase)
            || displayName.Contains("LIBRARY", StringComparison.OrdinalIgnoreCase))
        {
            return CustomerType.Institutional;
        }

        if (displayName.Contains("APARTMENT", StringComparison.OrdinalIgnoreCase)
            || displayName.Contains("APTS", StringComparison.OrdinalIgnoreCase)
            || displayName.Contains("MOBILE HOME", StringComparison.OrdinalIgnoreCase)
            || displayName.Contains("TRAILER PARK", StringComparison.OrdinalIgnoreCase))
        {
            return CustomerType.MultiFamily;
        }

        return LooksLikeIndividualName(displayName)
            ? CustomerType.Residential
            : CustomerType.Commercial;
    }

    private static bool LooksLikeIndividualName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var normalized = NormalizeWhitespace(value);
        if (normalized.IndexOfAny([',', '&', '/', '\\', '(', ')']) >= 0)
        {
            return false;
        }

        if (CorporateKeywords.Any(keyword => normalized.Contains(keyword, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        var tokens = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (tokens.Length < 2 || tokens.Length > 4)
        {
            return false;
        }

        return tokens.All(token => PersonNameTokenRegex.IsMatch(token));
    }

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
        var normalizedBillTo = NormalizeWhitespace(billTo);
        var trimmedBillTo = RemoveLeadingDisplayName(normalizedBillTo, displayName);

        if (TryParseStructuredAddress(trimmedBillTo, normalizedBillTo, out var structuredAddress)
            || TryParseStructuredAddress(normalizedBillTo, normalizedBillTo, out structuredAddress))
        {
            return structuredAddress;
        }

        var fallbackAddress = !string.IsNullOrWhiteSpace(trimmedBillTo)
            && !string.Equals(trimmedBillTo, displayName, StringComparison.OrdinalIgnoreCase)
            ? trimmedBillTo
            : "Imported customer reference";

        return new ParsedCustomerAddress(
            fallbackAddress,
            "Wiley",
            "CO",
            "81092",
            string.IsNullOrWhiteSpace(normalizedBillTo) ? null : normalizedBillTo,
            null,
            null,
            null,
            ServiceLocation.InsideCityLimits,
            true);
    }

    private static bool TryParseStructuredAddress(string candidate, string originalBillTo, out ParsedCustomerAddress address)
    {
        address = default!;
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return false;
        }

        var match = CityStateZipRegex.Match(candidate);
        if (!match.Success)
        {
            return false;
        }

        var street = NormalizeWhitespace(match.Groups["street"].Value.Trim(',', ' '));
        var city = NormalizeWhitespace(match.Groups["city"].Value);
        var state = match.Groups["state"].Value.ToUpperInvariant();
        var zip = match.Groups["zip"].Value;
        if (string.IsNullOrWhiteSpace(street) || string.IsNullOrWhiteSpace(city))
        {
            return false;
        }

        address = new ParsedCustomerAddress(
            street,
            city,
            state,
            zip,
            street,
            city,
            state,
            zip,
            city.Contains("Wiley", StringComparison.OrdinalIgnoreCase)
                ? ServiceLocation.InsideCityLimits
                : ServiceLocation.OutsideCityLimits,
            false);
        return true;
    }

    private static string RemoveLeadingDisplayName(string billTo, string displayName)
    {
        if (string.IsNullOrWhiteSpace(billTo) || string.IsNullOrWhiteSpace(displayName))
        {
            return billTo;
        }

        return billTo.StartsWith(displayName, StringComparison.OrdinalIgnoreCase)
            ? billTo[displayName.Length..].TrimStart(',', ' ')
            : billTo;
    }

    private static string BuildSyntheticAccountNumber(string enterpriseName, string displayName, string billTo)
    {
        var normalizedSource = $"{ResolveEnterpriseCode(enterpriseName)}|{NormalizeWhitespace(displayName)}|{NormalizeWhitespace(billTo)}";
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalizedSource)));
        return $"QB-{ResolveEnterpriseCode(enterpriseName)}-{hash[..10]}";
    }

    private static string ResolveEnterpriseCode(string enterpriseName)
    {
        if (enterpriseName.Contains("Sanitation", StringComparison.OrdinalIgnoreCase)
            || enterpriseName.Contains("District", StringComparison.OrdinalIgnoreCase))
        {
            return "WSD";
        }

        if (enterpriseName.Contains("Water", StringComparison.OrdinalIgnoreCase)
            || enterpriseName.Contains("Utility", StringComparison.OrdinalIgnoreCase))
        {
            return "WTR";
        }

        var normalized = new string(enterpriseName.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
        if (normalized.Length >= 3)
        {
            return normalized[..3];
        }

        return normalized.PadRight(3, 'X');
    }

    private static string? NormalizePhoneNumber(string value)
    {
        var digits = new string(value.Where(char.IsDigit).ToArray());
        return digits.Length switch
        {
            7 => $"{digits[..3]}-{digits[3..]}",
            10 => $"({digits[..3]}) {digits[3..6]}-{digits[6..]}",
            11 when digits[0] == '1' => $"+1 {digits[1..4]}-{digits[4..7]}-{digits[7..]}",
            _ => null
        };
    }

    private static string BuildCustomerImportNotes(string fileName, string enterpriseName, CustomerWorkbookRow row, bool usedFallbackAddress)
    {
        var notes = new List<string>
        {
            $"Imported from QuickBooks customer workbook '{fileName}' for {enterpriseName}."
        };

        var billTo = NormalizeWhitespace(row.BillTo);
        if (!string.IsNullOrWhiteSpace(billTo)
            && !string.Equals(billTo, NormalizeWhitespace(row.DisplayName), StringComparison.OrdinalIgnoreCase))
        {
            notes.Add($"Bill-to: {billTo}.");
        }

        if (!string.IsNullOrWhiteSpace(row.Fax))
        {
            notes.Add($"Fax: {NormalizeWhitespace(row.Fax)}.");
        }

        if (usedFallbackAddress)
        {
            notes.Add("Structured utility service-address fields were not present in the workbook, so the service location defaults were inferred.");
        }

        return Truncate(string.Join(' ', notes), 500) ?? string.Empty;
    }

    private static bool TryValidateUtilityCustomer(UtilityCustomer customer, out string validationErrors)
    {
        var results = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(customer, new ValidationContext(customer), results, validateAllProperties: true);
        validationErrors = string.Join(
            "; ",
            results
                .Select(result => result.ErrorMessage)
                .Where(message => !string.IsNullOrWhiteSpace(message))
                .Select(message => message!));

        return isValid;
    }

    private static void CopyImportedCustomer(UtilityCustomer source, UtilityCustomer target, DateTime importedAtUtc)
    {
        target.AccountNumber = source.AccountNumber;
        target.FirstName = source.FirstName;
        target.LastName = source.LastName;
        target.CompanyName = source.CompanyName;
        target.CustomerType = source.CustomerType;
        target.ServiceAddress = source.ServiceAddress;
        target.ServiceCity = source.ServiceCity;
        target.ServiceState = source.ServiceState;
        target.ServiceZipCode = source.ServiceZipCode;
        target.MailingAddress = source.MailingAddress;
        target.MailingCity = source.MailingCity;
        target.MailingState = source.MailingState;
        target.MailingZipCode = source.MailingZipCode;
        target.PhoneNumber = source.PhoneNumber;
        target.ServiceLocation = source.ServiceLocation;
        target.Status = source.Status;
        target.AccountOpenDate = source.AccountOpenDate;
        target.CurrentBalance = source.CurrentBalance;
        target.Notes = source.Notes;
        if (target.CreatedDate == default)
        {
            target.CreatedDate = importedAtUtc;
        }

        target.LastModifiedDate = importedAtUtc;
    }

    private static string BuildUtilityCustomerImportStatus(
        int importedCount,
        int insertedCount,
        int updatedCount,
        int skippedCount,
        int workbookCount,
        int rowCount,
        int structuredAddressCount,
        int fallbackAddressCount)
    {
        if (workbookCount == 0)
        {
            return "No QuickBooks customer workbook was found. UtilityCustomers were left unchanged.";
        }

        if (importedCount == 0)
        {
            return $"Detected {rowCount} QuickBooks customer row(s) across {workbookCount} workbook(s), but none could be synchronized into UtilityCustomers.";
        }

        var status = $"Imported {importedCount} UtilityCustomer record(s) from {workbookCount} QuickBooks customer workbook(s) ({insertedCount} inserted, {updatedCount} updated).";
        if (structuredAddressCount > 0)
        {
            status += $" Parsed {structuredAddressCount} structured address row(s).";
        }

        if (fallbackAddressCount > 0)
        {
            status += $" {fallbackAddressCount} row(s) used inferred Wiley service defaults because the workbook did not include structured utility service-address fields.";
        }

        if (skippedCount > 0)
        {
            status += $" Skipped {skippedCount} invalid or empty row(s).";
        }

        return status;
    }

    private static string NormalizeWhitespace(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : MultiWhitespaceRegex.Replace(value.Trim(), " ");
    }

    private static string? Truncate(string? value, int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length <= maximumLength)
        {
            return value;
        }

        return value[..maximumLength];
    }

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