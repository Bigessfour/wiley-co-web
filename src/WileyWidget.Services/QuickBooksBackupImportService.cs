using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WileyCoWeb.Contracts;
using WileyWidget.Services.Configuration;

namespace WileyWidget.Services;

public sealed class QuickBooksBackupImportService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    private readonly IQuickBooksLedgerImporter importer;
    private readonly IOptions<QuickBooksBackupImportOptions> options;
    private readonly ILogger<QuickBooksBackupImportService> logger;

    public QuickBooksBackupImportService(
        IQuickBooksLedgerImporter importer,
        IOptions<QuickBooksBackupImportOptions> options,
        ILogger<QuickBooksBackupImportService> logger)
    {
        this.importer = importer ?? throw new ArgumentNullException(nameof(importer));
        this.options = options ?? throw new ArgumentNullException(nameof(options));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task<QuickBooksBackupImportResponse> ImportAsync(CancellationToken cancellationToken = default)
        => ImportAsync(commit: true, cancellationToken);

    public async Task<QuickBooksBackupImportResponse> ImportAsync(bool commit, CancellationToken cancellationToken = default)
    {
        var settings = options.Value;
        if (!settings.Enabled)
        {
            throw new InvalidOperationException("QuickBooks backup import is turned off.");
        }

        var backupRoot = settings.BackupRoot?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(backupRoot))
        {
            throw new InvalidOperationException("QuickBooksBackupImport:BackupRoot is not set.");
        }

        var fullRoot = Path.GetFullPath(backupRoot);
        if (!Directory.Exists(fullRoot))
        {
            throw new DirectoryNotFoundException($"Backup import folder was not found: {fullRoot}");
        }

        if (settings.FiscalYear <= 0)
        {
            throw new InvalidOperationException("QuickBooksBackupImport:FiscalYear must be a positive year.");
        }

        var files = Directory.EnumerateFiles(fullRoot, "*", SearchOption.AllDirectories)
            .Select(path => new QuickBooksBackupFile(
                Path.GetRelativePath(fullRoot, path),
                new FileInfo(path).Length))
            .ToList();
        var plan = QuickBooksBackupImportPlanner.Plan(files);
        var statePath = ResolveStatePath(settings, fullRoot);
        var state = await ReadStateAsync(statePath, cancellationToken).ConfigureAwait(false);
        var results = new List<QuickBooksBackupImportFileResult>();
        var imported = 0;
        var duplicates = 0;
        var skipped = 0;
        var ready = 0;

        foreach (var item in plan)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (item.SkipReason is not null || string.IsNullOrWhiteSpace(item.Enterprise))
            {
                skipped++;
                results.Add(new QuickBooksBackupImportFileResult(
                    item.RelativePath,
                    item.Enterprise,
                    item.SkipReason ?? "skipped",
                    0,
                    false,
                    item.SkipReason ?? "Skipped."));
                continue;
            }

            var absolute = Path.GetFullPath(Path.Combine(fullRoot, item.RelativePath));
            var rootPrefix = fullRoot.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!absolute.StartsWith(rootPrefix, StringComparison.Ordinal))
            {
                skipped++;
                results.Add(new QuickBooksBackupImportFileResult(item.RelativePath, item.Enterprise, "outside-root", 0, false, "The file path left the backup folder."));
                continue;
            }

            var info = new FileInfo(absolute);
            var stamp = info.LastWriteTimeUtc.Ticks + ":" + info.Length;
            if (state.Files.TryGetValue(item.RelativePath, out var seen) && seen == stamp)
            {
                skipped++;
                results.Add(new QuickBooksBackupImportFileResult(item.RelativePath, item.Enterprise, "already-seen", 0, false, "This backup copy was already sent to the import."));
                continue;
            }

            try
            {
                var bytes = await File.ReadAllBytesAsync(absolute, cancellationToken).ConfigureAwait(false);
                var fileName = Path.GetFileName(absolute);
                var accountPrefix = CheckListingLedgerConverter.AccountPrefixForEnterprise(item.Enterprise);
                var extension = Path.GetExtension(fileName);
                IReadOnlyList<CheckListingRow> checks = [];
                if ((extension.Equals(".xlsx", StringComparison.OrdinalIgnoreCase) || extension.Equals(".xls", StringComparison.OrdinalIgnoreCase))
                    && CheckListingLedgerConverter.TryConvert(bytes, accountPrefix, out var csvBytes, out checks))
                {
                    bytes = csvBytes;
                    fileName = Path.ChangeExtension(fileName, ".csv");
                }

                if (!commit)
                {
                    ready++;
                    results.Add(new QuickBooksBackupImportFileResult(
                        item.RelativePath,
                        item.Enterprise,
                        "ready",
                        checks.Count,
                        false,
                        checks.Count == 0
                            ? "Ready for the QuickBooks import. This file is not a check listing, so the row count is known at commit."
                            : $"Ready. {checks.Count} check(s) would go through the QuickBooks import. Nothing was written."));
                    continue;
                }

                var preview = await importer.PreviewAsync(bytes, fileName, item.Enterprise, settings.FiscalYear, cancellationToken).ConfigureAwait(false);
                if (preview.Rows.Count == 0)
                {
                    skipped++;
                    results.Add(new QuickBooksBackupImportFileResult(item.RelativePath, item.Enterprise, "no-rows", 0, false, "The file has no ledger rows to import."));
                    continue;
                }

                var freshRows = preview.Rows.Where(row => !row.IsDuplicate).ToList();
                if (freshRows.Count == 0)
                {
                    duplicates++;
                    state.Files[item.RelativePath] = stamp;
                    results.Add(new QuickBooksBackupImportFileResult(item.RelativePath, item.Enterprise, "duplicate", 0, true, preview.StatusMessage));
                    continue;
                }

                if (freshRows.Count < preview.Rows.Count)
                {
                    bytes = CheckListingLedgerConverter.WriteLedgerCsv(freshRows);
                    fileName = Path.ChangeExtension(fileName, ".csv");
                }

                var commitResult = await importer.CommitAsync(bytes, fileName, item.Enterprise, settings.FiscalYear, cancellationToken).ConfigureAwait(false);
                if (commitResult.IsDuplicate)
                {
                    duplicates++;
                }
                else
                {
                    imported++;
                    state.Files[item.RelativePath] = stamp;
                }

                results.Add(new QuickBooksBackupImportFileResult(
                    item.RelativePath,
                    item.Enterprise,
                    commitResult.IsDuplicate ? "duplicate" : "imported",
                    commitResult.ImportedRows,
                    commitResult.IsDuplicate,
                    commitResult.StatusMessage));
                logger.LogInformation(
                    "Backup import {Status} for {File} as {Enterprise}: {Message}",
                    commitResult.IsDuplicate ? "duplicate" : "imported",
                    item.RelativePath,
                    item.Enterprise,
                    commitResult.StatusMessage);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                skipped++;
                results.Add(new QuickBooksBackupImportFileResult(item.RelativePath, item.Enterprise, "failed", 0, false, ex.Message));
                logger.LogWarning(ex, "Backup import failed for {File}", item.RelativePath);
            }
        }

        if (commit)
        {
            await WriteStateAsync(state, statePath, cancellationToken).ConfigureAwait(false);
        }

        var message = !commit
            ? $"Dry run. {ready} file(s) are ready. Nothing was written."
            : imported == 0 && duplicates == 0
                ? "No new QuickBooks ledger files were imported from this backup."
                : $"Imported {imported} file(s). {duplicates} file(s) were already in the ledger.";
        return new QuickBooksBackupImportResponse(
            fullRoot,
            settings.FiscalYear,
            plan.Count,
            imported,
            duplicates,
            skipped,
            message,
            results);
    }

    private static string ResolveStatePath(QuickBooksBackupImportOptions settings, string fullRoot)
    {
        if (!string.IsNullOrWhiteSpace(settings.StateFilePath))
        {
            return Path.GetFullPath(settings.StateFilePath);
        }

        // Beside the backup folder, not inside it. A stamp inside the folder
        // would sit on a NAS share the next scan walks.
        var trimmed = fullRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var parent = Path.GetDirectoryName(trimmed);
        if (string.IsNullOrEmpty(parent))
        {
            parent = trimmed;
        }

        return Path.Combine(parent, ".wiley-qb-backup-import-state.json");
    }

    private static async Task<BackupImportState> ReadStateAsync(string statePath, CancellationToken cancellationToken)
    {
        if (!File.Exists(statePath))
        {
            return new BackupImportState();
        }

        await using var stream = File.OpenRead(statePath);
        return await JsonSerializer.DeserializeAsync<BackupImportState>(stream, JsonOptions, cancellationToken).ConfigureAwait(false)
            ?? new BackupImportState();
    }

    private static async Task WriteStateAsync(BackupImportState state, string statePath, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(statePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var temp = statePath + ".tmp";
        await using (var stream = File.Create(temp))
        {
            await JsonSerializer.SerializeAsync(stream, state, JsonOptions, cancellationToken).ConfigureAwait(false);
        }

        File.Move(temp, statePath, overwrite: true);
    }

    private sealed class BackupImportState
    {
        public Dictionary<string, string> Files { get; set; } = new(StringComparer.Ordinal);
    }
}
