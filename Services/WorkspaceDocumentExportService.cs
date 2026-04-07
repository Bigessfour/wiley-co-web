using Syncfusion.Drawing;
using Syncfusion.Pdf;
using Syncfusion.Pdf.Graphics;
using Syncfusion.XlsIO;
using WileyCoWeb.State;

namespace WileyCoWeb.Services;

public sealed class WorkspaceDocumentExportService
{
    private const string ExcelContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    private const string PdfContentType = "application/pdf";

    public WorkspaceExportDocument CreateCustomerWorkbook(WorkspaceState workspaceState)
    {
        ArgumentNullException.ThrowIfNull(workspaceState);

        using var excelEngine = new ExcelEngine();
        var application = excelEngine.Excel;
        application.DefaultVersion = ExcelVersion.Xlsx;

        var workbook = application.Workbooks.Create(1);
        var worksheet = workbook.Worksheets[0];
        worksheet.Name = "Customers";

        WriteWorkbookTitle(worksheet, $"{workspaceState.SelectedEnterprise} customer export", 1, 4);
        worksheet.Range[2, 1].Text = "Scenario";
        worksheet.Range[2, 2].Text = workspaceState.ActiveScenarioName;
        worksheet.Range[3, 1].Text = "Fiscal Year";
        worksheet.Range[3, 2].Number = workspaceState.SelectedFiscalYear;

        WriteHeaderRow(worksheet, 5, ["Name", "Service", "City Limits"]);

        var rowIndex = 6;
        foreach (var customer in workspaceState.FilteredCustomers)
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

        return new WorkspaceExportDocument(
            $"{BuildFileStem(workspaceState)}-customers.xlsx",
            ExcelContentType,
            stream.ToArray());
    }

    public WorkspaceExportDocument CreateScenarioWorkbook(WorkspaceState workspaceState)
    {
        ArgumentNullException.ThrowIfNull(workspaceState);

        using var excelEngine = new ExcelEngine();
        var application = excelEngine.Excel;
        application.DefaultVersion = ExcelVersion.Xlsx;

        var workbook = application.Workbooks.Create(2);
        var summarySheet = workbook.Worksheets[0];
        summarySheet.Name = "Summary";
        WriteWorkbookTitle(summarySheet, $"{workspaceState.SelectedEnterprise} rate summary", 1, 2);
        summarySheet.Range[3, 1].Text = "Current Rate";
        summarySheet.Range[3, 2].Number = (double)workspaceState.CurrentRate;
        summarySheet.Range[3, 2].NumberFormat = "$#,##0.00";
        summarySheet.Range[4, 1].Text = "Break-Even Rate";
        summarySheet.Range[4, 2].Number = (double)workspaceState.RecommendedRate;
        summarySheet.Range[4, 2].NumberFormat = "$#,##0.00";
        summarySheet.Range[5, 1].Text = "Scenario Adjusted Rate";
        summarySheet.Range[5, 2].Number = (double)workspaceState.AdjustedRecommendedRate;
        summarySheet.Range[5, 2].NumberFormat = "$#,##0.00";
        summarySheet.Range[6, 1].Text = "Scenario Cost Total";
        summarySheet.Range[6, 2].Number = (double)workspaceState.ScenarioCostTotal;
        summarySheet.Range[6, 2].NumberFormat = "$#,##0";
        summarySheet.Range[7, 1].Text = "Projected Volume";
        summarySheet.Range[7, 2].Number = (double)workspaceState.ProjectedVolume;
        summarySheet.Range[7, 2].NumberFormat = "#,##0";
        summarySheet.UsedRange.AutofitColumns();

        var scenarioSheet = workbook.Worksheets[1];
        scenarioSheet.Name = "Scenario Items";
        WriteWorkbookTitle(scenarioSheet, workspaceState.ContextSummary, 1, 3);
        WriteHeaderRow(scenarioSheet, 3, ["Scenario Item", "Cost", "Cost Delta vs Current Rate"]);

        var rowIndex = 4;
        foreach (var item in workspaceState.ScenarioItems)
        {
            scenarioSheet.Range[rowIndex, 1].Text = item.Name;
            scenarioSheet.Range[rowIndex, 2].Number = (double)item.Cost;
            scenarioSheet.Range[rowIndex, 2].NumberFormat = "$#,##0";
            scenarioSheet.Range[rowIndex, 3].Number = (double)(workspaceState.CurrentRate - workspaceState.AdjustedRecommendedRate);
            scenarioSheet.Range[rowIndex, 3].NumberFormat = "$#,##0.00";
            rowIndex++;
        }

        scenarioSheet.AutoFilters.FilterRange = scenarioSheet.Range[3, 1, Math.Max(rowIndex - 1, 3), 3];
        scenarioSheet.UsedRange.AutofitColumns();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);

        return new WorkspaceExportDocument(
            $"{BuildFileStem(workspaceState)}-scenario.xlsx",
            ExcelContentType,
            stream.ToArray());
    }

    public WorkspaceExportDocument CreateWorkspacePdfReport(WorkspaceState workspaceState)
    {
        ArgumentNullException.ThrowIfNull(workspaceState);

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
        graphics.DrawString(workspaceState.ContextSummary, sectionFont, accentBrush, new PointF(left, y));
        y += lineHeight * 1.5f;

        foreach (var line in BuildSummaryLines(workspaceState))
        {
            graphics.DrawString(line, bodyFont, brush, new PointF(left, y));
            y += lineHeight;
        }

        y += lineHeight * 0.5f;
        graphics.DrawString("Scenario Items", sectionFont, accentBrush, new PointF(left, y));
        y += lineHeight;

        if (workspaceState.ScenarioItems.Count == 0)
        {
            graphics.DrawString("No scenario items are currently applied.", bodyFont, brush, new PointF(left, y));
            y += lineHeight;
        }
        else
        {
            foreach (var item in workspaceState.ScenarioItems.Take(8))
            {
                graphics.DrawString($"- {item.Name}: {item.Cost:C0}", bodyFont, brush, new PointF(left, y));
                y += lineHeight;
            }
        }

        y += lineHeight * 0.5f;
        graphics.DrawString("Projection Series", sectionFont, accentBrush, new PointF(left, y));
        y += lineHeight;

        foreach (var point in workspaceState.ProjectionSeries.Take(6))
        {
            graphics.DrawString($"- {point.Year}: {point.Rate:C2}", bodyFont, brush, new PointF(left, y));
            y += lineHeight;
        }

        using var stream = new MemoryStream();
        document.Save(stream);

        return new WorkspaceExportDocument(
            $"{BuildFileStem(workspaceState)}-rate-packet.pdf",
            PdfContentType,
            stream.ToArray());
    }

    private static IEnumerable<string> BuildSummaryLines(WorkspaceState workspaceState)
    {
        yield return $"Current rate: {workspaceState.CurrentRate:C2}";
        yield return $"Break-even rate: {workspaceState.RecommendedRate:C2}";
        yield return $"Adjusted break-even rate: {workspaceState.AdjustedRecommendedRate:C2}";
        yield return $"Projected volume: {workspaceState.ProjectedVolume:N0}";
        yield return $"Scenario pressure: {workspaceState.ScenarioCostTotal:C0}";
        yield return $"Visible customers: {workspaceState.FilteredCustomerCount}";
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

    private static string BuildFileStem(WorkspaceState workspaceState)
    {
        var enterprise = SanitizeFileName(workspaceState.SelectedEnterprise);
        return $"{enterprise}-fy{workspaceState.SelectedFiscalYear}";
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
}

public sealed record WorkspaceExportDocument(string FileName, string ContentType, byte[] Content);