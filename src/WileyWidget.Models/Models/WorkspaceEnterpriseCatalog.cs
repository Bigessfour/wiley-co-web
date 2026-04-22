using System;
using System.Collections.Generic;
using System.Linq;

namespace WileyWidget.Models;

public static class WorkspaceEnterpriseCatalog
{
    public const string WaterUtility = "Water Utility";
    public const string WileySanitationDistrict = "Wiley Sanitation District";
    public const string Trash = "Trash";
    public const string Apartments = "Apartments";

    public static readonly IReadOnlyList<string> CanonicalEnterpriseOrder =
    [
        WaterUtility,
        WileySanitationDistrict,
        Trash,
        Apartments
    ];

    private static readonly IReadOnlyDictionary<string, string[]> EnterpriseAliases =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            [WaterUtility] = [WaterUtility, "Water", "WTR"],
            [WileySanitationDistrict] = [WileySanitationDistrict, "Sanitation Utility", "Sanitation", "Sewer", "WSD"],
            [Trash] = [Trash, "Trash Utility", "Refuse", "Garbage"],
            [Apartments] = [Apartments, "Apartment", "Apts"]
        };

    public static IEnumerable<string> GetAliases(string enterpriseName)
    {
        if (string.IsNullOrWhiteSpace(enterpriseName))
        {
            return [];
        }

        return EnterpriseAliases.TryGetValue(enterpriseName.Trim(), out var aliases)
            ? aliases
            : [enterpriseName.Trim()];
    }

    public static bool TryNormalizeEnterpriseName(string? value, out string normalizedName)
    {
        normalizedName = string.Empty;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var trimmed = value.Trim();
        foreach (var enterpriseName in CanonicalEnterpriseOrder)
        {
            if (GetAliases(enterpriseName).Any(alias => string.Equals(alias, trimmed, StringComparison.OrdinalIgnoreCase)))
            {
                normalizedName = enterpriseName;
                return true;
            }
        }

        return false;
    }

    public static int GetSortOrder(string? enterpriseName)
    {
        if (!TryNormalizeEnterpriseName(enterpriseName, out var normalizedName))
        {
            return int.MaxValue;
        }

        for (var index = 0; index < CanonicalEnterpriseOrder.Count; index++)
        {
            if (string.Equals(CanonicalEnterpriseOrder[index], normalizedName, StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }
        }

        return int.MaxValue;
    }
}