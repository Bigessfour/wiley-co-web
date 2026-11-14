using Microsoft.AspNetCore.Components;
using WileyCoWeb.Contracts;
using WileyCoWeb.State;
using WileyWidget.Abstractions;

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
    [Parameter] public double GaugeMaximum { get; set; }
    [Parameter] public double GaugeCurrentRateValue { get; set; }
    [Parameter] public IReadOnlyList<RateComparisonPoint> RateComparison { get; set; } = [];
    [Parameter] public IReadOnlyList<BreakEvenQuadrantData> Quadrants { get; set; } = [];
    [Parameter] public IReadOnlyList<ApartmentUnitTypeData> ApartmentUnitTypes { get; set; } = [];
    [Parameter] public EventCallback<Syncfusion.Blazor.Inputs.ChangeEventArgs<decimal>> OnTotalCostsChanged { get; set; }
    [Parameter] public EventCallback<Syncfusion.Blazor.Inputs.ChangeEventArgs<decimal>> OnProjectedVolumeChanged { get; set; }
    [Parameter] public EventCallback OnSaveWorkspaceBaseline { get; set; }
    [Parameter] public EventCallback<IReadOnlyList<ApartmentUnitTypeData>> OnApartmentUnitTypesChanged { get; set; }

    public IReadOnlyList<BreakEvenQuadrantData> VisibleQuadrants => Quadrants;

    protected static bool QuadrantNeedsQuickBooksImport(BreakEvenQuadrantData quadrant)
        => quadrant.MonthlyExpenses <= 0m || quadrant.EffectiveCustomerCount <= 0m;

    protected string GetQuadrantSummary(BreakEvenQuadrantData quadrant)
        => $"Current {quadrant.CurrentRate:C2} · Balance {quadrant.MonthlyBalance:C0} · Break-even {quadrant.BreakEvenRate:C2}";

    protected static string GetQuadrantChartId(BreakEvenQuadrantData quadrant)
        => $"break-even-chart-{quadrant.EnterpriseName.ToLowerInvariant().Replace(' ', '-') }";

    protected double GetRateAdequacy(BreakEvenQuadrantData quadrant)
        => quadrant.BreakEvenRate > 0 && quadrant.CurrentRate > 0
            ? (double)EnterpriseRateService.CalculateRateAdequacyPercent(quadrant.CurrentRate, quadrant.BreakEvenRate)
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