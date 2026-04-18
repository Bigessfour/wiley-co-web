using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Moq;
using Syncfusion.Pdf;
using global::WileyWidget.Services.HealthChecks;
using Xunit;

namespace WileyWidget.Tests;

[Collection("Syncfusion license health")]
public sealed class SyncfusionLicenseHealthCheckTests
{
    [Fact]
    public async Task CheckHealthAsync_ReturnsDegraded_WhenLicenseKeyIsMissing()
    {
        using var scope = new SyncfusionLicenseEnvironmentScope(null);
        var check = CreateCheck();

        var result = await check.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Degraded, result.Status);
        Assert.Equal(false, result.Data["LicenseKeyPresent"]);
        Assert.Contains("not found", result.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CheckHealthAsync_ReturnsDegraded_WhenLicenseKeyIsTooShort()
    {
        using var scope = new SyncfusionLicenseEnvironmentScope(new string('x', 24));
        var check = CreateCheck();

        var result = await check.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Degraded, result.Status);
        Assert.Equal(true, result.Data["LicenseKeyPresent"]);
        Assert.Contains("too short", result.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CheckHealthAsync_ReturnsHealthy_WhenLicenseKeyIsPresent_AndSyncfusionAssemblyIsLoaded()
    {
        _ = typeof(PdfDocument).Assembly.FullName;
        using var scope = new SyncfusionLicenseEnvironmentScope(new string('x', 88));
        var check = CreateCheck();

        var result = await check.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Healthy, result.Status);
        Assert.Equal(true, result.Data["LicenseKeyPresent"]);
        Assert.NotEqual("Unknown", result.Data["SyncfusionVersion"]);
        Assert.Contains("appears valid", result.Description, StringComparison.OrdinalIgnoreCase);
    }

    private static SyncfusionLicenseHealthCheck CreateCheck()
        => new(new Mock<ILogger<SyncfusionLicenseHealthCheck>>().Object);

    private sealed class SyncfusionLicenseEnvironmentScope : IDisposable
    {
        private readonly string? previousCanonical;
        private readonly string? previousLegacy;
        private readonly string? previousConfiguration;

        public SyncfusionLicenseEnvironmentScope(string? licenseKey)
        {
            previousCanonical = Environment.GetEnvironmentVariable("SYNCFUSION_LICENSE_KEY");
            previousLegacy = Environment.GetEnvironmentVariable("SYNCUSION_LICENSE_KEY");
            previousConfiguration = Environment.GetEnvironmentVariable("Syncfusion__LicenseKey");

            Environment.SetEnvironmentVariable("SYNCFUSION_LICENSE_KEY", licenseKey);
            Environment.SetEnvironmentVariable("SYNCUSION_LICENSE_KEY", null);
            Environment.SetEnvironmentVariable("Syncfusion__LicenseKey", null);
        }

        public void Dispose()
        {
            Environment.SetEnvironmentVariable("SYNCFUSION_LICENSE_KEY", previousCanonical);
            Environment.SetEnvironmentVariable("SYNCUSION_LICENSE_KEY", previousLegacy);
            Environment.SetEnvironmentVariable("Syncfusion__LicenseKey", previousConfiguration);
        }
    }
}

[CollectionDefinition("Syncfusion license health", DisableParallelization = true)]
public sealed class SyncfusionLicenseHealthCollectionDefinition
{
}