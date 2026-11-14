namespace WileyWidget.Services;

public sealed record QuickBooksBackupFile(string RelativePath, long Length);

public sealed record QuickBooksBackupPlanItem(string RelativePath, string? Enterprise, string? SkipReason);

/// <summary>
/// Chooses which files from a PC backup are ledger exports the import already understands.
/// </summary>
public static class QuickBooksBackupImportPlanner
{
    public const long MaxFileBytes = 50L * 1024 * 1024;

    private static readonly string[] SkippedPathParts =
    [
        "/appdata/",
        "/windows/",
        "/program files/",
        "/program files (x86)/",
        "/#recycle/",
        "/@eadir/",
        "/node_modules/",
        "/downloads/",
        "/intuit/"
    ];

    public static IReadOnlyList<QuickBooksBackupPlanItem> Plan(IEnumerable<QuickBooksBackupFile> files)
    {
        var planned = new List<QuickBooksBackupPlanItem>();
        foreach (var file in files)
        {
            planned.Add(PlanOne(file));
        }

        return planned;
    }

    private static QuickBooksBackupPlanItem PlanOne(QuickBooksBackupFile file)
    {
        var relative = file.RelativePath.Replace('\\', '/').Trim();
        if (string.IsNullOrWhiteSpace(relative))
        {
            return new QuickBooksBackupPlanItem(file.RelativePath, null, "empty-path");
        }

        var lowered = "/" + relative.ToLowerInvariant();
        if (!IsLedgerExtension(lowered))
        {
            return new QuickBooksBackupPlanItem(relative, null, "unsupported-extension");
        }

        if (SkippedPathParts.Any(part => lowered.Contains(part, StringComparison.Ordinal)))
        {
            return new QuickBooksBackupPlanItem(relative, null, "skipped-folder");
        }

        if (file.Length > MaxFileBytes)
        {
            return new QuickBooksBackupPlanItem(relative, null, "too-large");
        }

        if (!IsLedgerFolder(lowered))
        {
            return new QuickBooksBackupPlanItem(relative, null, "not-a-ledger-folder");
        }

        var enterprise = ResolveEnterprise(lowered);
        if (enterprise is null)
        {
            return new QuickBooksBackupPlanItem(relative, null, "no-enterprise");
        }

        return new QuickBooksBackupPlanItem(relative, enterprise, null);
    }

    private static bool IsLedgerExtension(string loweredPath)
    {
        return loweredPath.EndsWith(".csv", StringComparison.Ordinal)
            || loweredPath.EndsWith(".xlsx", StringComparison.Ordinal)
            || loweredPath.EndsWith(".xls", StringComparison.Ordinal);
    }

    private static bool IsLedgerFolder(string loweredPath)
    {
        return ContainsAny(loweredPath, "check listing", "check-listing", "quickbooks", "quick books", "qb export");
    }

    private static string? ResolveEnterprise(string loweredPath)
    {
        if (ContainsAny(loweredPath, "trash", "refuse", "garbage"))
        {
            return "Trash";
        }

        if (ContainsAny(loweredPath, "brookside", "apartment"))
        {
            return "Apartments";
        }

        if (ContainsAny(loweredPath, "wsd", "sanitation", "sewer"))
        {
            return "Wiley Sanitation District";
        }

        if (ContainsAny(loweredPath, "utility", "water"))
        {
            return "Water Utility";
        }

        return null;
    }

    private static bool ContainsAny(string loweredPath, params string[] tokens)
    {
        foreach (var token in tokens)
        {
            if (loweredPath.Contains(token, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
