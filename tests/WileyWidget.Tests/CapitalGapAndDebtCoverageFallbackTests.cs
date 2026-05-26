using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using WileyWidget.Business.Interfaces;
using WileyWidget.Models;
using WileyWidget.Services;
using WileyWidget.Services.Abstractions;
using WileyWidget.Services.Configuration;

namespace WileyWidget.Tests;

public sealed class CapitalGapAndDebtCoverageFallbackTests
{
    [Fact]
    public async Task CapitalGap_BuildAsync_ReturnsSynthetic_WhenNoBudgetAndFallbackEnabled()
    {
        var budget = new Mock<IBudgetRepository>();
        budget.Setup(b => b.GetByFiscalYearAsync(2026, default)).ReturnsAsync(Array.Empty<BudgetEntry>());

        var options = Options.Create(new WorkspacePanelFallbackOptions { UseSyntheticCapitalGapWhenNoBudgetData = true });
        var service = new CapitalGapService(budget.Object, NullLogger<CapitalGapService>.Instance, options);

        var result = await service.BuildAsync("Water Utility", 2026);

        Assert.Equal("Water Utility", result.SelectedEnterprise);
        Assert.Equal(2026, result.SelectedFiscalYear);
        Assert.True(result.CapitalItems.Count > 0);
        Assert.Contains("sample data", result.ExecutiveSummary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CapitalGap_BuildAsync_ThrowsNotFound_WhenNoBudgetAndFallbackDisabled()
    {
        var budget = new Mock<IBudgetRepository>();
        budget.Setup(b => b.GetByFiscalYearAsync(2026, default)).ReturnsAsync(Array.Empty<BudgetEntry>());

        var options = Options.Create(new WorkspacePanelFallbackOptions { UseSyntheticCapitalGapWhenNoBudgetData = false });
        var service = new CapitalGapService(budget.Object, NullLogger<CapitalGapService>.Instance, options);

        await Assert.ThrowsAsync<CapitalGapNotFoundException>(() => service.BuildAsync("Water Utility", 2026));
    }

    [Fact]
    public async Task DebtCoverage_BuildAsync_ReturnsSynthetic_WhenEnterpriseMissingAndFallbackEnabled()
    {
        var enterprises = new Mock<IEnterpriseRepository>();
        enterprises.Setup(e => e.GetAllAsync(default)).ReturnsAsync(Array.Empty<Enterprise>());

        var accounts = new Mock<IAccountsRepository>();
        var budget = new Mock<IBudgetRepository>();

        var options = Options.Create(new WorkspacePanelFallbackOptions { UseSyntheticDebtCoverageWhenEnterpriseMissing = true });
        var service = new DebtCoverageService(
            enterprises.Object,
            accounts.Object,
            budget.Object,
            NullLogger<DebtCoverageService>.Instance,
            options);

        var result = await service.BuildAsync("Water Utility", 2026);

        Assert.Equal("Water Utility", result.SelectedEnterprise);
        Assert.Contains("sample data", result.ExecutiveSummary, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(3, result.WaterfallPoints.Count);
    }

    [Fact]
    public async Task DebtCoverage_BuildAsync_ThrowsNotFound_WhenEnterpriseMissingAndFallbackDisabled()
    {
        var enterprises = new Mock<IEnterpriseRepository>();
        enterprises.Setup(e => e.GetAllAsync(default)).ReturnsAsync(Array.Empty<Enterprise>());

        var accounts = new Mock<IAccountsRepository>();
        var budget = new Mock<IBudgetRepository>();

        var options = Options.Create(new WorkspacePanelFallbackOptions { UseSyntheticDebtCoverageWhenEnterpriseMissing = false });
        var service = new DebtCoverageService(
            enterprises.Object,
            accounts.Object,
            budget.Object,
            NullLogger<DebtCoverageService>.Instance,
            options);

        await Assert.ThrowsAsync<DebtCoverageNotFoundException>(() => service.BuildAsync("Water Utility", 2026));
    }
}
