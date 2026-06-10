using System.Security.Cryptography;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;
using ExcelDataReader;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WileyCoWeb.Contracts;
using WileyWidget.Data;
using WileyWidget.Models.Amplify;
using WileyWidget.Services.Abstractions;

namespace WileyWidget.Services;

public sealed class QuickBooksImportService
{
	private const string CanonicalEntity = "quickbooks-ledger";

	private readonly ILogger<QuickBooksImportService> logger;
	private readonly IDbContextFactory<AppDbContext> contextFactory;
	private readonly IQuickBooksFileParser csvParser;
	private readonly IQuickBooksFileParser excelParser;
	private readonly QuickBooksRoutingService routingService;
	private readonly IEnterpriseLedgerCostService enterpriseLedgerCostService;

	static QuickBooksImportService()
	{
		Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
	}

	public QuickBooksImportService(
		ILogger<QuickBooksImportService> logger,
		IDbContextFactory<AppDbContext> contextFactory,
		QuickBooksRoutingService routingService,
		IEnterpriseLedgerCostService enterpriseLedgerCostService,
		QuickBooksCsvParser csvParser,
		QuickBooksExcelParser excelParser)
	{
		this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
		this.contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
		this.routingService = routingService ?? throw new ArgumentNullException(nameof(routingService));
		this.enterpriseLedgerCostService = enterpriseLedgerCostService ?? throw new ArgumentNullException(nameof(enterpriseLedgerCostService));
		this.csvParser = csvParser ?? throw new ArgumentNullException(nameof(csvParser));
		this.excelParser = excelParser ?? throw new ArgumentNullException(nameof(excelParser));
	}

	public async Task<QuickBooksImportPreviewResponse> PreviewAsync(byte[] fileBytes, string fileName, string selectedEnterprise, int selectedFiscalYear, CancellationToken cancellationToken = default)
	{
		logger.LogInformation("Previewing QuickBooks import for {FileName} in {Enterprise} FY {FiscalYear} ({ByteCount} bytes)", Path.GetFileName(fileName), selectedEnterprise, selectedFiscalYear, fileBytes.LongLength);
		var preview = await ParseAsync(fileBytes, fileName).ConfigureAwait(false);

		// Slice 2c: proactive structural validation (max rows, amount bounds, allowed enterprises).
		// Throws ArgumentException -> 400 via GlobalExceptionHandler (consistent with other invalid input paths).
		ValidateStructuralLimits(preview, selectedEnterprise);

		var routedPreview = await routingService.ApplyRoutingAsync(preview, fileName, selectedEnterprise, cancellationToken).ConfigureAwait(false);
		var fileHash = ComputeFileHash(fileBytes);

		await using var context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
		var fileHashDuplicate = await context.SourceFiles.AsNoTracking().AnyAsync(sourceFile => sourceFile.CanonicalEntity == CanonicalEntity && sourceFile.FileHash == fileHash, cancellationToken).ConfigureAwait(false);
		var overlapAnalysis = await AnalyzeRoutedDuplicatesAsync(context, routedPreview, cancellationToken).ConfigureAwait(false);
		var duplicateRows = fileHashDuplicate ? routedPreview.Count : overlapAnalysis.DuplicateRows;
		var isDuplicate = fileHashDuplicate || duplicateRows > 0;
		var previewRows = fileHashDuplicate
			? routedPreview.Select(row => row with { IsDuplicate = true }).ToList()
			: overlapAnalysis.Rows;
		var statusMessage = BuildPreviewStatusMessage(preview.Count, duplicateRows, fileHashDuplicate);

		logger.LogInformation("QuickBooks preview completed for {FileName}: rows={RowCount}, duplicate={IsDuplicate}, duplicateRows={DuplicateRows}, fileHashDuplicate={FileHashDuplicate}", Path.GetFileName(fileName), preview.Count, isDuplicate, duplicateRows, fileHashDuplicate);

		return new QuickBooksImportPreviewResponse(
			Path.GetFileName(fileName),
			fileHash,
			selectedEnterprise,
			selectedFiscalYear,
			previewRows.Count,
			duplicateRows,
			isDuplicate,
			statusMessage,
			previewRows);
	}

	public async Task<QuickBooksImportCommitResponse> CommitAsync(byte[] fileBytes, string fileName, string selectedEnterprise, int selectedFiscalYear, CancellationToken cancellationToken = default)
	{
		logger.LogInformation("Committing QuickBooks import for {FileName} in {Enterprise} FY {FiscalYear} ({ByteCount} bytes)", Path.GetFileName(fileName), selectedEnterprise, selectedFiscalYear, fileBytes.LongLength);
		var parsedRows = await ParseAsync(fileBytes, fileName).ConfigureAwait(false);

		// Slice 2c: proactive structural validation (same limits as preview for defense-in-depth).
		ValidateStructuralLimits(parsedRows, selectedEnterprise);

		var routedRows = await routingService.ApplyRoutingAsync(parsedRows, fileName, selectedEnterprise, cancellationToken).ConfigureAwait(false);
		var fileHash = ComputeFileHash(fileBytes);

		await using var context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

		var duplicateExists = await context.SourceFiles.AsNoTracking().AnyAsync(sourceFile => sourceFile.CanonicalEntity == CanonicalEntity && sourceFile.FileHash == fileHash, cancellationToken).ConfigureAwait(false);
		if (duplicateExists)
		{
			logger.LogWarning("Blocked duplicate QuickBooks import for {FileName}", fileName);
			return new QuickBooksImportCommitResponse(
				Path.GetFileName(fileName),
				fileHash,
				selectedEnterprise,
				selectedFiscalYear,
				0,
				0,
				true,
				"Duplicate QuickBooks import blocked. The file was already imported.",
				["The selected file was already imported. No changes were made."]);
		}

		var overlapAnalysis = await AnalyzeRoutedDuplicatesAsync(context, routedRows, cancellationToken).ConfigureAwait(false);
		if (overlapAnalysis.DuplicateRows > 0)
		{
			logger.LogWarning("Blocked overlapping QuickBooks import for {FileName}: duplicateRows={DuplicateRows}", fileName, overlapAnalysis.DuplicateRows);
			return new QuickBooksImportCommitResponse(
				Path.GetFileName(fileName),
				fileHash,
				selectedEnterprise,
				selectedFiscalYear,
				0,
				0,
				true,
				BuildOverlapBlockedStatusMessage(overlapAnalysis.DuplicateRows),
				[$"{overlapAnalysis.DuplicateRows} routed row(s) already exist in the target enterprise scope(s). No changes were made."]);
		}

		var now = DateTimeOffset.UtcNow;
		var batch = new ImportBatch
		{
			BatchName = Path.GetFileNameWithoutExtension(fileName),
			SourceSystem = "quickbooks-desktop",
			Status = "completed",
			StartedAt = now,
			CompletedAt = now,
			Notes = $"Imported from QuickBooks Desktop export for {selectedEnterprise} FY {selectedFiscalYear}."
		};

		var sourceFile = new SourceFile
		{
			Batch = batch,
			CanonicalEntity = CanonicalEntity,
			OriginalFileName = Path.GetFileName(fileName),
			NormalizedFileName = Path.GetFileName(fileName),
			FileHash = fileHash,
			RowCount = parsedRows.Count,
			ColumnCount = 11,
			ImportedAt = now
		};

		context.ImportBatches.Add(batch);
		context.SourceFiles.Add(sourceFile);

		if (context.Database.IsRelational())
		{
			await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
			try
			{
				await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

				foreach (var row in routedRows)
				{
					context.LedgerEntries.Add(routingService.CreateLedgerEntry(sourceFile.Id, row, selectedEnterprise));
				}

				await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
				await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
			}
			catch
			{
				await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
				throw;
			}
		}
		else
		{
			await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

			foreach (var row in routedRows)
			{
				context.LedgerEntries.Add(routingService.CreateLedgerEntry(sourceFile.Id, row, selectedEnterprise));
			}

			await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
		}

		var refreshedEnterprises = await enterpriseLedgerCostService.RefreshEnterpriseMonthlyExpensesAsync(selectedFiscalYear, cancellationToken).ConfigureAwait(false);
		logger.LogInformation(
			"QuickBooks import committed for {FileName}: batchId={BatchId}, rows={RowCount}, refreshedEnterpriseCosts={RefreshedEnterpriseCosts}",
			Path.GetFileName(fileName),
			batch.Id,
			routedRows.Count,
			refreshedEnterprises);

		return new QuickBooksImportCommitResponse(
			Path.GetFileName(fileName),
			fileHash,
			selectedEnterprise,
			selectedFiscalYear,
			routedRows.Count,
			batch.Id,
			false,
			$"Imported {routedRows.Count} QuickBooks routed row(s) for {selectedEnterprise} FY {selectedFiscalYear}.",
			[]);
	}

	private async Task<List<QuickBooksImportPreviewRow>> ParseAsync(byte[] fileBytes, string fileName)
	{
		var extension = Path.GetExtension(fileName).ToLowerInvariant();
		return extension switch
		{
			".csv" => await csvParser.ParseAsync(fileBytes, fileName).ConfigureAwait(false),
			".xlsx" or ".xls" => await excelParser.ParseAsync(fileBytes, fileName).ConfigureAwait(false),
			_ => throw new InvalidOperationException($"Unsupported QuickBooks export format: {extension}")
		};
	}

	private static string BuildPreviewStatusMessage(int rowCount, int duplicateRows, bool fileHashDuplicate)
		=> fileHashDuplicate
			? "This QuickBooks export is already imported. The commit step will be blocked."
			: duplicateRows > 0
				? BuildOverlapBlockedStatusMessage(duplicateRows)
				: $"Preview loaded for {rowCount} rows.";

	private static string BuildOverlapBlockedStatusMessage(int duplicateRows)
		=> $"{duplicateRows} routed row(s) already exist in the target enterprise scope(s). The commit step will be blocked to prevent duplicate ledger postings.";

	private async Task<DuplicateRowAnalysis> AnalyzeRoutedDuplicatesAsync(AppDbContext context, IReadOnlyList<QuickBooksImportPreviewRow> routedRows, CancellationToken cancellationToken)
	{
		if (routedRows.Count == 0)
		{
			return new DuplicateRowAnalysis(0, []);
		}

		var scopes = routedRows
			.Select(row => row.RoutedEnterprise)
			.Where(scope => !string.IsNullOrWhiteSpace(scope))
			.Select(scope => scope!.Trim())
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.ToList();

		if (scopes.Count == 0)
		{
			return new DuplicateRowAnalysis(0, routedRows.ToList());
		}

		var existingSignatures = (await context.LedgerEntries
			.AsNoTracking()
			.Where(entry => scopes.Contains(entry.EntryScope))
			.ToListAsync(cancellationToken)
			.ConfigureAwait(false))
			.Select(QuickBooksLedgerSignature.FromLedgerEntry)
			.ToHashSet(StringComparer.Ordinal);

		var duplicateRows = 0;
		var analyzedRows = new List<QuickBooksImportPreviewRow>(routedRows.Count);

		foreach (var row in routedRows)
		{
			var isDuplicate = existingSignatures.Contains(QuickBooksLedgerSignature.FromPreviewRow(row));
			if (isDuplicate)
			{
				duplicateRows++;
			}

			analyzedRows.Add(row with { IsDuplicate = isDuplicate });
		}

		return new DuplicateRowAnalysis(duplicateRows, analyzedRows);
	}

	private static string ComputeFileHash(byte[] fileBytes)
		=> Convert.ToHexString(SHA256.HashData(fileBytes));

	private void ValidateStructuralLimits(IReadOnlyList<QuickBooksImportPreviewRow> rows, string selectedEnterprise)
	{
		const int MaxRows = 10000;
		const decimal MaxAmount = 999_999_999.99m;
		var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
		{
			"Water Utility", "Electric Utility", "Sewer Utility", "General Fund", "Town of Wiley",
			// Core enterprises used throughout model, tests, bootstrap data, and sample imports (WSD = sanitation/sewer).
			"Wiley Sanitation District", "Trash", "Apartments"
		};

		if (rows.Count > MaxRows)
			throw new ArgumentException($"QuickBooks row count ({rows.Count}) exceeds maximum allowed for preview/commit ({MaxRows}).");

		if (rows.Any(r => r.Amount.HasValue && Math.Abs(r.Amount.Value) > MaxAmount))
			throw new ArgumentException($"QuickBooks contains amount(s) exceeding allowed bound (+/-{MaxAmount:N2}).");

		if (!allowed.Contains(selectedEnterprise))
			throw new ArgumentException($"Enterprise '{selectedEnterprise}' is not permitted for QuickBooks imports in this environment.");
	}

	private sealed record DuplicateRowAnalysis(int DuplicateRows, IReadOnlyList<QuickBooksImportPreviewRow> Rows);

}