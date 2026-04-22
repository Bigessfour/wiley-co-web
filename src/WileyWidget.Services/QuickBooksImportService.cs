using System.Globalization;
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

namespace WileyWidget.Services;

public sealed class QuickBooksImportService
{
	private const string CanonicalEntity = "quickbooks-ledger";

	private readonly ILogger<QuickBooksImportService> logger;
	private readonly IDbContextFactory<AppDbContext> contextFactory;
	private readonly IQuickBooksFileParser csvParser;
	private readonly IQuickBooksFileParser excelParser;
	private readonly QuickBooksRoutingService routingService;

	static QuickBooksImportService()
	{
		Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
	}

	public QuickBooksImportService(
		ILogger<QuickBooksImportService> logger,
		IDbContextFactory<AppDbContext> contextFactory,
		QuickBooksRoutingService routingService,
		QuickBooksCsvParser csvParser,
		QuickBooksExcelParser excelParser)
	{
		this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
		this.contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
		this.routingService = routingService ?? throw new ArgumentNullException(nameof(routingService));
		this.csvParser = csvParser ?? throw new ArgumentNullException(nameof(csvParser));
		this.excelParser = excelParser ?? throw new ArgumentNullException(nameof(excelParser));
	}

	public async Task<QuickBooksImportPreviewResponse> PreviewAsync(byte[] fileBytes, string fileName, string selectedEnterprise, int selectedFiscalYear, CancellationToken cancellationToken = default)
	{
		logger.LogInformation("Previewing QuickBooks import for {FileName} in {Enterprise} FY {FiscalYear} ({ByteCount} bytes)", Path.GetFileName(fileName), selectedEnterprise, selectedFiscalYear, fileBytes.LongLength);
		var preview = await ParseAsync(fileBytes, fileName).ConfigureAwait(false);
		var routedPreview = await routingService.ApplyRoutingAsync(preview, fileName, selectedEnterprise, cancellationToken).ConfigureAwait(false);
		var fileHash = ComputeFileHash(fileBytes);

		await using var context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
		var isDuplicate = await context.SourceFiles.AsNoTracking().AnyAsync(sourceFile => sourceFile.CanonicalEntity == CanonicalEntity && sourceFile.FileHash == fileHash, cancellationToken).ConfigureAwait(false);

		var statusMessage = isDuplicate
			? "This QuickBooks export is already imported. The commit step will be blocked."
			: $"Preview loaded for {preview.Count} rows.";

		logger.LogInformation("QuickBooks preview completed for {FileName}: rows={RowCount}, duplicate={IsDuplicate}", Path.GetFileName(fileName), preview.Count, isDuplicate);

		return new QuickBooksImportPreviewResponse(
			Path.GetFileName(fileName),
			fileHash,
			selectedEnterprise,
			selectedFiscalYear,
			routedPreview.Count,
			isDuplicate ? routedPreview.Count : 0,
			isDuplicate,
			statusMessage,
			routedPreview.Select(row => row with { IsDuplicate = isDuplicate }).ToList());
	}

	public async Task<QuickBooksImportCommitResponse> CommitAsync(byte[] fileBytes, string fileName, string selectedEnterprise, int selectedFiscalYear, CancellationToken cancellationToken = default)
	{
		logger.LogInformation("Committing QuickBooks import for {FileName} in {Enterprise} FY {FiscalYear} ({ByteCount} bytes)", Path.GetFileName(fileName), selectedEnterprise, selectedFiscalYear, fileBytes.LongLength);
		var parsedRows = await ParseAsync(fileBytes, fileName).ConfigureAwait(false);
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
		await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

		foreach (var row in routedRows)
		{
			context.LedgerEntries.Add(routingService.CreateLedgerEntry(sourceFile.Id, row, selectedEnterprise));
		}

		await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
		logger.LogInformation("QuickBooks import committed for {FileName}: batchId={BatchId}, rows={RowCount}", Path.GetFileName(fileName), batch.Id, routedRows.Count);

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

	private static string ComputeFileHash(byte[] fileBytes)
		=> Convert.ToHexString(SHA256.HashData(fileBytes));

}