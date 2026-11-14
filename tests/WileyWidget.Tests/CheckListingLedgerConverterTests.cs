using WileyWidget.Services;

namespace WileyWidget.Tests;

public sealed class CheckListingLedgerConverterTests
{
    [Fact]
    public void TryRead_SampleWorkbook_KeepsBothSections_AndSkipsTheTotal()
    {
        var bytes = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", "check-listing-sample.xlsx"));

        var converted = CheckListingLedgerConverter.TryRead(bytes, "Utility", out var rows);

        Assert.True(converted);
        Assert.Equal(3, rows.Count);
        Assert.Equal("1001", rows[0].CheckNumber);
        Assert.Equal("2026-01-15", rows[0].Date);
        Assert.Equal("Utility:Water Supplies", rows[0].Account);
        Assert.Equal(10.50m, rows[0].Amount);
        Assert.Equal("Utility:Trash Truck", rows[1].Account);
        Assert.Equal("2001", rows[2].CheckNumber);
        Assert.Equal("Utility:Management Fee", rows[2].Account);
    }

    [Fact]
    public void TryRead_OctoberUtilityWorkbook_MatchesThePreparedImport_WhenTheSourceIsOnDisk()
    {
        var workbook = FindRepoFile(Path.Combine("Import Data", "tow-october-2026", "nas-source", "utility", "10-october-utility.xlsx"));
        if (workbook is null)
        {
            return;
        }

        var converted = CheckListingLedgerConverter.TryRead(File.ReadAllBytes(workbook), "Utility", out var rows);

        Assert.True(converted);
        Assert.Equal(14, rows.Count);
        Assert.Equal(32926.54m, rows.Sum(row => row.Amount));
        Assert.Equal("Utility:Water Supplies", Row(rows, "12364").Account);
        Assert.Equal("Utility:Trash Truck", Row(rows, "12368").Account);
        Assert.Equal("Utility:Expenses", Row(rows, "12377").Account);
        Assert.Equal("CARRIGAN EXCAVATING", Row(rows, "12364").Name);
    }

    [Fact]
    public void TryRead_OctoberSanitationWorkbook_IncludesTheGeneralSection_WhenTheSourceIsOnDisk()
    {
        var workbook = FindRepoFile(Path.Combine("Import Data", "tow-october-2026", "nas-source", "wsd", "10-october-wsd.xlsx"));
        if (workbook is null)
        {
            return;
        }

        var converted = CheckListingLedgerConverter.TryRead(File.ReadAllBytes(workbook), "WSD", out var rows);

        Assert.True(converted);
        Assert.Equal(9, rows.Count);
        Assert.Equal("WSD:Lab", Row(rows, "5261").Account);
        Assert.Equal("WSD:Advertising", Row(rows, "5262").Account);
        Assert.Equal("WSD:Management Fee", Row(rows, "5022").Account);
        Assert.Equal("WSD:Expenses", CheckListingLedgerConverter.MapAccount("AUDIT", "WSD"));
        Assert.Equal(1821.34m, rows.Sum(row => row.Amount));
    }

    private static CheckListingRow Row(IReadOnlyList<CheckListingRow> rows, string checkNumber)
        => rows.Single(row => row.CheckNumber == checkNumber);

    private static string? FindRepoFile(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, relativePath);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        return null;
    }
}
