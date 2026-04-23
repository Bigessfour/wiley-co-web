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
        var check = CreateCheck(() => null);

        var result = await check.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Degraded, result.Status);
        Assert.Equal(false, result.Data["LicenseKeyPresent"]);
        Assert.Contains("not found", result.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CheckHealthAsync_ReturnsDegraded_WhenLicenseKeyIsTooShort()
    {
        var check = CreateCheck(() => new string('x', 24));

        var result = await check.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Degraded, result.Status);
        Assert.Equal(true, result.Data["LicenseKeyPresent"]);
        Assert.Contains("too short", result.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CheckHealthAsync_ReturnsHealthy_WhenLicenseKeyIsPresent_AndSyncfusionAssemblyIsLoaded()
    {
        _ = typeof(PdfDocument).Assembly.FullName;
        var check = CreateCheck(() => new string('x', 88));

        var result = await check.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Healthy, result.Status);
        Assert.Equal(true, result.Data["LicenseKeyPresent"]);
        Assert.NotEqual("Unknown", result.Data["SyncfusionVersion"]);
        Assert.Contains("appears valid", result.Description, StringComparison.OrdinalIgnoreCase);
    }

    private static SyncfusionLicenseHealthCheck CreateCheck(Func<string?> licenseKeyResolver)
        => new(new Mock<ILogger<SyncfusionLicenseHealthCheck>>().Object, licenseKeyResolver);
}

[CollectionDefinition("Syncfusion license health", DisableParallelization = true)]
public sealed class SyncfusionLicenseHealthCollectionDefinition
{
}