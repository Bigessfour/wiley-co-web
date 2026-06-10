using System;
using System.Collections.Generic;

namespace WileyWidget.Models;

public static class EnterpriseBudgetScope
{
    private static readonly IReadOnlyDictionary<string, string[]> EnterpriseKeywords =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            [WorkspaceEnterpriseCatalog.WaterUtility] = ["Water", "WTR", "Utility"],
            [WorkspaceEnterpriseCatalog.WileySanitationDistrict] = ["Sewer", "Sanitation", "WSD", "District"],
            [WorkspaceEnterpriseCatalog.Trash] = ["Trash", "Refuse", "Garbage", "Sanitation"],
            [WorkspaceEnterpriseCatalog.Apartments] = ["Apartment", "Apts", "Housing"]
        };

    public static IReadOnlyList<string> GetKeywords(string? enterpriseName)
    {
        if (string.IsNullOrWhiteSpace(enterpriseName))
        {
            return [];
        }

        var trimmed = enterpriseName.Trim();
        if (EnterpriseKeywords.TryGetValue(trimmed, out var keywords))
        {
            return keywords;
        }

        return WorkspaceEnterpriseCatalog.TryNormalizeEnterpriseName(trimmed, out var normalized)
               && EnterpriseKeywords.TryGetValue(normalized, out keywords)
            ? keywords
            : [trimmed];
    }

    public static bool MatchesEnterpriseScope(
        string? enterpriseName,
        string? description,
        string? departmentName,
        string? fundName,
        string? municipalAccountName)
    {
        foreach (var keyword in GetKeywords(enterpriseName))
        {
            if (ContainsKeyword(description, keyword)
                || ContainsKeyword(departmentName, keyword)
                || ContainsKeyword(fundName, keyword)
                || ContainsKeyword(municipalAccountName, keyword))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsKeyword(string? value, string keyword)
        => !string.IsNullOrWhiteSpace(value)
            && value.Contains(keyword, StringComparison.OrdinalIgnoreCase);
}
