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
        state.ApplyBootstrap(new WorkspaceBootstrapData(
            "Water Utility",
            2026,
            "Council Review Scenario",
            55.25m,
            13250m,
            240m,
            DateTime.UtcNow.ToString("O"))
        {
            ScenarioItems =
            [
                new WorkspaceScenarioItemData(Guid.NewGuid(), "Vehicle replacement", 18000m),
                new WorkspaceScenarioItemData(Guid.NewGuid(), "Reserve transfer", 6200m)
            ],
            CustomerRows =
            [
                new CustomerRow("North Plant", "Water", "Yes"),
                new CustomerRow("South Lift", "Sewer", "No")
            ],
            ProjectionRows =
            [
                new ProjectionRow("2024", 48.10m),
                new ProjectionRow("2025", 51.40m),
                new ProjectionRow("2026", 55.25m)
            ]
        });

        return state;
    }
}
