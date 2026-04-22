using System;
using System.Collections.Generic;

namespace WileyCoWeb.Api;

internal static class WorkspaceEnterpriseSeedCatalog
{
    public static readonly IReadOnlyList<WorkspaceEnterpriseSeed> All =
    [
        new(
            "Water Utility",
            "Water",
            "Water",
            31.25m,
            98000m,
            4500,
            1.02m,
            2.00m),
        new(
            "Wiley Sanitation District",
            "Sewer",
            "Sewer",
            21.50m,
            72000m,
            3200,
            1.01m,
            1.00m),
        new(
            "Trash",
            "Trash",
            "Trash",
            18.75m,
            54000m,
            3000,
            1.03m,
            3.00m),
        new(
            "Apartments",
            "Apartments",
            "Apartments",
            725.00m,
            14500m,
            24,
            1.04m,
            4.00m)
    ];

    public static bool TryGet(string? enterpriseName, out WorkspaceEnterpriseSeed seed)
    {
        foreach (var candidate in All)
        {
            if (string.Equals(candidate.Name, enterpriseName, StringComparison.OrdinalIgnoreCase))
            {
                seed = candidate;
                return true;
            }
        }

        seed = default!;
        return false;
    }
}

internal sealed record WorkspaceEnterpriseSeed(
    string Name,
    string Type,
    string DepartmentName,
    decimal CurrentRate,
    decimal MonthlyExpenses,
    int CustomerCount,
    decimal GoalAdjustmentFactor,
    decimal TargetProfitMarginPercent);