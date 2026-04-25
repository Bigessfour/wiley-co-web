using Microsoft.AspNetCore.Components;
using WileyCoWeb.Contracts;
using WileyWidget.Models;

namespace WileyCoWeb.Components.Panels;

public partial class BreakEvenPanel : ComponentBase
{
    [Parameter] public decimal TotalCosts { get; set; }
    [Parameter] public decimal ProjectedVolume { get; set; }
    [Parameter] public string TotalCostsDisplay { get; set; } = string.Empty;
    [Parameter] public string ProjectedVolumeDisplay { get; set; } = string.Empty;
    [Parameter] public string RateDeltaDisplay { get; set; } = string.Empty;
    [Parameter] public string BreakEvenRateDisplay { get; set; } = string.Empty;
    [Parameter] public bool IsSavingBaseline { get; set; }
    [Parameter] public string BaselineSaveStatus { get; set; } = string.Empty;
    [Parameter] public IReadOnlyList<BreakEvenQuadrantData> Quadrants { get; set; } = [];
    [Parameter] public IReadOnlyList<ApartmentUnitTypeData> ApartmentUnitTypes { get; set; } = [];
    [Parameter] public EventCallback<Syncfusion.Blazor.Inputs.ChangeEventArgs<decimal>> OnTotalCostsChanged { get; set; }
    [Parameter] public EventCallback<Syncfusion.Blazor.Inputs.ChangeEventArgs<decimal>> OnProjectedVolumeChanged { get; set; }
    [Parameter] public EventCallback OnSaveWorkspaceBaseline { get; set; }
    [Parameter] public EventCallback<IReadOnlyList<ApartmentUnitTypeData>> OnApartmentUnitTypesChanged { get; set; }

    public IReadOnlyList<BreakEvenQuadrantData> VisibleQuadrants => Quadrants.Count > 0 ? Quadrants : BuildFallbackQuadrants();

    protected string GetQuadrantSummary(BreakEvenQuadrantData quadrant)
        => $"Current {quadrant.CurrentRate:C2} · Balance {quadrant.MonthlyBalance:C0} · Break-even {quadrant.BreakEvenRate:C2}";

    protected static string GetQuadrantChartId(BreakEvenQuadrantData quadrant)
        => $"break-even-chart-{quadrant.EnterpriseName.ToLowerInvariant().Replace(' ', '-') }";

    private IReadOnlyList<BreakEvenQuadrantData> BuildFallbackQuadrants()
    {
        var labels = new[] { "Current", "Projected", "Forward" };
        var breakEvenRate = ProjectedVolume > 0 ? TotalCosts / ProjectedVolume : 0m;
        var expensesPerCustomer = breakEvenRate;

        return new[]
        {
            BuildFallbackQuadrant(WorkspaceEnterpriseCatalog.WaterUtility, "Water", labels, breakEvenRate, expensesPerCustomer),
            BuildFallbackQuadrant(WorkspaceEnterpriseCatalog.WileySanitationDistrict, "Sewer", labels, breakEvenRate, expensesPerCustomer),
            BuildFallbackQuadrant(WorkspaceEnterpriseCatalog.Trash, "Trash", labels, breakEvenRate, expensesPerCustomer),
            BuildFallbackQuadrant(WorkspaceEnterpriseCatalog.Apartments, "Apartments", labels, breakEvenRate, expensesPerCustomer)
        };
    }

    private BreakEvenQuadrantData BuildFallbackQuadrant(string enterpriseName, string enterpriseType, IReadOnlyList<string> labels, decimal breakEvenRate, decimal expensesPerCustomer)
    {
        var revenue = ProjectedVolume > 0 ? TotalCosts / ProjectedVolume : 0m;
        var seriesPoints = labels
            .Select(label => new BreakEvenSeriesPoint(label, revenue, expensesPerCustomer, breakEvenRate))
            .ToList();

        return new BreakEvenQuadrantData(
            enterpriseName,
            enterpriseType,
            revenue,
            TotalCosts,
            TotalCosts,
            0m,
            breakEvenRate,
            Math.Max(1m, ProjectedVolume),
            seriesPoints);
    }

    protected double GetRateAdequacy(BreakEvenQuadrantData quadrant)
        => quadrant.BreakEvenRate > 0 && quadrant.CurrentRate > 0
            ? Math.Min((double)(quadrant.CurrentRate / quadrant.BreakEvenRate * 100m), 150.0)
            : 0.0;

    protected string GetRateAdequacyCssClass(BreakEvenQuadrantData quadrant)
        => GetRateAdequacy(quadrant) >= 100 ? "text-emerald-600"
         : GetRateAdequacy(quadrant) >= 85 ? "text-amber-600"
         : "text-rose-600";

    protected string GetRateAdequacyLabel(BreakEvenQuadrantData quadrant)
        => GetRateAdequacy(quadrant) >= 100 ? "Fully self-supporting"
         : GetRateAdequacy(quadrant) >= 85 ? "Near break-even"
         : "Below break-even";

    protected static string GetQuadrantGaugeId(BreakEvenQuadrantData quadrant)
        => $"break-even-gauge-{SanitizeId(quadrant.EnterpriseName)}";

    protected static string GetQuadrantSectionId(BreakEvenQuadrantData quadrant)
        => $"break-even-quadrant-{SanitizeId(quadrant.EnterpriseName)}";

    private static string SanitizeId(string value)
        => new string(value.Trim().Select(c => char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : '-').ToArray());
}