using Syncfusion.XlsIO;
using WileyCoWeb.Contracts;
using WileyCoWeb.Services;
using WileyCoWeb.State;

namespace WileyCoWeb.ComponentTests;

public sealed class CustomerDirectoryExportTests
{
    [Fact]
    [Trait("Category", "HighRisk")]
    public void CreateUtilityCustomerDirectoryWorkbook_WritesFilteredGridColumnsAndRows()
    {
        var state = WorkspaceExportTestHelpers.BuildWorkspaceState();
        state.SetCustomerSearchTerm("Alpha");

        var customers = new List<UtilityCustomerRecord>
        {
            CreateUtilityCustomer(
                1,
                "EXP-001",
                "Alpha Customer",
                "Residential",
                "InsideCityLimits",
                "Active",
                42.50m,
                "555-0101"),
            CreateUtilityCustomer(
                2,
                "EXP-002",
                "Beta Commercial",
                "Commercial",
                "OutsideCityLimits",
                "Active",
                120m,
                "555-0102"),
        };

        var filteredCustomers = customers
            .Where(customer =>
                customer.DisplayName.Contains(state.CustomerSearchTerm, StringComparison.OrdinalIgnoreCase)
                || customer.AccountNumber.Contains(state.CustomerSearchTerm, StringComparison.OrdinalIgnoreCase))
            .ToList();

        var service = new WorkspaceDocumentExportService();
        var workbook = service.CreateUtilityCustomerDirectoryWorkbook(filteredCustomers, state);

        Assert.EndsWith(".xlsx", workbook.FileName);
        Assert.Contains("customers", workbook.FileName, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", workbook.ContentType);
        Assert.Equal("PK", System.Text.Encoding.ASCII.GetString(workbook.Content, 0, 2));

        WorkspaceExportTestHelpers.AssertWorkbook(workbook.Content, openedWorkbook =>
        {
            var worksheet = openedWorkbook.Worksheets[0];

            Assert.Equal("Water Utility utility customer directory", worksheet.Range[1, 1].Text);
            Assert.Equal("Active Filters", worksheet.Range[4, 1].Text);
            Assert.Equal("Search: Alpha", worksheet.Range[4, 2].Text);
            Assert.Equal("Account #", worksheet.Range[6, 1].Text);
            Assert.Equal("Customer", worksheet.Range[6, 2].Text);
            Assert.Equal("Customer Type", worksheet.Range[6, 3].Text);
            Assert.Equal("Location", worksheet.Range[6, 4].Text);
            Assert.Equal("Status", worksheet.Range[6, 5].Text);
            Assert.Equal("Balance", worksheet.Range[6, 6].Text);
            Assert.Equal("Phone", worksheet.Range[6, 7].Text);
            Assert.Equal("EXP-001", worksheet.Range[7, 1].Text);
            Assert.Equal("Alpha Customer", worksheet.Range[7, 2].Text);
            Assert.Equal("Residential", worksheet.Range[7, 3].Text);
            Assert.Equal("InsideCityLimits", worksheet.Range[7, 4].Text);
            Assert.Equal("Active", worksheet.Range[7, 5].Text);
            Assert.Equal(42.50, worksheet.Range[7, 6].Number, 2);
            Assert.Equal("555-0101", worksheet.Range[7, 7].Text);

            var exportedAccountNumbers = Enumerable.Range(7, Math.Max(worksheet.UsedRange.LastRow - 6, 1))
                .Select(rowIndex => worksheet.Range[rowIndex, 1].Text)
                .Where(accountNumber => !string.IsNullOrWhiteSpace(accountNumber))
                .Where(accountNumber => !accountNumber.StartsWith("Created with a trial version of Syncfusion", StringComparison.Ordinal))
                .ToArray();

            Assert.Equal(["EXP-001"], exportedAccountNumbers);
        });
    }

    private static UtilityCustomerRecord CreateUtilityCustomer(
        int id,
        string accountNumber,
        string displayName,
        string customerType,
        string serviceLocation,
        string status,
        decimal currentBalance,
        string phoneNumber)
    {
        return new UtilityCustomerRecord(
            id,
            accountNumber,
            "First",
            "Last",
            null,
            displayName,
            customerType,
            "100 Main St",
            "Wiley",
            "CO",
            "81092",
            serviceLocation,
            status,
            currentBalance,
            "2026-01-01T00:00:00.000Z",
            phoneNumber,
            null,
            null,
            null);
    }
}
