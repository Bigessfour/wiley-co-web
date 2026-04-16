using System.Globalization;
using System.IO.Compression;
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
    private static readonly StringComparer PathComparer = StringComparer.OrdinalIgnoreCase;
    private static readonly XNamespace SpreadsheetNamespace = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private static readonly XNamespace RelationshipNamespace = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
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

        var ledgerImportSummary = request?.IncludeSampleLedgerData == true
            ? await ImportSampleLedgerDataAsync(resolvedImportPath, cancellationToken).ConfigureAwait(false)
            : LedgerImportSummary.NotRequested();

        var discoveredCustomerRows = sources.Sum(item => item.DiscoveredCustomerReferenceRows);
        var utilityCustomerImportStatus = BuildUtilityCustomerImportStatus(sources);

        logger.LogInformation(
            "Imported workspace reference data from {ImportPath}: imported={Imported}, updated={Updated}, customerRows={CustomerRows}, ledgerFiles={LedgerFiles}, ledgerRows={LedgerRows}, baselines={BaselineCount}",
            resolvedImportPath,
            importedEnterpriseCount,
            updatedEnterpriseCount,
            discoveredCustomerRows,
            ledgerImportSummary.ImportedFileCount,
            ledgerImportSummary.ImportedRowCount,
            seededEnterpriseBaselineCount);

        return new WorkspaceReferenceDataImportResponse(
            resolvedImportPath,
            importedAtUtc.ToString("O", CultureInfo.InvariantCulture),
            importedEnterpriseCount,
            updatedEnterpriseCount,
            discoveredCustomerRows,
            0,
            utilityCustomerImportStatus,
            sources.Select(item => item.Name).OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToArray(),
            ledgerImportSummary.ImportedFileCount,
            ledgerImportSummary.ImportedRowCount,
            ledgerImportSummary.Status,
            seededEnterpriseBaselineCount);
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
        var worksheet = workbookMetadata.Worksheets.FirstOrDefault(sheet => !sheet.Name.Contains("Tips", StringComparison.OrdinalIgnoreCase));
        if (worksheet == null)
        {
            return new CustomerWorkbookInspection(0, false, []);
        }

        using var archive = ZipFile.OpenRead(filePath);
        var worksheetEntry = archive.GetEntry(worksheet.EntryName);
        if (worksheetEntry == null)
        {
            return new CustomerWorkbookInspection(0, false, []);
        }

        var sharedStrings = ReadSharedStrings(archive);
        var worksheetDocument = XDocument.Load(worksheetEntry.Open());
        var rows = worksheetDocument.Root?
            .Descendants(SpreadsheetNamespace + "row")
            .Select(row => row.Elements(SpreadsheetNamespace + "c").Select(cell => ReadCellValue(cell, sharedStrings)).ToList())
            .Where(values => values.Any(value => !string.IsNullOrWhiteSpace(value)))
            .ToList()
            ?? [];

        if (rows.Count == 0)
        {
            return new CustomerWorkbookInspection(0, false, []);
        }

        var headers = rows[0]
            .Select(value => value.Trim())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToArray();

        var canPopulateUtilityCustomers = headers.Any(header => header.Contains("Account", StringComparison.OrdinalIgnoreCase))
            && headers.Any(header => header.Contains("Service", StringComparison.OrdinalIgnoreCase) || header.Contains("Address", StringComparison.OrdinalIgnoreCase));

        return new CustomerWorkbookInspection(Math.Max(0, rows.Count - 1), canPopulateUtilityCustomers, headers);
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
            ? $"Detected {source.DiscoveredCustomerReferenceRows} QuickBooks customer reference rows for this enterprise, but the export lacks the account and service-address fields needed for automatic UtilityCustomer import."
            : "No QuickBooks utility-customer roster was imported for this enterprise.";

        return $"Source files: {fileSummary}. {customerNote}";
    }

    private static string BuildUtilityCustomerImportStatus(IEnumerable<EnterpriseReferenceSource> sources)
    {
        var customerSources = sources.Where(source => source.DiscoveredCustomerReferenceRows > 0).ToList();
        if (customerSources.Count == 0)
        {
            return "No QuickBooks customer workbook was found. Use /api/utility-customers to add customer records manually.";
        }

        var headers = customerSources
            .SelectMany(source => source.CustomerWorkbookHeaders)
            .Where(header => !string.IsNullOrWhiteSpace(header))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(header => header, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var headerSummary = headers.Length == 0 ? "no recognizable headers" : string.Join(", ", headers);
        return $"Detected {customerSources.Sum(source => source.DiscoveredCustomerReferenceRows)} QuickBooks customer reference rows, but skipped automatic UtilityCustomer import because the workbook headers ({headerSummary}) do not include utility account and service-address fields. Use /api/utility-customers for manual CRUD or a dedicated upload later.";
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

    private sealed record EnterpriseReferenceSource(
        string Name,
        string Type,
        IReadOnlyList<string> SourceFiles,
        int EstimatedCitizenCount,
        int DiscoveredCustomerReferenceRows,
        IReadOnlyList<string> CustomerWorkbookHeaders);

    private sealed record EnterpriseBaselineSeed(decimal CurrentRate, decimal MonthlyExpenses, int CitizenCount);

    private sealed record LedgerImportSummary(int ImportedFileCount, int ImportedRowCount, string Status)
    {
        public static LedgerImportSummary NotRequested()
            => new(0, 0, "Sample ledger import was not requested.");
    }
}