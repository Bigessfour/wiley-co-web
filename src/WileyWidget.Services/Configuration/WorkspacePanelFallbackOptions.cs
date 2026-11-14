namespace WileyWidget.Services.Configuration;

/// <summary>
/// Optional sample math for council panels. Defaults off so a missing budget stays empty
/// and the workspace can ask for a QuickBooks import.
/// </summary>
public sealed class WorkspacePanelFallbackOptions
{
    public const string SectionName = "WorkspacePanels:Fallback";

    /// <summary>When true, capital gap requests return synthetic results if live budget data would yield NotFound.</summary>
    public bool UseSyntheticCapitalGapWhenNoBudgetData { get; set; }

    /// <summary>When true, debt coverage requests return synthetic results if the enterprise is missing from the data store.</summary>
    public bool UseSyntheticDebtCoverageWhenEnterpriseMissing { get; set; }
}
