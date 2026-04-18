using Syncfusion.Pdf.Parsing;
using Syncfusion.XlsIO;
using WileyCoWeb.Contracts;
using WileyCoWeb.Services;
using WileyCoWeb.State;

namespace WileyCoWeb.ComponentTests;

public sealed class WorkspaceExportServiceTests
{
    [Fact]
    public void CreateCustomerWorkbook_WritesFilteredCustomerRows_AndExportMetadata()
    {
        var state = BuildWorkspaceState();
        state.SetCustomerServiceFilter("Water");
        var service = new WorkspaceDocumentExportService();

        var customerWorkbook = service.CreateCustomerWorkbook(state);

        Assert.EndsWith(".xlsx", customerWorkbook.FileName);
        Assert.Equal("water-utility-fy2026-customers.xlsx", customerWorkbook.FileName);
        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", customerWorkbook.ContentType);
        Assert.Equal("PK", System.Text.Encoding.ASCII.GetString(customerWorkbook.Content, 0, 2));

        AssertWorkbook(customerWorkbook.Content, workbook =>
        {
            var worksheet = workbook.Worksheets[0];

            Assert.Equal("Water Utility customer export", worksheet.Range[1, 1].Text);
            Assert.Equal("Scenario", worksheet.Range[2, 1].Text);
            Assert.Equal(WorkspaceTestData.CouncilReviewScenario, worksheet.Range[2, 2].Text);
            Assert.Equal("Fiscal Year", worksheet.Range[3, 1].Text);
            Assert.Equal(WorkspaceTestData.WaterFiscalYear.ToString(), worksheet.Range[3, 2].DisplayText);
            Assert.Equal("Name", worksheet.Range[5, 1].Text);
            Assert.Equal("Service", worksheet.Range[5, 2].Text);
            Assert.Equal("City Limits", worksheet.Range[5, 3].Text);
            Assert.Equal("North Plant", worksheet.Range[6, 1].Text);
            Assert.Equal("Water", worksheet.Range[6, 2].Text);
            Assert.Equal("Yes", worksheet.Range[6, 3].Text);

            var exportedCustomerNames = Enumerable.Range(6, Math.Max(worksheet.UsedRange.LastRow - 5, 1))
                .Select(rowIndex => worksheet.Range[rowIndex, 1].Text)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Where(name => !name.StartsWith("Created with a trial version of Syncfusion", StringComparison.Ordinal))
                .ToArray();

            Assert.Equal(["North Plant"], exportedCustomerNames);
        });
    }

    [Fact]
    public void CreateScenarioWorkbook_WritesScenarioSummary_AndScenarioRows()
    {
        var state = BuildWorkspaceState();
        var service = new WorkspaceDocumentExportService();

        var scenarioWorkbook = service.CreateScenarioWorkbook(state);

        Assert.EndsWith(".xlsx", scenarioWorkbook.FileName);
        Assert.Equal("water-utility-fy2026-scenario.xlsx", scenarioWorkbook.FileName);
        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", scenarioWorkbook.ContentType);
        Assert.Equal("PK", System.Text.Encoding.ASCII.GetString(scenarioWorkbook.Content, 0, 2));

        AssertWorkbook(scenarioWorkbook.Content, workbook =>
        {
            var summarySheet = workbook.Worksheets[0];
            var scenarioSheet = workbook.Worksheets[1];

            Assert.Equal("Water Utility rate summary", summarySheet.Range[1, 1].Text);
            Assert.Equal("Current Rate", summarySheet.Range[3, 1].Text);
            Assert.Equal((double)WorkspaceTestData.WaterCurrentRate, summarySheet.Range[3, 2].Number, 10);
            Assert.Equal("$#,##0.00", summarySheet.Range[3, 2].NumberFormat);
            Assert.Equal("Scenario Adjusted Rate", summarySheet.Range[5, 1].Text);
            Assert.Equal((double)state.AdjustedRecommendedRate, summarySheet.Range[5, 2].Number, 10);
            Assert.Equal("$#,##0.00", summarySheet.Range[5, 2].NumberFormat);
            Assert.Equal("Scenario Cost Total", summarySheet.Range[6, 1].Text);
            Assert.Equal((double)state.ScenarioCostTotal, summarySheet.Range[6, 2].Number);
            Assert.Equal("$#,##0", summarySheet.Range[6, 2].NumberFormat);

            Assert.Equal(state.ContextSummary, scenarioSheet.Range[1, 1].Text);
            Assert.Equal("Scenario Item", scenarioSheet.Range[3, 1].Text);
            Assert.Equal("Cost", scenarioSheet.Range[3, 2].Text);
            Assert.Equal("Cost Delta vs Current Rate", scenarioSheet.Range[3, 3].Text);
            Assert.Equal("Vehicle replacement", scenarioSheet.Range[4, 1].Text);
            Assert.Equal("Reserve transfer", scenarioSheet.Range[5, 1].Text);
            Assert.Equal(18000d, scenarioSheet.Range[4, 2].Number);
            Assert.Equal(6200d, scenarioSheet.Range[5, 2].Number);
            Assert.Equal((double)(state.CurrentRate - state.AdjustedRecommendedRate), scenarioSheet.Range[4, 3].Number, 10);
            Assert.Equal((double)(state.CurrentRate - state.AdjustedRecommendedRate), scenarioSheet.Range[5, 3].Number, 10);
        });
    }

    [Fact]
    public void CreateWorkspacePdfReport_WritesWorkspaceSummaryContent()
    {
        var state = BuildWorkspaceState();
        var service = new WorkspaceDocumentExportService();

        var pdfReport = service.CreateWorkspacePdfReport(state);

        Assert.EndsWith(".pdf", pdfReport.FileName);
        Assert.Equal("water-utility-fy2026-rate-packet.pdf", pdfReport.FileName);
        Assert.Equal("application/pdf", pdfReport.ContentType);
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(pdfReport.Content, 0, 4));
        Assert.True(pdfReport.Content.Length > 1000);

        using var stream = new MemoryStream(pdfReport.Content, writable: false);
        using var document = new PdfLoadedDocument(stream);
        Assert.Equal(1, document.Pages.Count);
    }

    private static void AssertWorkbook(byte[] workbookBytes, Action<IWorkbook> assertion)
    {
        using var excelEngine = new ExcelEngine();
        var application = excelEngine.Excel;
        application.DefaultVersion = ExcelVersion.Xlsx;

        using var stream = new MemoryStream(workbookBytes, writable: false);
        var workbook = application.Workbooks.Open(stream);
        assertion(workbook);
        workbook.Close();
    }

    private static WorkspaceState BuildWorkspaceState()
    {
        var state = new WorkspaceState();
        state.ApplyBootstrap(WorkspaceTestData.CreateWaterUtilityBootstrap(
            WorkspaceTestData.CouncilReviewScenario,
            WorkspaceTestData.WaterCurrentRate,
            WorkspaceTestData.WaterTotalCosts,
            WorkspaceTestData.WaterProjectedVolume,
            DateTime.UtcNow.ToString("O"),
            scenarioItems: [
                new WorkspaceScenarioItemData(Guid.NewGuid(), "Vehicle replacement", 18000m),
                new WorkspaceScenarioItemData(Guid.NewGuid(), "Reserve transfer", 6200m)
            ],
            customerRows: [
                new CustomerRow("North Plant", "Water", "Yes"),
                new CustomerRow("South Lift", "Sewer", "No")
            ],
            projectionRows: [
                new ProjectionRow("2024", 48.10m),
                new ProjectionRow("2025", 51.40m),
                new ProjectionRow("2026", WorkspaceTestData.WaterCurrentRate)
            ]));

        return state;
    }
}
