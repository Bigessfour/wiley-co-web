using System.IO.Compression;
using System.Security;
using System.Text;
using Microsoft.EntityFrameworkCore;
using WileyWidget.Data;
using WileyWidget.Services;

namespace WileyWidget.Tests;

public sealed class QuickBooksImportServiceTests
{
    private readonly ILoggerFactory _loggerFactory = LoggerFactory.Create(builder => { });

    [Fact]
    public async Task PreviewAsync_ParsesSparseTransactionListCsv_AndSkipsReportTitleRows()
    {
        var service = CreateService();

        var preview = await service.PreviewAsync(
            Encoding.UTF8.GetBytes(CreateSparseTransactionListCsv()),
            "transaction-list-by-date-all.csv",
            "Water Utility",
            2026);

        Assert.Equal(2, preview.TotalRows);
        Assert.False(preview.IsDuplicate);
        Assert.Equal(3, preview.Rows[0].RowNumber);
        Assert.Equal("Deposit", preview.Rows[0].EntryType);
        Assert.Equal("101 · CASH IN BANK - UTILITY", preview.Rows[0].AccountName);
        Assert.Equal("105 · ACCOUNTS RECEIVABLE", preview.Rows[0].SplitAccount);
        Assert.Equal(362.90m, preview.Rows[0].Amount);
        Assert.Equal("WATER PAYMENTS", preview.Rows[1].Name);
    }

    [Fact]
    public async Task PreviewAsync_ParsesGeneralLedgerCsv_AndCarriesSectionAccountContext()
    {
        var service = CreateService();

        var preview = await service.PreviewAsync(
            Encoding.UTF8.GetBytes(CreateGeneralLedgerCsv()),
            "general-ledger-fy2026-util.csv",
            "Water Utility",
            2026);

        Assert.Equal(1, preview.TotalRows);
        Assert.False(preview.IsDuplicate);
        Assert.Equal(5, preview.Rows[0].RowNumber);
        Assert.Equal("Deposit", preview.Rows[0].EntryType);
        Assert.Equal("101 · CASH IN BANK - UTILITY", preview.Rows[0].AccountName);
        Assert.Equal("105 · ACCOUNTS RECEIVABLE", preview.Rows[0].SplitAccount);
        Assert.Equal(362.90m, preview.Rows[0].Amount);
        Assert.Equal(88635.91m, preview.Rows[0].RunningBalance);
    }

    [Fact]
    public async Task PreviewAsync_ParsesXlsxExports_AndSkipsReportTitleRows()
    {
        var service = CreateService();

        var workbookBytes = CreateWorkbook(
            ["Date", "Type", "Num", "Name", "Memo", "Account", "Split", "Amount", "Balance", "Clr"],
            ["Jan - Dec 26"],
            ["01/01/2026", "Invoice", "1001", "Town of Wiley", "Water Billing", "Water Revenue", "Accounts Receivable", "125.00", "125.00", "C"],
            ["01/02/2026", "Payment", "1002", "Town of Wiley", "Payment Received", "Accounts Receivable", "Water Revenue", "-125.00", "0.00", "C"]);

        var preview = await service.PreviewAsync(
            workbookBytes,
            "quickbooks-ledger.xlsx",
            "Water Utility",
            2026);

        Assert.Equal(2, preview.TotalRows);
        Assert.False(preview.IsDuplicate);
        Assert.Equal("Water Billing", preview.Rows[0].Memo);
        Assert.Equal("Accounts Receivable", preview.Rows[1].AccountName);
        Assert.Equal("Water Revenue", preview.Rows[1].SplitAccount);
    }

    [Fact]
    public async Task PreviewAsync_ParsesXlsxExports_WhenQuickBooksTipsSheetComesFirst()
    {
        var service = CreateService();

        var workbookBytes = CreateWorkbook(
            ("QuickBooks Desktop Export Tips",
            [
                ["QuickBooks Desktop Export Tips"],
                ["Do not edit the exported report layout before upload."]
            ]),
            ("Sheet1",
            [
                ["", "", "", "Type", "", "Date", "", "Num", "", "Name", "", "Memo", "", "Account", "", "Clr", "", "Split", "", "Amount", "", "Balance"],
                ["Jan - Dec 26"],
                ["", "", "", "Deposit", "", "46024", "", "", "", "WATER PAYMENTS", "", "VIA CREDIT CARD", "", "105 · ACCOUNTS RECEIVABLE", "", "", "", "101 · CASH IN BANK - UTILITY", "", "-362.90", "", "0.00"]
            ]));

        var preview = await service.PreviewAsync(
            workbookBytes,
            "quickbooks-ledger.xlsx",
            "Water Utility",
            2026);

        Assert.Equal(1, preview.TotalRows);
        Assert.False(preview.IsDuplicate);
        Assert.Equal(3, preview.Rows[0].RowNumber);
        Assert.Equal("2026-01-02", preview.Rows[0].EntryDate);
        Assert.Equal("Deposit", preview.Rows[0].EntryType);
        Assert.Equal("WATER PAYMENTS", preview.Rows[0].Name);
        Assert.Equal("105 · ACCOUNTS RECEIVABLE", preview.Rows[0].AccountName);
        Assert.Equal("101 · CASH IN BANK - UTILITY", preview.Rows[0].SplitAccount);
    }

    private QuickBooksImportService CreateService()
    {
        return new QuickBooksImportService(
            _loggerFactory.CreateLogger<QuickBooksImportService>(),
            CreateContextFactory($"QuickBooksImportServiceTests-{Guid.NewGuid():N}"));
    }

    private static IDbContextFactory<AppDbContext> CreateContextFactory(string databaseName)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName)
            .EnableSensitiveDataLogging()
            .Options;

        return new AppDbContextFactory(options);
    }

    private static string CreateSparseTransactionListCsv()
    {
        return ",,,Type,,Date,,Num,,Name,,Memo,,Account,,Clr,,Split,,Amount,,Balance\n"
            + "Jan - Dec 26,,,,,,,,,,,,,,,,,,,,,\n"
            + ",,,Deposit,,01/02/2026,,,,,,Deposit,,101 · CASH IN BANK - UTILITY,,C,,105 · ACCOUNTS RECEIVABLE,,362.90,,362.90\n"
            + ",,,Deposit,,01/02/2026,,,,WATER PAYMENTS,,VIA CREDIT CARD,,105 · ACCOUNTS RECEIVABLE,,,,101 · CASH IN BANK - UTILITY,,-362.90,,0.00\n";
    }

    private static string CreateGeneralLedgerCsv()
    {
        return ",,,,,Type,,Date,,Num,,Name,,Memo,,Split,,Amount,,Balance\n"
            + ",001 · VOID CHECK,,,,,,,,,,,,,,,,,,0.00\n"
            + ",Total 001 · VOID CHECK,,,,,,,,,,,,,,,,,,0.00\n"
            + ",101 · CASH IN BANK - UTILITY,,,,,,,,,,,,,,,,,,\"88,273.01\"\n"
            + ",,,,,Deposit,,01/02/2026,,,,,,Deposit,,105 · ACCOUNTS RECEIVABLE,,362.90,,\"88,635.91\"\n"
            + ",Total 101 · CASH IN BANK - UTILITY,,,,,,,,,,,,,,,,362.90,,\"88,635.91\"\n";
    }

    private static byte[] CreateWorkbook(params string[][] rows)
    {
        return CreateWorkbook(("Sheet1", rows));
    }

    private static byte[] CreateWorkbook(params (string Name, string[][] Rows)[] worksheets)
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteEntry(
                archive,
                "[Content_Types].xml",
                BuildContentTypesXml(worksheets.Length));

            WriteEntry(
                archive,
                "_rels/.rels",
                "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>"
                + "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">"
                + "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/>"
                + "</Relationships>");

            WriteEntry(
                archive,
                "xl/workbook.xml",
                BuildWorkbookXml(worksheets));

            WriteEntry(
                archive,
                "xl/_rels/workbook.xml.rels",
                BuildWorkbookRelationshipsXml(worksheets.Length));

            for (var index = 0; index < worksheets.Length; index++)
            {
                WriteEntry(archive, $"xl/worksheets/sheet{index + 1}.xml", BuildWorksheetXml(worksheets[index].Rows));
            }
        }

        return stream.ToArray();
    }

    private static string BuildContentTypesXml(int worksheetCount)
    {
        var builder = new StringBuilder();
        builder.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
        builder.Append("<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">");
        builder.Append("<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>");
        builder.Append("<Default Extension=\"xml\" ContentType=\"application/xml\"/>");
        builder.Append("<Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/>");

        for (var index = 1; index <= worksheetCount; index++)
        {
            builder.Append($"<Override PartName=\"/xl/worksheets/sheet{index}.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>");
        }

        builder.Append("</Types>");
        return builder.ToString();
    }

    private static string BuildWorkbookXml(IEnumerable<(string Name, string[][] Rows)> worksheets)
    {
        var builder = new StringBuilder();
        builder.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
        builder.Append("<workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"><sheets>");

        var index = 1;
        foreach (var worksheet in worksheets)
        {
            builder.Append($"<sheet name=\"{SecurityElement.Escape(worksheet.Name)}\" sheetId=\"{index}\" r:id=\"rId{index}\"/>");
            index++;
        }

        builder.Append("</sheets></workbook>");
        return builder.ToString();
    }

    private static string BuildWorkbookRelationshipsXml(int worksheetCount)
    {
        var builder = new StringBuilder();
        builder.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
        builder.Append("<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">");

        for (var index = 1; index <= worksheetCount; index++)
        {
            builder.Append($"<Relationship Id=\"rId{index}\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet{index}.xml\"/>");
        }

        builder.Append("</Relationships>");
        return builder.ToString();
    }

    private static string BuildWorksheetXml(IEnumerable<string[]> rows)
    {
        var builder = new StringBuilder();
        builder.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
        builder.Append("<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><sheetData>");

        var rowNumber = 1;
        foreach (var row in rows)
        {
            builder.Append($"<row r=\"{rowNumber}\">");
            for (var columnIndex = 0; columnIndex < row.Length; columnIndex++)
            {
                if (string.IsNullOrEmpty(row[columnIndex]))
                {
                    continue;
                }

                var cellReference = $"{ToColumnName(columnIndex + 1)}{rowNumber}";
                builder.Append($"<c r=\"{cellReference}\" t=\"inlineStr\"><is><t>{SecurityElement.Escape(row[columnIndex])}</t></is></c>");
            }

            builder.Append("</row>");
            rowNumber++;
        }

        builder.Append("</sheetData></worksheet>");
        return builder.ToString();
    }

    private static string ToColumnName(int columnNumber)
    {
        var dividend = columnNumber;
        var columnName = string.Empty;

        while (dividend > 0)
        {
            var modulo = (dividend - 1) % 26;
            columnName = Convert.ToChar('A' + modulo) + columnName;
            dividend = (dividend - modulo) / 26;
        }

        return columnName;
    }

    private static void WriteEntry(ZipArchive archive, string entryName, string content)
    {
        var entry = archive.CreateEntry(entryName, CompressionLevel.NoCompression);
        using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        writer.Write(content);
    }
}