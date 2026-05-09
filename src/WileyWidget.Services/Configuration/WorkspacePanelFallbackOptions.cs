namespace WileyWidget.Services.Configuration;

/// <summary>
/// Optional fallbacks when live budget/enterprise data is missing so council-facing panels return 200 with deterministic sample math instead of 404.
/// </summary>
public sealed class WorkspacePanelFallbackOptions
{
    public const string SectionName = "WorkspacePanels:Fallback";

    /// <summary>When true, capital gap requests return synthetic results if live budget data would yield NotFound.</summary>
    public bool UseSyntheticCapitalGapWhenNoBudgetData { get; set; } = true;

    /// <summary>When true, debt coverage requests return synthetic results if the enterprise is missing from the data store.</summary>
    public bool UseSyntheticDebtCoverageWhenEnterpriseMissing { get; set; } = true;
}
