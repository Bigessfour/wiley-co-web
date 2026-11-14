namespace WileyWidget.Services.Configuration;

/// <summary>
/// Runs the existing QuickBooks ledger import when a new copy of Deb's PC backup appears.
/// The company file itself is not read. Only ledger spreadsheets inside that backup are.
/// </summary>
public sealed class QuickBooksBackupImportOptions
{
    public const string SectionName = "QuickBooksBackupImport";

    public bool Enabled { get; set; }

    /// <summary>Local folder the backup watcher fills. Empty disables the scan.</summary>
    public string BackupRoot { get; set; } = "";

    public int FiscalYear { get; set; } = 2026;

    /// <summary>Remembers files already sent through the import. Empty uses a file next to the backup root.</summary>
    public string StateFilePath { get; set; } = "";
}
