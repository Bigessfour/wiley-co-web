using System.Collections.Generic;
using System.Linq;

namespace WileyCoWeb.Contracts;

public static class WorkspaceDefaults
{
    public const string SelectedEnterprise = "Water";

    public const int SelectedFiscalYear = 2026;

    public const string ActiveScenarioName = "Base Planning Scenario";

    public const decimal CurrentRate = 28.50m;

    public const decimal TotalCosts = 412500m;

    public const decimal ProjectedVolume = 14500m;

    public static IReadOnlyList<string> EnterpriseOptions { get; } = [SelectedEnterprise];

    public static IReadOnlyList<int> FiscalYearOptions { get; } = [SelectedFiscalYear];

    public static IReadOnlyList<string> CustomerServiceOptions { get; } = ["All Services"];

    public static IReadOnlyList<string> CustomerCityLimitOptions { get; } = ["All", "Yes", "No"];

    public static WorkspaceBootstrapData CreateBootstrapData(string? lastUpdatedUtc = null)
    {
        return new WorkspaceBootstrapData(
            SelectedEnterprise,
            SelectedFiscalYear,
            ActiveScenarioName,
            CurrentRate,
            TotalCosts,
            ProjectedVolume,
            lastUpdatedUtc)
        {
            EnterpriseOptions = EnterpriseOptions.ToList(),
            FiscalYearOptions = FiscalYearOptions.ToList(),
            CustomerServiceOptions = CustomerServiceOptions.ToList(),
            CustomerCityLimitOptions = CustomerCityLimitOptions.ToList()
        };
    }
}