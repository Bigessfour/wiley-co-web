using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using WileyCoWeb.Contracts;

namespace WileyWidget.Services;

public sealed class QuickBooksCsvParser : IQuickBooksFileParser
{
    public async Task<List<QuickBooksImportPreviewRow>> ParseAsync(byte[] fileBytes, string fileName)
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

    private static IEnumerable<(int RowNumber, IReadOnlyList<string?> Values)> ReadCsvRows(CsvParser parser)
    {
        var rowNumber = 2;
        while (parser.Read())
        {
            yield return (rowNumber, (parser.Record ?? []).Cast<string?>().ToArray());
            rowNumber++;
        }
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

    private static decimal? TryParseDecimal(string? value)
    {
        return decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }

    private static bool TryParseExcelSerialDate(string? value, out DateOnly parsedDate)
    {
        parsedDate = default;
        if (string.IsNullOrWhiteSpace(value) || !decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var serial))
        {
            return false;
        }

        try
        {
            parsedDate = DateOnly.FromDateTime(DateTime.FromOADate((double)serial));
            return true;
        }
        catch
        {
            return false;
        }
    }
}