using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using WileyCoWeb.Contracts;
using WileyWidget.Data;
using WileyWidget.Services;
using WileyWidget.Services.Configuration;

namespace WileyWidget.Tests;

public sealed class QuickBooksBackupImportServiceTests
{
    [Fact]
    public async Task ImportAsync_CommitsANewUtilityFile_AndSkipsItTheNextTime()
    {
        var root = Path.Combine(Path.GetTempPath(), "wiley-qb-backup-" + Guid.NewGuid().ToString("N"));
        var state = Path.Combine(Path.GetTempPath(), "wiley-qb-backup-state-" + Guid.NewGuid().ToString("N") + ".json");
        Directory.CreateDirectory(Path.Combine(root, "CHECK LISTING- UTILITY"));
        await File.WriteAllTextAsync(Path.Combine(root, "CHECK LISTING- UTILITY", "october.csv"), "Date,Type\n");

        var importer = new RecordingImporter();
        var service = CreateService(importer, root, state);

        var first = await service.ImportAsync();
        var second = await service.ImportAsync();

        Assert.Equal(1, first.ImportedFiles);
        Assert.Equal("Water Utility", importer.Enterprises.Single());
        Assert.Equal(0, second.ImportedFiles);
        Assert.Contains(second.Files, file => file.Status == "already-seen");

        Directory.Delete(root, recursive: true);
        File.Delete(state);
    }

    [Fact]
    public async Task ImportAsync_DryRun_DoesNotCallTheImport()
    {
        var root = Path.Combine(Path.GetTempPath(), "wiley-qb-backup-" + Guid.NewGuid().ToString("N"));
        var state = Path.Combine(Path.GetTempPath(), "wiley-qb-backup-state-" + Guid.NewGuid().ToString("N") + ".json");
        var source = Path.Combine(AppContext.BaseDirectory, "Fixtures", "check-listing-sample.xlsx");
        Directory.CreateDirectory(Path.Combine(root, "CHECK LISTING- UTILITY"));
        File.Copy(source, Path.Combine(root, "CHECK LISTING- UTILITY", "sample.xlsx"));

        var importer = new RecordingImporter();
        var service = CreateService(importer, root, state);

        var report = await service.ImportAsync(commit: false);

        Assert.Equal("ready", report.Files.Single(file => file.RelativePath.EndsWith("sample.xlsx", StringComparison.Ordinal)).Status);
        Assert.Equal(3, report.Files.Single(file => file.RelativePath.EndsWith("sample.xlsx", StringComparison.Ordinal)).ImportedRows);
        Assert.Empty(importer.Enterprises);
        Assert.False(File.Exists(state));

        Directory.Delete(root, recursive: true);
    }

    [Fact]
    public async Task ImportAsync_CommitsOnlyTheChecksThatAreNotAlreadyImported()
    {
        var root = Path.Combine(Path.GetTempPath(), "wiley-qb-backup-" + Guid.NewGuid().ToString("N"));
        var state = Path.Combine(Path.GetTempPath(), "wiley-qb-backup-state-" + Guid.NewGuid().ToString("N") + ".json");
        Directory.CreateDirectory(Path.Combine(root, "CHECK LISTING- UTILITY"));
        await File.WriteAllTextAsync(Path.Combine(root, "CHECK LISTING- UTILITY", "october.csv"), "Date,Type\n");

        var importer = new RecordingImporter
        {
            PreviewRows =
            [
                new QuickBooksImportPreviewRow(1, "2026-11-04", "Check", "12402", "MAY VALLEY", "HOTSPOT AND CHLORINE", "Utility:Water Supplies", null, 1028.34m, null, null, false, "Water Utility"),
                new QuickBooksImportPreviewRow(2, "2026-10-06", "Check", "12378", "MAY VALLEY", "HOTSPOT/CHLORINE", "Utility:Water Supplies", null, 344.84m, null, null, true, "Water Utility")
            ]
        };
        var service = CreateService(importer, root, state);

        var result = await service.ImportAsync();
        var committed = Encoding.UTF8.GetString(importer.CommittedBytes.Single());

        Assert.Equal(1, result.ImportedFiles);
        Assert.Contains("12402", committed, StringComparison.Ordinal);
        Assert.DoesNotContain("12378", committed, StringComparison.Ordinal);
        Assert.EndsWith(".csv", importer.FileNames.Single(), StringComparison.Ordinal);

        Directory.Delete(root, recursive: true);
        File.Delete(state);
    }

    [Fact]
    [Trait("Category", "HighRisk")]
    public async Task ImportAsync_WritesCheckListingRowsThroughTheExistingImport()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"qb-backup-{Guid.NewGuid():N}")
            .Options;
        var contextFactory = new AppDbContextFactory(options);
        var importer = new QuickBooksImportService(
            LoggerFactory.Create(_ => { }).CreateLogger<QuickBooksImportService>(),
            contextFactory,
            new QuickBooksRoutingService(contextFactory),
            new QuickBooksCsvParser(),
            new QuickBooksExcelParser());

        var root = Path.Combine(Path.GetTempPath(), "wiley-qb-backup-" + Guid.NewGuid().ToString("N"));
        var state = Path.Combine(Path.GetTempPath(), "wiley-qb-backup-state-" + Guid.NewGuid().ToString("N") + ".json");
        Directory.CreateDirectory(Path.Combine(root, "CHECK LISTING- UTILITY"));
        File.Copy(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "check-listing-sample.xlsx"),
            Path.Combine(root, "CHECK LISTING- UTILITY", "sample.xlsx"));
        var service = new QuickBooksBackupImportService(
            importer,
            Options.Create(new QuickBooksBackupImportOptions
            {
                Enabled = true,
                BackupRoot = root,
                FiscalYear = 2026,
                StateFilePath = state
            }),
            NullLogger<QuickBooksBackupImportService>.Instance);

        var first = await service.ImportAsync();
        var second = await service.ImportAsync();

        await using var context = await contextFactory.CreateDbContextAsync();
        var entries = await context.LedgerEntries.AsNoTracking().ToListAsync();

        Assert.Equal(1, first.ImportedFiles);
        Assert.Equal(3, first.Files.Single(file => file.Status == "imported").ImportedRows);
        Assert.Equal(0, second.ImportedFiles);
        Assert.Equal(3, entries.Count);
        Assert.Contains(entries, entry => entry.TransactionNumber == "1001" && entry.EntryScope == "Water Utility");
        Assert.Contains(entries, entry => entry.TransactionNumber == "1002" && entry.EntryScope == "Trash");
        Assert.Contains(entries, entry => entry.TransactionNumber == "2001" && entry.AccountName == "Utility:Management Fee");

        Directory.Delete(root, recursive: true);
        File.Delete(state);
    }

    [Fact]
    public async Task ImportAsync_RejectsWhenTurnedOff()
    {
        var service = new QuickBooksBackupImportService(
            new RecordingImporter(),
            Options.Create(new QuickBooksBackupImportOptions { Enabled = false, BackupRoot = "/tmp", FiscalYear = 2026 }),
            NullLogger<QuickBooksBackupImportService>.Instance);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ImportAsync());
    }

    private static QuickBooksBackupImportService CreateService(RecordingImporter importer, string root, string state)
    {
        return new QuickBooksBackupImportService(
            importer,
            Options.Create(new QuickBooksBackupImportOptions
            {
                Enabled = true,
                BackupRoot = root,
                FiscalYear = 2026,
                StateFilePath = state
            }),
            NullLogger<QuickBooksBackupImportService>.Instance);
    }

    private sealed class RecordingImporter : IQuickBooksLedgerImporter
    {
        public List<string> Enterprises { get; } = [];

        public List<string> FileNames { get; } = [];

        public List<byte[]> CommittedBytes { get; } = [];

        public IReadOnlyList<QuickBooksImportPreviewRow>? PreviewRows { get; init; }

        public Task<QuickBooksImportPreviewResponse> PreviewAsync(byte[] fileBytes, string fileName, string selectedEnterprise, int selectedFiscalYear, CancellationToken cancellationToken = default)
        {
            var rows = PreviewRows ?? [new QuickBooksImportPreviewRow(1, "2026-09-08", "Check", "1", "Example", "Fuel", "Utility:Fuel", null, 10m, null, null, false, selectedEnterprise)];
            return Task.FromResult(new QuickBooksImportPreviewResponse(
                fileName,
                "hash",
                selectedEnterprise,
                selectedFiscalYear,
                rows.Count,
                rows.Count(row => row.IsDuplicate),
                rows.All(row => row.IsDuplicate),
                "Preview.",
                rows));
        }

        public Task<QuickBooksImportCommitResponse> CommitAsync(byte[] fileBytes, string fileName, string selectedEnterprise, int selectedFiscalYear, CancellationToken cancellationToken = default)
        {
            Enterprises.Add(selectedEnterprise);
            FileNames.Add(fileName);
            CommittedBytes.Add(fileBytes);
            return Task.FromResult(new QuickBooksImportCommitResponse(
                fileName,
                "hash",
                selectedEnterprise,
                selectedFiscalYear,
                1,
                10,
                false,
                "Imported.",
                []));
        }
    }
}
