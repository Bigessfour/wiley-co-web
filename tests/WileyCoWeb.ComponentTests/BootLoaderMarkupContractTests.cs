namespace WileyCoWeb.ComponentTests;

/// <summary>
/// Guards the static Blazor boot shell in wwwroot/index.html so council-facing hosts never ship a broken first paint.
/// </summary>
public sealed class BootLoaderMarkupContractTests
{
    [Fact]
    public void IndexHtml_contains_app_mount_and_wiley_boot_headline()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "test-assets", "wwwroot-index.html");
        Assert.True(File.Exists(path), $"Expected copied index at {path}");

        var html = File.ReadAllText(path);
        Assert.Contains("id=\"app\"", html, StringComparison.Ordinal);
        Assert.Contains("id=\"wiley-static-boot-headline\"", html, StringComparison.Ordinal);
        Assert.Contains("Starting Wiley Widget", html, StringComparison.Ordinal);
        Assert.Contains("data-wiley-theme", html, StringComparison.Ordinal);
        Assert.Contains("blazor.webassembly.js", html, StringComparison.Ordinal);
    }
}
