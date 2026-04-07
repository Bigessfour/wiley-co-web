using System.Text.Json;
using Syncfusion.Drawing;
using Syncfusion.Pdf;
using Syncfusion.Pdf.Graphics;
using Syncfusion.XlsIO;

namespace WileyWidget.Services;

public sealed class WorkspaceSnapshotExportArchiveService
{
    public const string CustomerWorkbookKind = "customer-workbook";
    public const string ScenarioWorkbookKind = "scenario-workbook";
    public const string WorkspacePdfKind = "workspace-pdf";

    private const string ExcelContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    private const string PdfContentType = "application/pdf";
    private const string CurrencyFormat = "$#,##0.00";
    private const string RoundedCurrencyFormat = "$#,##0";
    private const string WholeNumberFormat = "#,##0";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public static IReadOnlyList<WorkspaceSnapshotExportArtifactDocument> CreateDocuments(string snapshotPayload, IReadOnlyCollection<string>? requestedKinds = null)
    {
        if (string.IsNullOrWhiteSpace(snapshotPayload))
        {
            throw new ArgumentException("Snapshot payload is required.", nameof(snapshotPayload));
        }

        var snapshot = JsonSerializer.Deserialize<WorkspaceArchiveSnapshotData>(snapshotPayload, JsonOptions)
            ?? throw new InvalidOperationException("Snapshot payload could not be deserialized for export generation.");

        var selectedKinds = NormalizeRequestedKinds(requestedKinds);
        var documents = new List<WorkspaceSnapshotExportArtifactDocument>(selectedKinds.Count);

        foreach (var kind in selectedKinds)
        {
            documents.Add(kind switch
            {
                CustomerWorkbookKind => CreateCustomerWorkbook(snapshot),
                ScenarioWorkbookKind => CreateScenarioWorkbook(snapshot),
                WorkspacePdfKind => CreateWorkspacePdfReport(snapshot),
                _ => throw new InvalidOperationException($"Unsupported export kind '{kind}'.")
            });
        }

        return documents;
    }

    private static IReadOnlyList<string> NormalizeRequestedKinds(IReadOnlyCollection<string>? requestedKinds)
    {
        if (requestedKinds == null || requestedKinds.Count == 0)
        {
            return [CustomerWorkbookKind, ScenarioWorkbookKind, WorkspacePdfKind];
        }

        var normalized = requestedKinds
            .Where(kind => !string.IsNullOrWhiteSpace(kind))
            .Select(kind => kind.Trim().ToLowerInvariant())
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (normalized.Count == 0)
        {
            return [CustomerWorkbookKind, ScenarioWorkbookKind, WorkspacePdfKind];
        }

        return normalized;
    }

    private static WorkspaceSnapshotExportArtifactDocument CreateCustomerWorkbook(WorkspaceArchiveSnapshotData snapshot)
    {
        using var excelEngine = new ExcelEngine();
        var application = excelEngine.Excel;
        application.DefaultVersion = ExcelVersion.Xlsx;

        var workbook = application.Workbooks.Create(1);
        var worksheet = workbook.Worksheets[0];
        worksheet.Name = "Customers";

        WriteWorkbookTitle(worksheet, $"{snapshot.SelectedEnterprise} customer export", 1, 4);
        worksheet.Range[2, 1].Text = "Scenario";
        worksheet.Range[2, 2].Text = snapshot.ActiveScenarioName;
        worksheet.Range[3, 1].Text = "Fiscal Year";
        worksheet.Range[3, 2].Number = snapshot.SelectedFiscalYear;

        WriteHeaderRow(worksheet, 5, ["Name", "Service", "City Limits"]);

        var rowIndex = 6;
        foreach (var customer in snapshot.CustomerRows)
        {
            worksheet.Range[rowIndex, 1].Text = customer.Name;
            worksheet.Range[rowIndex, 2].Text = customer.Service;
            worksheet.Range[rowIndex, 3].Text = customer.CityLimits;
            rowIndex++;
        }

        worksheet.AutoFilters.FilterRange = worksheet.Range[5, 1, Math.Max(rowIndex - 1, 5), 3];
        worksheet.UsedRange.AutofitColumns();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);

        return new WorkspaceSnapshotExportArtifactDocument(
            CustomerWorkbookKind,
            $"{BuildFileStem(snapshot)}-customers.xlsx",
            ExcelContentType,
            stream.ToArray());
    }

    private static WorkspaceSnapshotExportArtifactDocument CreateScenarioWorkbook(WorkspaceArchiveSnapshotData snapshot)
    {
        using var excelEngine = new ExcelEngine();
        var application = excelEngine.Excel;
        application.DefaultVersion = ExcelVersion.Xlsx;

        var workbook = application.Workbooks.Create(2);
        var summarySheet = workbook.Worksheets[0];
        summarySheet.Name = "Summary";
        WriteWorkbookTitle(summarySheet, $"{snapshot.SelectedEnterprise} rate summary", 1, 2);
        summarySheet.Range[3, 1].Text = "Current Rate";
        summarySheet.Range[3, 2].Number = (double)(snapshot.CurrentRate ?? 0m);
        summarySheet.Range[3, 2].NumberFormat = CurrencyFormat;
        summarySheet.Range[4, 1].Text = "Break-Even Rate";
        summarySheet.Range[4, 2].Number = (double)snapshot.RecommendedRate;
        summarySheet.Range[4, 2].NumberFormat = CurrencyFormat;
        summarySheet.Range[5, 1].Text = "Scenario Adjusted Rate";
        summarySheet.Range[5, 2].Number = (double)snapshot.AdjustedRecommendedRate;
        summarySheet.Range[5, 2].NumberFormat = CurrencyFormat;
        summarySheet.Range[6, 1].Text = "Scenario Cost Total";
        summarySheet.Range[6, 2].Number = (double)snapshot.ScenarioCostTotal;
        summarySheet.Range[6, 2].NumberFormat = RoundedCurrencyFormat;
        summarySheet.Range[7, 1].Text = "Projected Volume";
        summarySheet.Range[7, 2].Number = (double)(snapshot.ProjectedVolume ?? 0m);
        summarySheet.Range[7, 2].NumberFormat = WholeNumberFormat;
        summarySheet.UsedRange.AutofitColumns();

        var scenarioSheet = workbook.Worksheets[1];
        scenarioSheet.Name = "Scenario Items";
        WriteWorkbookTitle(scenarioSheet, snapshot.ContextSummary, 1, 3);
        WriteHeaderRow(scenarioSheet, 3, ["Scenario Item", "Cost", "Cost Delta vs Current Rate"]);

        var rowIndex = 4;
        foreach (var item in snapshot.ScenarioItems)
        {
            scenarioSheet.Range[rowIndex, 1].Text = item.Name;
            scenarioSheet.Range[rowIndex, 2].Number = (double)item.Cost;
            scenarioSheet.Range[rowIndex, 2].NumberFormat = RoundedCurrencyFormat;
            scenarioSheet.Range[rowIndex, 3].Number = (double)((snapshot.CurrentRate ?? 0m) - snapshot.AdjustedRecommendedRate);
            scenarioSheet.Range[rowIndex, 3].NumberFormat = CurrencyFormat;
            rowIndex++;
        }

        scenarioSheet.AutoFilters.FilterRange = scenarioSheet.Range[3, 1, Math.Max(rowIndex - 1, 3), 3];
        scenarioSheet.UsedRange.AutofitColumns();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);

        return new WorkspaceSnapshotExportArtifactDocument(
            ScenarioWorkbookKind,
            $"{BuildFileStem(snapshot)}-scenario.xlsx",
            ExcelContentType,
            stream.ToArray());
    }

    private static WorkspaceSnapshotExportArtifactDocument CreateWorkspacePdfReport(WorkspaceArchiveSnapshotData snapshot)
    {
        using var document = new PdfDocument();
        var page = document.Pages.Add();
        var graphics = page.Graphics;
        var titleFont = new PdfStandardFont(PdfFontFamily.Helvetica, 18, PdfFontStyle.Bold);
        var sectionFont = new PdfStandardFont(PdfFontFamily.Helvetica, 12, PdfFontStyle.Bold);
        var bodyFont = new PdfStandardFont(PdfFontFamily.Helvetica, 10);
        var brush = new PdfSolidBrush(new PdfColor(15, 23, 42));
        var accentBrush = new PdfSolidBrush(new PdfColor(14, 116, 144));

        float left = 36;
        float top = 32;
        float lineHeight = 18;
        float y = top;

        graphics.DrawString("Wiley Workspace Rate Packet", titleFont, brush, new PointF(left, y));
        y += lineHeight * 1.75f;
        graphics.DrawString(snapshot.ContextSummary, sectionFont, accentBrush, new PointF(left, y));
        y += lineHeight * 1.5f;

        foreach (var line in BuildSummaryLines(snapshot))
        {
            graphics.DrawString(line, bodyFont, brush, new PointF(left, y));
            y += lineHeight;
        }

        y += lineHeight * 0.5f;
        graphics.DrawString("Scenario Items", sectionFont, accentBrush, new PointF(left, y));
        y += lineHeight;

        if (snapshot.ScenarioItems.Count == 0)
        {
            graphics.DrawString("No scenario items are currently applied.", bodyFont, brush, new PointF(left, y));
            y += lineHeight;
        }
        else
        {
            foreach (var item in snapshot.ScenarioItems.Take(8))
            {
                graphics.DrawString($"- {item.Name}: {item.Cost:C0}", bodyFont, brush, new PointF(left, y));
                y += lineHeight;
            }
        }

        y += lineHeight * 0.5f;
        graphics.DrawString("Projection Series", sectionFont, accentBrush, new PointF(left, y));
        y += lineHeight;

        foreach (var point in snapshot.ProjectionRows.Take(6))
        {
            graphics.DrawString($"- {point.Year}: {point.Rate:C2}", bodyFont, brush, new PointF(left, y));
            y += lineHeight;
        }

        using var stream = new MemoryStream();
        document.Save(stream);

        return new WorkspaceSnapshotExportArtifactDocument(
            WorkspacePdfKind,
            $"{BuildFileStem(snapshot)}-rate-packet.pdf",
            PdfContentType,
            stream.ToArray());
    }

    private static IEnumerable<string> BuildSummaryLines(WorkspaceArchiveSnapshotData snapshot)
    {
        yield return $"Current rate: {(snapshot.CurrentRate ?? 0m):C2}";
        yield return $"Break-even rate: {snapshot.RecommendedRate:C2}";
        yield return $"Adjusted break-even rate: {snapshot.AdjustedRecommendedRate:C2}";
        yield return $"Projected volume: {(snapshot.ProjectedVolume ?? 0m):N0}";
        yield return $"Scenario pressure: {snapshot.ScenarioCostTotal:C0}";
        yield return $"Visible customers: {snapshot.CustomerRows.Count}";
    }

    private static void WriteWorkbookTitle(IWorksheet worksheet, string title, int row, int columnSpan)
    {
        worksheet.Range[row, 1, row, columnSpan].Merge();
        worksheet.Range[row, 1].Text = title;
        worksheet.Range[row, 1].CellStyle.Font.Bold = true;
        worksheet.Range[row, 1].CellStyle.Font.Size = 16;
        worksheet.Range[row, 1].CellStyle.Color = Color.FromArgb(15, 23, 42);
        worksheet.Range[row, 1].CellStyle.Font.Color = ExcelKnownColors.White;
    }

    private static void WriteHeaderRow(IWorksheet worksheet, int rowIndex, IReadOnlyList<string> headers)
    {
        for (var index = 0; index < headers.Count; index++)
        {
            var cell = worksheet.Range[rowIndex, index + 1];
            cell.Text = headers[index];
            cell.CellStyle.Font.Bold = true;
            cell.CellStyle.Color = Color.FromArgb(14, 116, 144);
            cell.CellStyle.Font.Color = ExcelKnownColors.White;
        }
    }

    private static string BuildFileStem(WorkspaceArchiveSnapshotData snapshot)
    {
        var enterprise = SanitizeFileName(snapshot.SelectedEnterprise);
        return $"{enterprise}-fy{snapshot.SelectedFiscalYear}";
    }

    private static string SanitizeFileName(string value)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = new string(value
            .Trim()
            .Select(character => invalidChars.Contains(character) ? '-' : char.ToLowerInvariant(character))
            .ToArray());

        return sanitized.Replace(' ', '-');
    }

    private sealed record WorkspaceArchiveSnapshotData(
        string SelectedEnterprise,
        int SelectedFiscalYear,
        string ActiveScenarioName,
        decimal? CurrentRate,
        decimal? TotalCosts,
        decimal? ProjectedVolume)
    {
        public List<WorkspaceArchiveScenarioItemData> ScenarioItems { get; init; } = [];
        public List<WorkspaceArchiveCustomerRow> CustomerRows { get; init; } = [];
        public List<WorkspaceArchiveProjectionRow> ProjectionRows { get; init; } = [];

        public decimal ScenarioCostTotal => ScenarioItems.Sum(item => item.Cost);
        public decimal RecommendedRate => ProjectedVolume is null or 0 ? 0m : (TotalCosts ?? 0m) / ProjectedVolume.Value;
        public decimal AdjustedRecommendedRate => ProjectedVolume is null or 0 ? 0m : ((TotalCosts ?? 0m) + ScenarioCostTotal) / ProjectedVolume.Value;
        public string ContextSummary => $"{SelectedEnterprise} FY {SelectedFiscalYear} | {ActiveScenarioName}";
    }

    private sealed record WorkspaceArchiveScenarioItemData(Guid Id, string Name, decimal Cost);
    private sealed record WorkspaceArchiveCustomerRow(string Name, string Service, string CityLimits);
    private sealed record WorkspaceArchiveProjectionRow(string Year, decimal Rate);
}

public sealed record WorkspaceSnapshotExportArtifactDocument(string DocumentKind, string FileName, string ContentType, byte[] Content);