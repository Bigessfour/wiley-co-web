using Syncfusion.Pdf;
using Syncfusion.Pdf.Parsing;
using Syncfusion.XlsIO;
using WileyCoWeb.Contracts;
using WileyCoWeb.Services;
using WileyCoWeb.State;

namespace WileyCoWeb.ComponentTests;

public sealed class UnitTest1
{
    [Fact]
    public void Syncfusion_Exports_ReturnExpectedExcelAndPdfPayloads()
    {
        var state = WorkspaceExportTestHelpers.BuildWorkspaceState();
        var service = new WorkspaceDocumentExportService();

        var customerWorkbook = service.CreateCustomerWorkbook(state);
        Assert.EndsWith(".xlsx", customerWorkbook.FileName);
        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", customerWorkbook.ContentType);
        Assert.Equal("PK", System.Text.Encoding.ASCII.GetString(customerWorkbook.Content, 0, 2));

        var scenarioWorkbook = service.CreateScenarioWorkbook(state);
        Assert.EndsWith(".xlsx", scenarioWorkbook.FileName);
        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", scenarioWorkbook.ContentType);
        Assert.Equal("PK", System.Text.Encoding.ASCII.GetString(scenarioWorkbook.Content, 0, 2));

        var pdfReport = service.CreateWorkspacePdfReport(state);
        Assert.EndsWith(".pdf", pdfReport.FileName);
        Assert.Equal("application/pdf", pdfReport.ContentType);
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(pdfReport.Content, 0, 4));
    }
}

public sealed class WorkspaceExportServiceTests
{
    [Fact]
    public void CreateCustomerWorkbook_WritesFilteredCustomerRows_AndExportMetadata()
    {
        var state = WorkspaceExportTestHelpers.BuildWorkspaceState();
        state.SetCustomerServiceFilter("Water");
        var service = new WorkspaceDocumentExportService();

        var customerWorkbook = service.CreateCustomerWorkbook(state);

        Assert.EndsWith(".xlsx", customerWorkbook.FileName);
        Assert.Equal("water-utility-fy2026-customers.xlsx", customerWorkbook.FileName);
        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", customerWorkbook.ContentType);
        Assert.Equal("PK", System.Text.Encoding.ASCII.GetString(customerWorkbook.Content, 0, 2));

        WorkspaceExportTestHelpers.AssertWorkbook(customerWorkbook.Content, workbook =>
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
        var state = WorkspaceExportTestHelpers.BuildWorkspaceState();
        var service = new WorkspaceDocumentExportService();

        var scenarioWorkbook = service.CreateScenarioWorkbook(state);

        Assert.EndsWith(".xlsx", scenarioWorkbook.FileName);
        Assert.Equal("water-utility-fy2026-scenario.xlsx", scenarioWorkbook.FileName);
        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", scenarioWorkbook.ContentType);
        Assert.Equal("PK", System.Text.Encoding.ASCII.GetString(scenarioWorkbook.Content, 0, 2));

        WorkspaceExportTestHelpers.AssertWorkbook(scenarioWorkbook.Content, workbook =>
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
        var state = WorkspaceExportTestHelpers.BuildWorkspaceState();
        var service = new WorkspaceDocumentExportService();
        const string councilNarrative = "Council staffing narrative: plan funding for a part-time city clerk and a full-time utility employee.";

        var pdfReport = service.CreateWorkspacePdfReport(state, councilNarrative);
        var pdfText = WorkspaceExportTestHelpers.ExtractPdfText(pdfReport.Content);

        Assert.EndsWith(".pdf", pdfReport.FileName);
        Assert.Equal("water-utility-fy2026-rate-packet.pdf", pdfReport.FileName);
        Assert.Equal("application/pdf", pdfReport.ContentType);
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(pdfReport.Content, 0, 4));
        Assert.True(pdfReport.Content.Length > 1000);
        Assert.Contains("TOWN OF WILEY, COLORADO | UTILITY RATE STUDY", pdfText);
        Assert.Contains("Powered by Wiley Widget + Semantic Kernel", pdfText);
        Assert.Contains(WorkspaceTestData.CouncilReviewScenario, pdfText);
        Assert.Contains("Council planning narrative", pdfText);
        Assert.Contains("part-time city clerk", pdfText);
        Assert.Contains("Financial summary", pdfText);
        Assert.Contains("Total costs", pdfText);
        Assert.Contains("Adjusted total costs", pdfText);
        Assert.Contains("Rate comparison visualization", pdfText);
        Assert.Contains("Scenario items", pdfText);
        Assert.Contains("Vehicle replacement", pdfText);
        Assert.Contains("New Personnel Cost Allocation", pdfText);
        Assert.Contains("PT City Clerk", pdfText);
        Assert.Contains("FT Field Employee", pdfText);
        Assert.Contains("Prorated annual cost per enterprise", pdfText);
        Assert.Contains("Rate Impact Summary", pdfText);
        Assert.Contains("Wiley Widget/Semantic Kernel planning insight", pdfText);
        Assert.Contains("Projection series", pdfText);
        Assert.Contains("Assumptions & Data Sources", pdfText);
        Assert.Contains("Data source: Live Aurora ledger_entries after QuickBooks import", pdfText);
        Assert.Contains("AI grounding: Semantic Kernel + WorkspaceKnowledgeService (as of 2026-05-11)", pdfText);
        Assert.Contains("Allocation model: Pro-rata by direct benefit (Field) + equal split (Clerk)", pdfText);
    }

    [Fact]
    public void CreateWorkspacePdfReport_DetectsPersonnelScenarioItems_AndWritesCouncilAllocationSections()
    {
        var state = new WorkspaceState();
        state.ApplyBootstrap(WorkspaceTestData.CreateWaterUtilityBootstrap(
            "Council personnel hire scenario",
            WorkspaceTestData.WaterCurrentRate,
            WorkspaceTestData.WaterTotalCosts,
            WorkspaceTestData.WaterProjectedVolume,
            scenarioItems:
            [
                new WorkspaceScenarioItemData(Guid.NewGuid(), "PT City Clerk", 6250m),
                new WorkspaceScenarioItemData(Guid.NewGuid(), "FT Field Employee", 18333.33m)
            ]));
        var service = new WorkspaceDocumentExportService();

        var pdfReport = service.CreateWorkspacePdfReport(state);
        var pdfText = WorkspaceExportTestHelpers.ExtractPdfText(pdfReport.Content);

        Assert.Contains("New Personnel Cost Allocation", pdfText);
        Assert.Contains("$25,000", pdfText);
        Assert.Contains("25.0% split across Water, Sewer, Trash, and Apartments", pdfText);
        Assert.Contains("$55,000", pdfText);
        Assert.Contains("33.3% split across Water, Sewer, and Apartments", pdfText);
        Assert.Contains("Prorated annual cost per enterprise", pdfText);
        Assert.Contains("Rate Impact Summary", pdfText);
        Assert.Contains("New annual revenue", pdfText);
        Assert.Contains("reserve capacity", pdfText);
    }
}

internal static class WorkspaceExportTestHelpers
{
    public static void AssertWorkbook(byte[] workbookBytes, Action<IWorkbook> assertion)
    {
        using var excelEngine = new ExcelEngine();
        var application = excelEngine.Excel;
        application.DefaultVersion = ExcelVersion.Xlsx;

        using var stream = new MemoryStream(workbookBytes, writable: false);
        var workbook = application.Workbooks.Open(stream);
        assertion(workbook);
        workbook.Close();
    }

    public static string ExtractPdfText(byte[] pdfBytes)
    {
        using var stream = new MemoryStream(pdfBytes, writable: false);
        using var loadedDocument = new PdfLoadedDocument(stream);
        var builder = new System.Text.StringBuilder();

        foreach (PdfPageBase page in loadedDocument.Pages)
        {
            builder.AppendLine(page.ExtractText());
        }

        return builder.ToString();
    }

    public static WorkspaceState BuildWorkspaceState()
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
