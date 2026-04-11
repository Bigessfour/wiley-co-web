namespace WileyCoWeb.Contracts;

public static class WorkspaceDefaults
{
    // Empty bootstrap placeholders only. Production and tests must hydrate real values from API or explicit fixture data.
    public const string SelectedEnterprise = "";

    public const int SelectedFiscalYear = 0;

    public const string ActiveScenarioName = "";

    public const decimal CurrentRate = 0m;

    public const decimal TotalCosts = 0m;

    public const decimal ProjectedVolume = 0m;

    public static readonly string[] EnterpriseOptions = [];

    public static readonly int[] FiscalYearOptions = [];

    public static readonly string[] CustomerServiceOptions = [];

    public static readonly string[] CustomerCityLimitOptions = [];
}