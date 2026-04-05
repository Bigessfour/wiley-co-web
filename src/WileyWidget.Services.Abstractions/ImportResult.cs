using System.Collections.Generic;

namespace WileyWidget.Services.Abstractions;

/// <summary>
/// Result returned by import operations.
/// </summary>
public sealed class ImportResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public int AccountsImported { get; set; }
    public int TotalRows { get; set; }
    public int ProcessedRows { get; set; }
    public int SuccessfulRows { get; set; }
    public int FailedRows { get; set; }
    public List<string>? ValidationErrors { get; set; }
    public List<string> Errors { get; set; } = new();
}