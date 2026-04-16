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
		logger.LogInformation("Previewing QuickBooks import for {FileName} in {Enterprise} FY {FiscalYear} ({ByteCount} bytes)", Path.GetFileName(fileName), selectedEnterprise, selectedFiscalYear, fileBytes.LongLength);
		var preview = await ParseAsync(fileBytes, fileName).ConfigureAwait(false);
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
			preview.Count,
			isDuplicate ? preview.Count : 0,
			isDuplicate,
			statusMessage,
			preview.Select(row => row with { IsDuplicate = isDuplicate }).ToList());
	}

	public async Task<QuickBooksImportCommitResponse> CommitAsync(byte[] fileBytes, string fileName, string selectedEnterprise, int selectedFiscalYear, CancellationToken cancellationToken = default)
	{
		logger.LogInformation("Committing QuickBooks import for {FileName} in {Enterprise} FY {FiscalYear} ({ByteCount} bytes)", Path.GetFileName(fileName), selectedEnterprise, selectedFiscalYear, fileBytes.LongLength);
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
		logger.LogInformation("QuickBooks import committed for {FileName}: batchId={BatchId}, rows={RowCount}", Path.GetFileName(fileName), batch.Id, parsedRows.Count);

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
		using var parser = new CsvParser(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
		{
			HasHeaderRecord = false,
			MissingFieldFound = null,
			BadDataFound = context => throw new InvalidOperationException($"Bad QuickBooks CSV data near row '{context.RawRecord}'")
		});

		try
		{
			if (!parser.Read())
			{
				return [];
			}

			var headerRow = parser.Record ?? [];
			return ParseTabularRows(headerRow, ReadCsvRows(parser));
		}
		catch (Exception ex)
		{
			throw new InvalidOperationException($"Unable to read the QuickBooks CSV export: {ex.Message}", ex);
		}
	}

	private static async Task<List<QuickBooksImportPreviewRow>> ParseExcelAsync(byte[] fileBytes)
	{
		await using var stream = new MemoryStream(fileBytes, writable: false);
		using var reader = ExcelReaderFactory.CreateReader(stream);
		var dataSet = reader.AsDataSet(new ExcelDataSetConfiguration
		{
			ConfigureDataTable = _ => new ExcelDataTableConfiguration { UseHeaderRow = false }
		});

		if (dataSet.Tables.Count == 0)
		{
			return [];
		}

		foreach (System.Data.DataTable table in dataSet.Tables)
		{
			var rawRows = table.Rows.Cast<System.Data.DataRow>()
				.Select((row, index) => (RowNumber: index + 1, Values: (IReadOnlyList<string?>)row.ItemArray.Select(ConvertExcelCellValue).ToArray()))
				.ToList();

			if (TryParseExcelWorksheet(rawRows, out var parsedRows))
			{
				return parsedRows;
			}
		}

		return [];
	}

	private static bool TryParseExcelWorksheet(
		IReadOnlyList<(int RowNumber, IReadOnlyList<string?> Values)> rawRows,
		out List<QuickBooksImportPreviewRow> parsedRows)
	{
		for (var index = 0; index < rawRows.Count; index++)
		{
			var headerLookup = CreateHeaderLookup(rawRows[index].Values);
			if (!IsRecognizedQuickBooksHeader(headerLookup))
			{
				continue;
			}

			parsedRows = ParseTabularRows(rawRows[index].Values, rawRows.Skip(index + 1));
			return true;
		}

		parsedRows = [];
		return false;
	}

	private static List<QuickBooksImportPreviewRow> ParseTabularRows(IReadOnlyList<string?> headerRow, IEnumerable<(int RowNumber, IReadOnlyList<string?> Values)> rawRows)
	{
		var headerLookup = CreateHeaderLookup(headerRow);
		var rows = new List<QuickBooksImportPreviewRow>();
		string? currentAccountContext = null;

		foreach (var rawRow in rawRows)
		{
			var previewRow = TryCreatePreviewRow(rawRow.RowNumber, rawRow.Values, headerLookup, ref currentAccountContext);
			if (previewRow is not null)
			{
				rows.Add(previewRow);
			}
		}

		return rows;
	}

	private static IEnumerable<(int RowNumber, IReadOnlyList<string?> Values)> ReadCsvRows(CsvParser parser)
	{
		var rowNumber = 2;
		while (parser.Read())
		{
			yield return (rowNumber, (parser.Record ?? []).Cast<string?>().ToArray());
			rowNumber++;
		}
	}

	private static QuickBooksImportPreviewRow? TryCreatePreviewRow(int rowNumber, IReadOnlyList<string?> values, HeaderLookup headerLookup, ref string? currentAccountContext)
	{
		var leadingLabel = GetLeadingLabel(values, headerLookup.FirstMappedColumnIndex);
		var entryType = GetCellValue(values, headerLookup.TypeIndex);
		var entryDate = NormalizeEntryDate(GetCellValue(values, headerLookup.DateIndex));
		var amount = TryParseDecimal(GetCellValue(values, headerLookup.AmountIndex));
		var runningBalance = TryParseDecimal(GetCellValue(values, headerLookup.BalanceIndex));

		if (string.IsNullOrWhiteSpace(entryType) || string.IsNullOrWhiteSpace(entryDate))
		{
			if (headerLookup.AccountIndex < 0
				&& !string.IsNullOrWhiteSpace(leadingLabel)
				&& (amount is not null || runningBalance is not null)
				&& !leadingLabel.StartsWith("Total ", StringComparison.OrdinalIgnoreCase))
			{
				currentAccountContext = leadingLabel;
			}

			return null;
		}

		var accountName = GetCellValue(values, headerLookup.AccountIndex) ?? currentAccountContext;

		return new QuickBooksImportPreviewRow(
			rowNumber,
			entryDate,
			entryType,
			GetCellValue(values, headerLookup.TransactionNumberIndex),
			GetCellValue(values, headerLookup.NameIndex),
			GetCellValue(values, headerLookup.MemoIndex),
			accountName,
			GetCellValue(values, headerLookup.SplitIndex),
			amount,
			runningBalance,
			GetCellValue(values, headerLookup.ClearedFlagIndex),
			false);
	}

	private static HeaderLookup CreateHeaderLookup(IReadOnlyList<string?> headerRow)
	{
		return new HeaderLookup(
			FindHeaderIndex(headerRow, "Type", "Transaction Type"),
			FindHeaderIndex(headerRow, "Date", "Transaction Date"),
			FindHeaderIndex(headerRow, "Num", "Transaction Number"),
			FindHeaderIndex(headerRow, "Name", "Customer", "Vendor"),
			FindHeaderIndex(headerRow, "Memo", "Description"),
			FindHeaderIndex(headerRow, "Account"),
			FindHeaderIndex(headerRow, "Split"),
			FindHeaderIndex(headerRow, "Amount"),
			FindHeaderIndex(headerRow, "Balance"),
			FindHeaderIndex(headerRow, "Clr"));
	}

	private static bool IsRecognizedQuickBooksHeader(HeaderLookup headerLookup)
		=> headerLookup.TypeIndex >= 0
			&& headerLookup.DateIndex >= 0
			&& (headerLookup.AmountIndex >= 0
				|| headerLookup.BalanceIndex >= 0
				|| headerLookup.AccountIndex >= 0
				|| headerLookup.SplitIndex >= 0);

	private static int FindHeaderIndex(IReadOnlyList<string?> headers, params string[] aliases)
	{
		for (var index = 0; index < headers.Count; index++)
		{
			var normalizedHeader = NormalizeHeader(headers[index]);
			if (aliases.Any(alias => string.Equals(normalizedHeader, alias, StringComparison.OrdinalIgnoreCase)))
			{
				return index;
			}
		}

		return -1;
	}

	private static string? GetCellValue(IReadOnlyList<string?> values, int index)
	{
		if (index < 0 || index >= values.Count)
		{
			return null;
		}

		return NormalizeCellValue(values[index]);
	}

	private static string? GetLeadingLabel(IReadOnlyList<string?> values, int firstMappedColumnIndex)
	{
		if (firstMappedColumnIndex <= 0)
		{
			return null;
		}

		for (var index = 0; index < Math.Min(firstMappedColumnIndex, values.Count); index++)
		{
			var value = NormalizeCellValue(values[index]);
			if (!string.IsNullOrWhiteSpace(value))
			{
				return value;
			}
		}

		return null;
	}

	private static string? NormalizeHeader(string? value)
		=> string.IsNullOrWhiteSpace(value)
			? null
			: value.Trim().Trim('\uFEFF');

	private static string? NormalizeCellValue(string? value)
		=> string.IsNullOrWhiteSpace(value)
			? null
			: value.Trim();

	private static string? NormalizeEntryDate(string? value)
	{
		var normalized = NormalizeCellValue(value);
		if (TryParseExcelSerialDate(normalized, out var date))
		{
			return date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
		}

		return normalized;
	}

	private static string? ConvertExcelCellValue(object? value)
	{
		return value switch
		{
			null => null,
			DateTime dateTime => dateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
			DateOnly dateOnly => dateOnly.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
			IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
			_ => value.ToString()
		};
	}

	private static DateOnly? TryParseDateOnly(string? value)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			return null;
		}

		return DateOnly.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDate)
			? parsedDate
			: TryParseExcelSerialDate(value, out parsedDate)
				? parsedDate
				: null;
	}

	private static bool TryParseExcelSerialDate(string? value, out DateOnly parsedDate)
	{
		parsedDate = default;

		if (!double.TryParse(value, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var serialDate)
			|| serialDate <= 0
			|| serialDate >= 2958466)
		{
			return false;
		}

		try
		{
			parsedDate = DateOnly.FromDateTime(DateTime.FromOADate(serialDate));
			return true;
		}
		catch (ArgumentException)
		{
			return false;
		}
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

	private sealed record HeaderLookup(
		int TypeIndex,
		int DateIndex,
		int TransactionNumberIndex,
		int NameIndex,
		int MemoIndex,
		int AccountIndex,
		int SplitIndex,
		int AmountIndex,
		int BalanceIndex,
		int ClearedFlagIndex)
	{
		public int FirstMappedColumnIndex => new[]
		{
			TypeIndex,
			DateIndex,
			TransactionNumberIndex,
			NameIndex,
			MemoIndex,
			AccountIndex,
			SplitIndex,
			AmountIndex,
			BalanceIndex,
			ClearedFlagIndex
		}.Where(index => index >= 0).DefaultIfEmpty(0).Min();
	}
}