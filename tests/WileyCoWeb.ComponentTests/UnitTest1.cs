using WileyCoWeb.Contracts;
using WileyCoWeb.Services;
using WileyCoWeb.State;

namespace WileyCoWeb.ComponentTests;

public sealed class UnitTest1
{
    [Fact]
    public void Syncfusion_Exports_ReturnExpectedExcelAndPdfPayloads()
    {
        var state = BuildWorkspaceState();
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
