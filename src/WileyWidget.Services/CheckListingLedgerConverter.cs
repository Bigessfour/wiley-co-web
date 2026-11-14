using System.Data;
using System.Globalization;
using System.Text;
using ExcelDataReader;
using WileyCoWeb.Contracts;

namespace WileyWidget.Services;

public sealed record CheckListingRow(
    string Date,
    string CheckNumber,
    string Name,
    string Memo,
    string Account,
    decimal Amount);

/// <summary>
/// Turns a clerk check-listing workbook into the transaction list the QuickBooks import already commits.
/// Columns are CK#, DATE, TO, AMOUNT, and FOR. A second section on the same sheet, such as WSD general, is included.
/// </summary>
public static class CheckListingLedgerConverter
{
    private static readonly (string[] Keys, string Suffix)[] AccountRules =
    [
        (["PAYROLL", "WAGES"], "Payroll"),
        (["FUEL"], "Fuel"),
        (["SEWAGE"], "Sewage"),
        (["TRASH TRUCK", "TRASH SUPPLIES", "TRANSMISSION"], "Trash Truck"),
        (["WATER LEAK", "WATER SUPPLIES", "LEAK", "HOTSPOT", "CHLORINE"], "Water Supplies"),
        (["UTILIT"], "Utilities"),
        (["POSTAGE"], "Postage"),
        (["INSURANCE", "WORKMANS", "HEALTH"], "Insurance"),
        (["MNG", "MANAGEMENT"], "Management Fee"),
        (["REPAIR", "RADIATOR", "BACKHOE"], "Repairs"),
        (["MEMBERSHIP", "DUES"], "Dues"),
        (["CLEANING", "LABOR", "LOCK", "DOOR", "STOVE", "WATER HEATER", "STACKABLE", "FIRE EXT"], "Maintenance"),
        (["DMR", "TESTING", "ANALYTICAL", "COLIFORM"], "Lab"),
        (["PUBLICATION", "AD"], "Advertising"),
        (["REFUND"], "Refunds"),
        (["MULTIUSER"], "Software"),
        (["SHOP", "SUPPLIES"], "Supplies")
    ];

    public static string AccountPrefixForEnterprise(string enterprise)
        => enterprise switch
        {
            "Water Utility" => "Utility",
            "Trash" => "Trash",
            "Apartments" => "Brookside",
            "Wiley Sanitation District" => "WSD",
            _ => "Town"
        };

    public static string MapAccount(string? memo, string accountPrefix)
    {
        var text = (memo ?? string.Empty).ToUpperInvariant();
        var tokens = text.Split([' ', '/', ',', '-', '.'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var (keys, suffix) in AccountRules)
        {
            if (keys.Any(key => key.Length <= 3
                ? tokens.Contains(key, StringComparer.Ordinal)
                : text.Contains(key, StringComparison.Ordinal)))
            {
                return accountPrefix + ":" + suffix;
            }
        }

        return accountPrefix + ":Expenses";
    }

    public static bool TryConvert(byte[] fileBytes, string accountPrefix, out byte[] csvBytes, out IReadOnlyList<CheckListingRow> rows)
    {
        csvBytes = [];
        if (!TryRead(fileBytes, accountPrefix, out rows) || rows.Count == 0)
        {
            return false;
        }

        csvBytes = WriteCheckListingCsv(rows);
        return true;
    }

    public static bool TryRead(byte[] fileBytes, string accountPrefix, out IReadOnlyList<CheckListingRow> rows)
    {
        rows = [];
        if (fileBytes.Length == 0)
        {
            return false;
        }

        try
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            using var stream = new MemoryStream(fileBytes, writable: false);
            using var reader = ExcelReaderFactory.CreateReader(stream);
            var dataSet = reader.AsDataSet(new ExcelDataSetConfiguration
            {
                ConfigureDataTable = _ => new ExcelDataTableConfiguration { UseHeaderRow = false }
            });

            var combined = new List<CheckListingRow>();
            foreach (DataTable table in dataSet.Tables)
            {
                if (TryReadTable(table, accountPrefix, out var tableRows) && tableRows.Count > 0)
                {
                    combined.AddRange(tableRows);
                }
            }

            if (combined.Count > 0)
            {
                rows = combined;
                return true;
            }
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or ArgumentException)
        {
            return false;
        }

        return false;
    }

    public static byte[] WriteLedgerCsv(IReadOnlyList<QuickBooksImportPreviewRow> rows)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Date,Type,Num,Name,Memo,Account,Split,Amount,Balance,Clr");
        foreach (var row in rows)
        {
            builder.Append(Csv(row.EntryDate));
            builder.Append(',');
            builder.Append(Csv(row.EntryType));
            builder.Append(',');
            builder.Append(Csv(row.TransactionNumber));
            builder.Append(',');
            builder.Append(Csv(row.Name));
            builder.Append(',');
            builder.Append(Csv(row.Memo));
            builder.Append(',');
            builder.Append(Csv(row.AccountName));
            builder.Append(',');
            builder.Append(Csv(row.SplitAccount));
            builder.Append(',');
            builder.Append(row.Amount?.ToString("0.00", CultureInfo.InvariantCulture) ?? string.Empty);
            builder.Append(',');
            builder.Append(row.RunningBalance?.ToString("0.00", CultureInfo.InvariantCulture) ?? string.Empty);
            builder.Append(',');
            builder.Append(Csv(row.ClearedFlag));
            builder.AppendLine();
        }

        return Encoding.UTF8.GetBytes(builder.ToString());
    }

    private static byte[] WriteCheckListingCsv(IReadOnlyList<CheckListingRow> rows)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Date,Type,Num,Name,Memo,Account,Amount");
        foreach (var row in rows)
        {
            builder.Append(Csv(row.Date));
            builder.Append(",Check,");
            builder.Append(Csv(row.CheckNumber));
            builder.Append(',');
            builder.Append(Csv(row.Name));
            builder.Append(',');
            builder.Append(Csv(row.Memo));
            builder.Append(',');
            builder.Append(Csv(row.Account));
            builder.Append(',');
            builder.Append(row.Amount.ToString("0.00", CultureInfo.InvariantCulture));
            builder.AppendLine();
        }

        return Encoding.UTF8.GetBytes(builder.ToString());
    }

    private static bool TryReadTable(DataTable table, string accountPrefix, out List<CheckListingRow> rows)
    {
        rows = [];
        var headerIndex = -1;
        var ckIndex = -1;
        var dateIndex = -1;
        var nameIndex = -1;
        var amountIndex = -1;
        var memoIndex = -1;

        for (var index = 0; index < table.Rows.Count && index < 40; index++)
        {
            var cells = table.Rows[index].ItemArray;
            if (!TryHeader(cells, out ckIndex, out dateIndex, out nameIndex, out amountIndex, out memoIndex))
            {
                continue;
            }

            headerIndex = index;
            break;
        }

        if (headerIndex < 0)
        {
            return false;
        }

        for (var index = headerIndex + 1; index < table.Rows.Count; index++)
        {
            var cells = table.Rows[index].ItemArray;
            if (!TryCheckRow(cells, ckIndex, dateIndex, nameIndex, amountIndex, memoIndex, accountPrefix, out var row))
            {
                continue;
            }

            rows.Add(row);
        }

        return true;
    }

    private static bool TryHeader(object?[] cells, out int ck, out int date, out int name, out int amount, out int memo)
    {
        ck = Find(cells, "CK");
        date = Find(cells, "DATE");
        name = Find(cells, "TO");
        amount = Find(cells, "AMOUNT");
        memo = Find(cells, "FOR");
        return ck >= 0 && date >= 0 && name >= 0 && amount >= 0 && memo >= 0;
    }

    private static bool TryCheckRow(
        object?[] cells,
        int ckIndex,
        int dateIndex,
        int nameIndex,
        int amountIndex,
        int memoIndex,
        string accountPrefix,
        out CheckListingRow row)
    {
        row = null!;
        var checkNumber = FormatCheckNumber(CellText(cells, ckIndex));
        if (string.IsNullOrWhiteSpace(checkNumber) || !checkNumber.Any(char.IsDigit) || checkNumber.Contains("TOTAL", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!TryDate(cells, dateIndex, out var date))
        {
            return false;
        }

        if (!TryAmount(cells, amountIndex, out var amount))
        {
            return false;
        }

        var name = CellText(cells, nameIndex);
        var memo = CellText(cells, memoIndex);
        row = new CheckListingRow(date, checkNumber, name, memo, MapAccount(memo, accountPrefix), amount);
        return true;
    }

    private static int Find(object?[] cells, string token)
    {
        for (var index = 0; index < cells.Length; index++)
        {
            var text = CellText(cells, index);
            if (text.Contains(token, StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }
        }

        return -1;
    }

    private static string CellText(object?[] cells, int index)
    {
        if (index < 0 || index >= cells.Length || cells[index] is null || cells[index] is DBNull)
        {
            return string.Empty;
        }

        return Convert.ToString(cells[index], CultureInfo.InvariantCulture)?.Trim() ?? string.Empty;
    }

    private static string FormatCheckNumber(string text)
    {
        if (decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var number) && number == decimal.Truncate(number))
        {
            return decimal.Truncate(number).ToString(CultureInfo.InvariantCulture);
        }

        return text;
    }

    private static bool TryDate(object?[] cells, int index, out string date)
    {
        date = string.Empty;
        if (index < 0 || index >= cells.Length || cells[index] is null || cells[index] is DBNull)
        {
            return false;
        }

        switch (cells[index])
        {
            case DateTime dateTime:
                date = DateOnly.FromDateTime(dateTime).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                return true;
            case double serial when serial is > 20_000 and < 80_000:
                date = DateOnly.FromDateTime(DateTime.FromOADate(serial)).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                return true;
        }

        var text = CellText(cells, index);
        if (DateOnly.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            date = parsed.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            return true;
        }

        return false;
    }

    private static bool TryAmount(object?[] cells, int index, out decimal amount)
    {
        amount = 0;
        if (index < 0 || index >= cells.Length || cells[index] is null || cells[index] is DBNull)
        {
            return false;
        }

        if (cells[index] is double number)
        {
            amount = Math.Round(Convert.ToDecimal(number, CultureInfo.InvariantCulture), 2, MidpointRounding.AwayFromZero);
            return true;
        }

        if (!decimal.TryParse(CellText(cells, index), NumberStyles.Number | NumberStyles.AllowCurrencySymbol, CultureInfo.InvariantCulture, out amount))
        {
            return false;
        }

        amount = Math.Round(amount, 2, MidpointRounding.AwayFromZero);
        return true;
    }

    private static string Csv(string? value)
    {
        var text = value ?? string.Empty;
        if (text.Contains('"', StringComparison.Ordinal) || text.Contains(',', StringComparison.Ordinal) || text.Contains('\n', StringComparison.Ordinal))
        {
            return "\"" + text.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
        }

        return text;
    }
}
