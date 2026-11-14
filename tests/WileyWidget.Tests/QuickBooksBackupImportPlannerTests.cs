using WileyWidget.Services;

namespace WileyWidget.Tests;

public sealed class QuickBooksBackupImportPlannerTests
{
    [Fact]
    public void Plan_MapsFundFolders_AndSkipsEverythingElse()
    {
        var plan = QuickBooksBackupImportPlanner.Plan(
        [
            new QuickBooksBackupFile("C/Users/Town of Wiley/Documents/CHECK LISTING- UTILITY/october.csv", 100),
            new QuickBooksBackupFile("C/Users/Town of Wiley/Documents/BROOKSIDE CHECK LISTING/sept.xlsx", 100),
            new QuickBooksBackupFile("C/Users/Town of Wiley/Documents/CHECK LISTING- WSD/may.csv", 100),
            new QuickBooksBackupFile("C/Users/Town of Wiley/Documents/CHECK LISTING- UTILITY/trash-dump.csv", 100),
            new QuickBooksBackupFile("C/Users/Town of Wiley/Documents/CHECK LISTING- GENERAL/water-trash.csv", 100),
            new QuickBooksBackupFile("C/Users/Town of Wiley/Documents/CHECK LISTING- GENERAL/june.csv", 100),
            new QuickBooksBackupFile("C/Users/Town of Wiley/Documents/water bill.csv", 100),
            new QuickBooksBackupFile("C/Users/Town of Wiley/AppData/Local/export.csv", 100),
            new QuickBooksBackupFile("C/Users/Town of Wiley/Documents/Quick Books Companies/town.qbw", 100)
        ]);

        Assert.Equal("Water Utility", plan[0].Enterprise);
        Assert.Equal("Apartments", plan[1].Enterprise);
        Assert.Equal("Wiley Sanitation District", plan[2].Enterprise);
        Assert.Equal("Water Utility", plan[3].Enterprise);
        Assert.Null(plan[3].SkipReason);
        Assert.Equal("no-enterprise", plan[4].SkipReason);
        Assert.Equal("no-enterprise", plan[5].SkipReason);
        Assert.Equal("not-a-ledger-folder", plan[6].SkipReason);
        Assert.Equal("skipped-folder", plan[7].SkipReason);
        Assert.Equal("unsupported-extension", plan[8].SkipReason);
    }
}
