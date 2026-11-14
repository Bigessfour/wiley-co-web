using System;
using System.Collections.Generic;

namespace WileyCoWeb.Api;

internal static class WorkspaceEnterpriseSeedCatalog
{
    public static readonly IReadOnlyList<WorkspaceEnterpriseSeed> All =
    [
        // Adopted minimum monthly charges from the July 1, 2026 rate change.
        // October 26, 2026 town bill, account 213, 3,000 gallons, still at the minimum:
        // water $58, sewage $72, garbage $30.
        // Costs, customer counts, and profit targets stay empty until a QuickBooks import.
        new(
            "Water Utility",
            "Water",
            "Water",
            58.00m,
            0m,
            0,
            0m,
            0m),
        new(
            "Wiley Sanitation District",
            "Sewer",
            "Sewer",
            72.00m,
            0m,
            0,
            0m,
            0m),
        new(
            "Trash",
            "Trash",
            "Trash",
            30.00m,
            0m,
            0,
            0m,
            0m),
        new(
            "Apartments",
            "Apartments",
            "Apartments",
            0m,
            0m,
            0,
            0m,
            0m)
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
