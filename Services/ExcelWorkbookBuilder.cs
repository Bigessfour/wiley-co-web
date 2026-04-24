using Syncfusion.Drawing;
using Syncfusion.XlsIO;
using WileyCoWeb.Contracts;
using WileyCoWeb.State;

namespace WileyCoWeb.Services;

public sealed class ExcelWorkbookBuilder
{
    private const string ExcelContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    private const string CurrencyNumberFormat = "$#,##0.00";

    public WorkspaceExportDocument CreateCustomerWorkbook(WorkspaceState workspaceState)
    {
        return CreateCustomerWorkbookCore(workspaceState);
    }

    public WorkspaceExportDocument CreateScenarioWorkbook(WorkspaceState workspaceState)
    {
        return CreateScenarioWorkbookCore(workspaceState);
    }

    public WorkspaceExportDocument CreateReserveTrajectoryWorkbook(WorkspaceState workspaceState)
    {
        return CreateReserveTrajectoryWorkbookCore(workspaceState);
    }

    private static ExcelEngine CreateExcelEngine()
    {
        var excelEngine = new ExcelEngine();
        excelEngine.Excel.DefaultVersion = ExcelVersion.Xlsx;
        return excelEngine;
    }

    private WorkspaceExportDocument CreateCustomerWorkbookCore(WorkspaceState workspaceState)
    {
        ArgumentNullException.ThrowIfNull(workspaceState);

        using var excelEngine = CreateExcelEngine();
        var workbook = excelEngine.Excel.Workbooks.Create(1);
        var worksheet = workbook.Worksheets[0];
        worksheet.Name = "Customers";

        PopulateCustomerWorksheet(worksheet, workspaceState);
        return CreateWorkbookExport(workbook, $"{BuildFileStem(workspaceState)}-customers.xlsx");
    }

    private WorkspaceExportDocument CreateScenarioWorkbookCore(WorkspaceState workspaceState)
    {
        ArgumentNullException.ThrowIfNull(workspaceState);

        using var excelEngine = CreateExcelEngine();
        var workbook = excelEngine.Excel.Workbooks.Create(2);

        var summarySheet = workbook.Worksheets[0];
        summarySheet.Name = "Summary";
        PopulateScenarioSummaryWorksheet(summarySheet, workspaceState);

        var scenarioSheet = workbook.Worksheets[1];
        scenarioSheet.Name = "Scenario Items";
        PopulateScenarioItemsWorksheet(scenarioSheet, workspaceState);

        return CreateWorkbookExport(workbook, $"{BuildFileStem(workspaceState)}-scenario.xlsx");
    }

    private WorkspaceExportDocument CreateReserveTrajectoryWorkbookCore(WorkspaceState workspaceState)
    {
        ArgumentNullException.ThrowIfNull(workspaceState);

        using var excelEngine = CreateExcelEngine();
        var workbook = excelEngine.Excel.Workbooks.Create(2);

        var summarySheet = workbook.Worksheets[0];
        summarySheet.Name = "Overview";
        PopulateReserveTrajectorySummaryWorksheet(summarySheet, workspaceState);

        var forecastSheet = workbook.Worksheets[1];
        forecastSheet.Name = "Forecast";
        PopulateReserveTrajectoryForecastWorksheet(forecastSheet, workspaceState);

        return CreateWorkbookExport(workbook, $"{BuildFileStem(workspaceState)}-reserve-trajectory.xlsx");
    }

    private static void PopulateCustomerWorksheet(IWorksheet worksheet, WorkspaceState workspaceState)
    {
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
    }

    private static void PopulateScenarioSummaryWorksheet(IWorksheet summarySheet, WorkspaceState workspaceState)
    {
        WriteWorkbookTitle(summarySheet, $"{workspaceState.SelectedEnterprise} rate summary", 1, 2);
        summarySheet.Range[3, 1].Text = "Current Rate";
        summarySheet.Range[3, 2].Number = (double)workspaceState.CurrentRate;
        summarySheet.Range[3, 2].NumberFormat = CurrencyNumberFormat;
        summarySheet.Range[4, 1].Text = "Break-Even Rate";
        summarySheet.Range[4, 2].Number = (double)workspaceState.RecommendedRate;
        summarySheet.Range[4, 2].NumberFormat = CurrencyNumberFormat;
        summarySheet.Range[5, 1].Text = "Scenario Adjusted Rate";
        summarySheet.Range[5, 2].Number = (double)workspaceState.AdjustedRecommendedRate;
        summarySheet.Range[5, 2].NumberFormat = CurrencyNumberFormat;
        summarySheet.Range[6, 1].Text = "Scenario Cost Total";
        summarySheet.Range[6, 2].Number = (double)workspaceState.ScenarioCostTotal;
        summarySheet.Range[6, 2].NumberFormat = "$#,##0";
        summarySheet.Range[7, 1].Text = "Projected Volume";
        summarySheet.Range[7, 2].Number = (double)workspaceState.ProjectedVolume;
        summarySheet.Range[7, 2].NumberFormat = "#,##0";
        summarySheet.UsedRange.AutofitColumns();
    }

    private static void PopulateScenarioItemsWorksheet(IWorksheet scenarioSheet, WorkspaceState workspaceState)
    {
        WriteWorkbookTitle(scenarioSheet, workspaceState.ContextSummary, 1, 3);
        WriteHeaderRow(scenarioSheet, 3, ["Scenario Item", "Cost", "Cost Delta vs Current Rate"]);

        var rowIndex = 4;
        foreach (var item in workspaceState.ScenarioItems)
        {
            scenarioSheet.Range[rowIndex, 1].Text = item.Name;
            scenarioSheet.Range[rowIndex, 2].Number = (double)item.Cost;
            scenarioSheet.Range[rowIndex, 2].NumberFormat = "$#,##0";
            scenarioSheet.Range[rowIndex, 3].Number = (double)(workspaceState.CurrentRate - workspaceState.AdjustedRecommendedRate);
            scenarioSheet.Range[rowIndex, 3].NumberFormat = CurrencyNumberFormat;
            rowIndex++;
        }

        scenarioSheet.AutoFilters.FilterRange = scenarioSheet.Range[3, 1, Math.Max(rowIndex - 1, 3), 3];
        scenarioSheet.UsedRange.AutofitColumns();
    }

    private static void PopulateReserveTrajectorySummaryWorksheet(IWorksheet summarySheet, WorkspaceState workspaceState)
    {
        WriteWorkbookTitle(summarySheet, $"{workspaceState.SelectedEnterprise} reserve trajectory", 1, 2);
        summarySheet.Range[3, 1].Text = "Current Reserves";
        summarySheet.Range[3, 2].Number = (double)(workspaceState.ReserveTrajectory?.CurrentReserves ?? 0m);
        summarySheet.Range[3, 2].NumberFormat = CurrencyNumberFormat;
        summarySheet.Range[4, 1].Text = "Recommended Reserve Level";
        summarySheet.Range[4, 2].Number = (double)(workspaceState.ReserveTrajectory?.RecommendedReserveLevel ?? 0m);
        summarySheet.Range[4, 2].NumberFormat = CurrencyNumberFormat;
        summarySheet.Range[5, 1].Text = "Risk Assessment";
        summarySheet.Range[5, 2].Text = workspaceState.ReserveTrajectory?.RiskAssessment ?? "Unavailable";
        summarySheet.Range[6, 1].Text = "Forecast Points";
        summarySheet.Range[6, 2].Number = workspaceState.ReserveTrajectory?.ForecastPoints.Count ?? 0;
        summarySheet.UsedRange.AutofitColumns();
    }

    private static void PopulateReserveTrajectoryForecastWorksheet(IWorksheet forecastSheet, WorkspaceState workspaceState)
    {
        WriteWorkbookTitle(forecastSheet, $"{workspaceState.SelectedEnterprise} reserve forecast", 1, 4);
        WriteHeaderRow(forecastSheet, 3, ["Date", "Projected Reserves", "Confidence Interval", "Policy Floor"]);

        var forecastPoints = workspaceState.ReserveTrajectory?.ForecastPoints ?? [];
        var rowIndex = 4;

        foreach (var point in forecastPoints)
        {
            forecastSheet.Range[rowIndex, 1].DateTime = point.DateUtc;
            forecastSheet.Range[rowIndex, 1].NumberFormat = "mmm yy";
            forecastSheet.Range[rowIndex, 2].Number = (double)point.PredictedReserves;
            forecastSheet.Range[rowIndex, 2].NumberFormat = CurrencyNumberFormat;
            forecastSheet.Range[rowIndex, 3].Number = (double)point.ConfidenceInterval;
            forecastSheet.Range[rowIndex, 3].NumberFormat = CurrencyNumberFormat;
            forecastSheet.Range[rowIndex, 4].Number = (double)(workspaceState.ReserveTrajectory?.RecommendedReserveLevel ?? 0m);
            forecastSheet.Range[rowIndex, 4].NumberFormat = CurrencyNumberFormat;
            rowIndex++;
        }

        forecastSheet.AutoFilters.FilterRange = forecastSheet.Range[3, 1, Math.Max(rowIndex - 1, 3), 4];
        forecastSheet.UsedRange.AutofitColumns();
    }

    private static WorkspaceExportDocument CreateWorkbookExport(IWorkbook workbook, string fileName)
    {
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);

        return new WorkspaceExportDocument(fileName, ExcelContentType, stream.ToArray());
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