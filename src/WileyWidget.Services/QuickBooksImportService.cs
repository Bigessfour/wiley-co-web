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

	static QuickBooksImportService()
	{
		Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
	}

	public QuickBooksImportService(ILogger<QuickBooksImportService> logger, IDbContextFactory<AppDbContext> contextFactory)
	{
		this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
		this.contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
	}

	public async Task<QuickBooksImportPreviewResponse> PreviewAsync(byte[] fileBytes, string fileName, string selectedEnterprise, int selectedFiscalYear, CancellationToken cancellationToken = default)
	{
		var preview = await ParseAsync(fileBytes, fileName).ConfigureAwait(false);
		var fileHash = ComputeFileHash(fileBytes);

		await using var context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
		var isDuplicate = await context.SourceFiles.AsNoTracking().AnyAsync(sourceFile => sourceFile.CanonicalEntity == CanonicalEntity && sourceFile.FileHash == fileHash, cancellationToken).ConfigureAwait(false);

		var statusMessage = isDuplicate
			? "This QuickBooks export is already imported. The commit step will be blocked."
			: $"Preview loaded for {preview.Count} rows.";

		return new QuickBooksImportPreviewResponse(
			Path.GetFileName(fileName),
			fileHash,
			selectedEnterprise,
			selectedFiscalYear,
			preview.Count,
			isDuplicate ? preview.Count : 0,
			isDuplicate,
			statusMessage,
			preview.Select(row => row with { IsDuplicate = isDuplicate }).ToList());
	}

	public async Task<QuickBooksImportCommitResponse> CommitAsync(byte[] fileBytes, string fileName, string selectedEnterprise, int selectedFiscalYear, CancellationToken cancellationToken = default)
	{
		var parsedRows = await ParseAsync(fileBytes, fileName).ConfigureAwait(false);
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

		foreach (var row in parsedRows)
		{
			context.LedgerEntries.Add(new LedgerEntry
			{
				SourceFile = sourceFile,
				SourceRowNumber = row.RowNumber,
				EntryDate = TryParseDateOnly(row.EntryDate),
				EntryType = row.EntryType,
				TransactionNumber = row.TransactionNumber,
				Name = row.Name,
				Memo = row.Memo,
				AccountName = row.AccountName,
				SplitAccount = row.SplitAccount,
				Amount = row.Amount,
				RunningBalance = row.RunningBalance,
				ClearedFlag = row.ClearedFlag,
				EntryScope = selectedEnterprise
			});
		}

		await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

		return new QuickBooksImportCommitResponse(
			Path.GetFileName(fileName),
			fileHash,
			selectedEnterprise,
			selectedFiscalYear,
			parsedRows.Count,
			batch.Id,
			false,
			$"Imported {parsedRows.Count} QuickBooks rows for {selectedEnterprise} FY {selectedFiscalYear}.",
			[]);
	}

	private static async Task<List<QuickBooksImportPreviewRow>> ParseAsync(byte[] fileBytes, string fileName)
	{
		var extension = Path.GetExtension(fileName).ToLowerInvariant();
		return extension switch
		{
			".csv" => await ParseCsvAsync(fileBytes).ConfigureAwait(false),
			".xlsx" or ".xls" => await ParseExcelAsync(fileBytes).ConfigureAwait(false),
			_ => throw new InvalidOperationException($"Unsupported QuickBooks export format: {extension}")
		};
	}

	private static async Task<List<QuickBooksImportPreviewRow>> ParseCsvAsync(byte[] fileBytes)
	{
		await using var stream = new MemoryStream(fileBytes, writable: false);
		using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
		using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
		{
			HasHeaderRecord = true,
			MissingFieldFound = null,
			BadDataFound = context => throw new InvalidOperationException($"Bad QuickBooks CSV data near row '{context.RawRecord}'")
		});

		csv.Context.RegisterClassMap<QuickBooksImportRowMap>();
		var rows = new List<QuickBooksImportPreviewRow>();

		try
		{
			var records = csv.GetRecords<QuickBooksImportRow>().ToList();
			for (var index = 0; index < records.Count; index++)
			{
				var record = records[index];
				rows.Add(ToPreviewRow(index + 2, record));
			}
		}
		catch (Exception ex)
		{
			throw new InvalidOperationException($"Unable to read the QuickBooks CSV export: {ex.Message}", ex);
		}

		return await Task.FromResult(rows);
	}

	private static async Task<List<QuickBooksImportPreviewRow>> ParseExcelAsync(byte[] fileBytes)
	{
		await using var stream = new MemoryStream(fileBytes, writable: false);
		using var reader = ExcelReaderFactory.CreateReader(stream);
		var dataSet = reader.AsDataSet(new ExcelDataSetConfiguration
		{
			ConfigureDataTable = _ => new ExcelDataTableConfiguration { UseHeaderRow = true }
		});

		if (dataSet.Tables.Count == 0)
		{
			return [];
		}

		var table = dataSet.Tables[0];
		var rows = new List<QuickBooksImportPreviewRow>(table.Rows.Count);

		for (var index = 0; index < table.Rows.Count; index++)
		{
			var row = table.Rows[index];
			rows.Add(ToPreviewRow(index + 2, new QuickBooksImportRow
			{
				EntryDate = row["Date"]?.ToString() ?? row["Transaction Date"]?.ToString(),
				EntryType = row["Type"]?.ToString() ?? row["Transaction Type"]?.ToString(),
				TransactionNumber = row["Num"]?.ToString() ?? row["Transaction Number"]?.ToString(),
				Name = row["Name"]?.ToString(),
				Memo = row["Memo"]?.ToString() ?? row["Description"]?.ToString(),
				AccountName = row["Account"]?.ToString(),
				SplitAccount = row["Split"]?.ToString(),
				Amount = TryParseDecimal(row["Amount"]?.ToString()),
				RunningBalance = TryParseDecimal(row["Balance"]?.ToString()),
				ClearedFlag = row["Clr"]?.ToString()
			}));
		}

		return rows;
	}

	private static QuickBooksImportPreviewRow ToPreviewRow(int rowNumber, QuickBooksImportRow row)
	{
		return new QuickBooksImportPreviewRow(
			rowNumber,
			row.EntryDate,
			row.EntryType,
			row.TransactionNumber,
			row.Name,
			row.Memo,
			row.AccountName,
			row.SplitAccount,
			row.Amount,
			row.RunningBalance,
			row.ClearedFlag,
			false);
	}

	private static DateOnly? TryParseDateOnly(string? value)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			return null;
		}

		return DateOnly.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDate)
			? parsedDate
			: null;
	}

	private static decimal? TryParseDecimal(string? value)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			return null;
		}

		return decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsedValue)
			? parsedValue
			: null;
	}

	private static string ComputeFileHash(byte[] fileBytes)
		=> Convert.ToHexString(SHA256.HashData(fileBytes));

	private sealed class QuickBooksImportRow
	{
		public string? EntryDate { get; set; }
		public string? EntryType { get; set; }
		public string? TransactionNumber { get; set; }
		public string? Name { get; set; }
		public string? Memo { get; set; }
		public string? AccountName { get; set; }
		public string? SplitAccount { get; set; }
		public decimal? Amount { get; set; }
		public decimal? RunningBalance { get; set; }
		public string? ClearedFlag { get; set; }
	}

#pragma warning disable S1144
	private sealed class QuickBooksImportRowMap : ClassMap<QuickBooksImportRow>
	{
		public QuickBooksImportRowMap()
		{
			Map(m => m.EntryDate).Name("Date", "Transaction Date");
			Map(m => m.EntryType).Name("Type", "Transaction Type");
			Map(m => m.TransactionNumber).Name("Num", "Transaction Number");
			Map(m => m.Name).Name("Name", "Customer", "Vendor");
			Map(m => m.Memo).Name("Memo", "Description");
			Map(m => m.AccountName).Name("Account");
			Map(m => m.SplitAccount).Name("Split");
			Map(m => m.Amount).Name("Amount");
			Map(m => m.RunningBalance).Name("Balance");
			Map(m => m.ClearedFlag).Name("Clr");
		}
	}
#pragma warning restore S1144
}