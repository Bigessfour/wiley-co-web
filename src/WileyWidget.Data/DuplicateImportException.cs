using System;

namespace WileyWidget.Data;

/// <summary>
/// Typed exception for duplicate import attempts (QuickBooks or reference data).
/// Preferred over string-matching InvalidOperationException for duplicate detection.
/// </summary>
public sealed class DuplicateImportException : Exception
{
    public string EntityName { get; }

    public DuplicateImportException(string message, string entityName = "quickbooks-ledger")
        : base(message)
    {
        EntityName = entityName;
    }

    public DuplicateImportException(string message, Exception innerException, string entityName = "quickbooks-ledger")
        : base(message, innerException)
    {
        EntityName = entityName;
    }
}
