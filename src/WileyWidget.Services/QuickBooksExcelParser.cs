using ExcelDataReader;
using WileyCoWeb.Contracts;

namespace WileyWidget.Services;

public sealed class QuickBooksExcelParser : IQuickBooksFileParser
{
    public async Task<List<QuickBooksImportPreviewRow>> ParseAsync(byte[] fileBytes, string fileName)
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
            return date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        }

        return normalized;
    }

    private static string? ConvertExcelCellValue(object? value)
    {
        return value switch
        {
            null => null,
            DateTime dateTime => dateTime.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
            DateOnly dateOnly => dateOnly.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
            IFormattable formattable => formattable.ToString(null, System.Globalization.CultureInfo.InvariantCulture),
            _ => value.ToString()
        };
    }

    private static decimal? TryParseDecimal(string? value)
    {
        return decimal.TryParse(value, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }

    private static bool TryParseExcelSerialDate(string? value, out DateOnly parsedDate)
    {
        parsedDate = default;
        if (string.IsNullOrWhiteSpace(value) || !decimal.TryParse(value, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var serial))
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