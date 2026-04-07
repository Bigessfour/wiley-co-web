using Moq;
using Xunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using WileyWidget.Services;
using WileyWidget.Services.Abstractions;
using WileyWidget.Business.Interfaces;
using Microsoft.Extensions.Logging;
using System.Reflection;

namespace WileyWidget.Tests;

public class DiValidationTests
{
    [Fact]
    public void CoreServices_CanBeResolved()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();
        var analyticsRepositoryMock = new Mock<IAnalyticsRepository>();

        // Register mocks for external dependencies
        services.AddSingleton(new Mock<IBudgetRepository>().Object);
        services.AddSingleton(analyticsRepositoryMock.Object);
        services.AddSingleton(new Mock<IBudgetAnalyticsRepository>().Object);
        services.AddSingleton(new Mock<ILogger<AnalyticsService>>().Object);
        services.AddSingleton(new Mock<ILogger<FileImportService>>().Object);

        // Act
        services.AddWileyWidgetCoreServices(configuration);

        // Ensure test doubles are used for repositories registered by the extension.
        services.AddSingleton<IAnalyticsRepository>(analyticsRepositoryMock.Object);
        var provider = services.BuildServiceProvider();

        // Assert
        Assert.NotNull(provider.GetService<IAnalyticsService>());
        Assert.NotNull(provider.GetService<IAppEventBus>());
        Assert.NotNull(provider.GetService<IFileImportService>());
    }

    [Fact]
    public void ValidateCoreServices_ReturnsTrue_ForRegisteredCoreServiceSet()
    {
        var validator = new DiValidationService(Mock.Of<ILogger<DiValidationService>>());

        Assert.True(validator.ValidateCoreServices());
    }

    [Fact]
    public void GetDiscoveredServiceInterfaces_ReturnsExpectedKnownInterfaces()
    {
        var validator = new DiValidationService(Mock.Of<ILogger<DiValidationService>>());
        var interfaces = validator.GetDiscoveredServiceInterfaces(new[]
        {
            typeof(IFileImportService).Assembly,
            typeof(WileyWidgetServicesExtensions).Assembly
        }).ToArray();

        Assert.Contains(typeof(IFileImportService).FullName!, interfaces);
        Assert.Contains(typeof(IAnalyticsService).FullName!, interfaces);
        Assert.Contains(typeof(IAnalyticsRepository).FullName!, interfaces);
    }

    [Fact]
    public void ValidateRegistrations_FindsKnownCoreImplementations()
    {
        var validator = new DiValidationService(Mock.Of<ILogger<DiValidationService>>());
        var report = validator.ValidateRegistrations(new[]
        {
            typeof(IFileImportService).Assembly,
            typeof(WileyWidgetServicesExtensions).Assembly
        });

        Assert.Contains(typeof(IFileImportService).FullName!, report.ResolvedServices);
        Assert.Contains(typeof(IAnalyticsService).FullName!, report.ResolvedServices);
        Assert.Contains(typeof(IAnalyticsRepository).FullName!, report.ResolvedServices);
        Assert.Contains(typeof(IAppEventBus).FullName!, report.ResolvedServices);
        Assert.NotEmpty(report.ResolvedServices);
    }
}
