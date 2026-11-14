namespace WileyCoWeb.Contracts;

/// <summary>
/// Clerk-facing copy when a workspace field has no live QuickBooks data.
/// </summary>
public static class QuickBooksImportNotes
{
    public const string BudgetAndCustomers =
        "Import the QuickBooks budget for costs and the QuickBooks customer list for volume. These fields stay blank until that data is stored.";

    public const string CapitalGap =
        "Import the QuickBooks budget for this enterprise and fiscal year. Capital gap needs capital-tagged budget lines, and none are stored yet.";

    public const string DebtCoverage =
        "Import the QuickBooks budget for this enterprise and fiscal year. Debt coverage needs revenue and debt-service amounts, and none are stored yet.";

    public static bool IsMissingQuickBooksData(Exception exception)
    {
        var message = exception.Message;
        return message.Contains("No budget entries", StringComparison.OrdinalIgnoreCase)
            || message.Contains("No capital-tagged", StringComparison.OrdinalIgnoreCase)
            || message.Contains("was not found in the live data store", StringComparison.OrdinalIgnoreCase)
            || message.Contains("Insufficient budget data", StringComparison.OrdinalIgnoreCase);
    }
}
