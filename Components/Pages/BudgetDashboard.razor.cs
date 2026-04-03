using System.Globalization;
using System.IO.Compression;
using System.Security;
using System.Text;
using Microsoft.AspNetCore.Components;

namespace WileyCoWeb.Components.Pages;

public partial class BudgetDashboardBase : ComponentBase
{
    protected readonly List<string> GridToolbarItems = new() { "ExcelExport" };
    protected readonly List<BudgetLineItem> LineItems = new();
    protected readonly List<BudgetWaterfallPoint> WaterfallPoints = new();
    protected readonly double[] IntermediateSumIndexes = { 4 };
    protected readonly double[] SumIndexes = { 7 };
    protected byte[] SpreadsheetBytes = Array.Empty<byte>();

    protected decimal TotalBudget { get; set; }
    protected decimal ActualSpend { get; set; }
    protected decimal Variance => ActualSpend - TotalBudget;
    protected decimal Remaining => TotalBudget - ActualSpend;
    protected string TotalBudgetDisplay => TotalBudget.ToString("C0", CultureInfo.CurrentCulture);
    protected string ActualSpendDisplay => ActualSpend.ToString("C0", CultureInfo.CurrentCulture);
    protected string VarianceDisplay => Variance.ToString("C0", CultureInfo.CurrentCulture);
    protected string RemainingDisplay => Remaining.ToString("C0", CultureInfo.CurrentCulture);

    protected override void OnInitialized()
    {
        LineItems.AddRange(new[]
        {
            new BudgetLineItem("Personnel", "Operations", "A. Morgan", 90000m, 88500m, "On Track"),
            new BudgetLineItem("Product Design", "Product", "L. Kim", 28000m, 27600m, "On Track"),
            new BudgetLineItem("Cloud Infrastructure", "Engineering", "J. Patel", 34000m, 36100m, "Over"),
            new BudgetLineItem("Marketing Campaigns", "Growth", "M. Reyes", 42000m, 35500m, "Under"),
            new BudgetLineItem("Sales Travel", "Sales", "S. Bennett", 12000m, 9800m, "Under"),
            new BudgetLineItem("Operations", "Finance", "D. Brown", 21000m, 18850m, "Under"),
            new BudgetLineItem("Legal & Compliance", "Legal", "R. Alvarez", 9000m, 7600m, "Under"),
            new BudgetLineItem("Training & Hiring", "People", "N. Foster", 9000m, 11900m, "Over")
        });

        TotalBudget = LineItems.Sum(item => item.Budget);
        ActualSpend = LineItems.Sum(item => item.Actual);

        WaterfallPoints.AddRange(new[]
        {
            new BudgetWaterfallPoint { Stage = "Approved Budget", Amount = (double)TotalBudget },
            new BudgetWaterfallPoint { Stage = "Personnel", Amount = (double)(LineItems[0].Actual - LineItems[0].Budget) },
            new BudgetWaterfallPoint { Stage = "Product & Design", Amount = (double)(LineItems[1].Actual - LineItems[1].Budget) },
            new BudgetWaterfallPoint { Stage = "Infrastructure", Amount = (double)(LineItems[2].Actual - LineItems[2].Budget) },
            new BudgetWaterfallPoint { Stage = "Commercial", Amount = (double)(LineItems[3].Actual - LineItems[3].Budget) },
            new BudgetWaterfallPoint { Stage = "Operating Cost", Amount = (double)(LineItems[4].Actual - LineItems[4].Budget) },
            new BudgetWaterfallPoint { Stage = "Optimization", Amount = (double)(LineItems[5].Actual - LineItems[5].Budget) },
            new BudgetWaterfallPoint { Stage = "Actual Spend", Amount = (double)ActualSpend }
        });

        SpreadsheetBytes = CreateBudgetSpreadsheetBytes();
    }

    private byte[] CreateBudgetSpreadsheetBytes()
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, true))
        {
            AddEntry(archive, "[Content_Types].xml", $$"""
<?xml version="1.0" encoding="utf-8"?>
<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
  <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml" />
  <Default Extension="xml" ContentType="application/xml" />
  <Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml" />
  <Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml" />
  <Override PartName="/xl/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml" />
</Types>
""");

            AddEntry(archive, "_rels/.rels", $$"""
<?xml version="1.0" encoding="utf-8"?>
<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
  <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml" />
</Relationships>
""");

            AddEntry(archive, "xl/workbook.xml", $$"""
<?xml version="1.0" encoding="utf-8"?>
<workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
  <sheets>
    <sheet name="Budget" sheetId="1" r:id="rId1" />
  </sheets>
</workbook>
""");

            AddEntry(archive, "xl/_rels/workbook.xml.rels", $$"""
<?xml version="1.0" encoding="utf-8"?>
<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
  <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml" />
  <Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" Target="styles.xml" />
</Relationships>
""");

            AddEntry(archive, "xl/styles.xml", $$"""
<?xml version="1.0" encoding="utf-8"?>
<styleSheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">
  <fonts count="1">
    <font>
      <sz val="11" />
      <color theme="1" />
      <name val="Calibri" />
      <family val="2" />
    </font>
  </fonts>
  <fills count="1">
    <fill>
      <patternFill patternType="none" />
    </fill>
  </fills>
  <borders count="1">
    <border>
      <left />
      <right />
      <top />
      <bottom />
      <diagonal />
    </border>
  </borders>
  <cellStyleXfs count="1">
    <xf numFmtId="0" fontId="0" fillId="0" borderId="0" />
  </cellStyleXfs>
  <cellXfs count="1">
    <xf numFmtId="0" fontId="0" fillId="0" borderId="0" xfId="0" />
  </cellXfs>
</styleSheet>
""");

            AddEntry(archive, "xl/worksheets/sheet1.xml", BuildWorksheetXml());
        }

        return stream.ToArray();
    }

    private string BuildWorksheetXml()
    {
        var rows = new List<string>
        {
            BuildInlineStringRow(1, "Wiley.co Budget Planning Workbook", "FY 2026 editable starter data"),
            BuildInlineStringRow(3, "Category", "Department", "Owner", "Budget", "Actual", "Variance", "Status")
        };

        var rowIndex = 4;
        foreach (var item in LineItems)
        {
            rows.Add(BuildMixedRow(rowIndex++, item.Category, item.Department, item.Owner, item.Budget, item.Actual, item.Variance, item.Status));
        }

        rows.Add(BuildMixedRow(rowIndex, "Total", string.Empty, string.Empty, TotalBudget, ActualSpend, Variance, Remaining >= 0 ? "Under Budget" : "Over Budget"));

        return $$"""
<?xml version="1.0" encoding="utf-8"?>
<worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">
  <sheetData>
    {{string.Join("\n    ", rows)}}
  </sheetData>
</worksheet>
""";
    }

    private static string BuildInlineStringRow(int rowNumber, params string[] values)
    {
        var cells = new StringBuilder();

        for (var index = 0; index < values.Length; index++)
        {
            var column = GetColumnName(index + 1);
            cells.Append($"<c r=\"{column}{rowNumber}\" t=\"inlineStr\"><is><t>{EscapeXml(values[index])}</t></is></c>");
        }

        return $"<row r=\"{rowNumber}\">{cells}</row>";
    }

    private static string BuildMixedRow(int rowNumber, string category, string department, string owner, decimal budget, decimal actual, decimal variance, string status)
    {
        return $"<row r=\"{rowNumber}\">" +
               $"<c r=\"A{rowNumber}\" t=\"inlineStr\"><is><t>{EscapeXml(category)}</t></is></c>" +
               $"<c r=\"B{rowNumber}\" t=\"inlineStr\"><is><t>{EscapeXml(department)}</t></is></c>" +
               $"<c r=\"C{rowNumber}\" t=\"inlineStr\"><is><t>{EscapeXml(owner)}</t></is></c>" +
               $"<c r=\"D{rowNumber}\"><v>{budget.ToString(CultureInfo.InvariantCulture)}</v></c>" +
               $"<c r=\"E{rowNumber}\"><v>{actual.ToString(CultureInfo.InvariantCulture)}</v></c>" +
               $"<c r=\"F{rowNumber}\"><v>{variance.ToString(CultureInfo.InvariantCulture)}</v></c>" +
               $"<c r=\"G{rowNumber}\" t=\"inlineStr\"><is><t>{EscapeXml(status)}</t></is></c>" +
               "</row>";
    }

    private static string GetColumnName(int index)
    {
        var dividend = index;
        var columnName = string.Empty;

        while (dividend > 0)
        {
            var modulo = (dividend - 1) % 26;
            columnName = Convert.ToChar('A' + modulo) + columnName;
            dividend = (dividend - modulo) / 26;
        }

        return columnName;
    }

    private static string EscapeXml(string value) => SecurityElement.Escape(value) ?? string.Empty;

    private static void AddEntry(ZipArchive archive, string entryPath, string content)
    {
        var entry = archive.CreateEntry(entryPath, CompressionLevel.Optimal);
        using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
        writer.Write(content);
    }

    protected sealed record BudgetLineItem(string Category, string Department, string Owner, decimal Budget, decimal Actual, string Status)
    {
        public decimal Variance => Actual - Budget;
    }

    protected sealed class BudgetWaterfallPoint
    {
        public string Stage { get; set; } = string.Empty;
        public double Amount { get; set; }
    }
}
