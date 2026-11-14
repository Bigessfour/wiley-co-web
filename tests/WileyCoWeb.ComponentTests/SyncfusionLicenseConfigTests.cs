using Xunit;

namespace WileyCoWeb.ComponentTests;

public sealed class SyncfusionLicenseConfigTests
{
    [Fact]
    [Trait("Category", "HighRisk")]
    public void CommittedClientSettings_DoNotContainALicenseKey()
    {
        var root = FindRepoRoot();
        foreach (var relative in new[]
        {
            Path.Combine("wwwroot", "appsettings.Production.json"),
            Path.Combine("wwwroot", "appsettings.Development.json"),
            Path.Combine("WileyCoWeb.Api", "appsettings.json"),
            Path.Combine("WileyCoWeb.Api", "appsettings.Production.json"),
        })
        {
            var path = Path.Combine(root, relative);
            Assert.True(File.Exists(path), path);
            var json = File.ReadAllText(path);
            Assert.DoesNotContain("SyncfusionLicenseKey", json, StringComparison.Ordinal);
            Assert.DoesNotContain("Ngo9" + "Big", json, StringComparison.Ordinal);
        }
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "WileyCoWeb.slnx")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Could not find WileyCoWeb.slnx from the test output directory.");
    }
}
