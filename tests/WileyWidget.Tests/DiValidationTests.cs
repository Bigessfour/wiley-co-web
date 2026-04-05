using Moq;
using Xunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using WileyWidget.Services;
using WileyWidget.Services.Abstractions;
using WileyWidget.Business.Interfaces;
using Microsoft.Extensions.Logging;

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
}
