using Microsoft.Extensions.Logging;
using Moq;
using WileyWidget.Services;

namespace WileyWidget.Tests;

public sealed class AppEventBusTests
{
    [Fact]
    public void Publish_DoesNothing_WhenThereAreNoSubscribers()
    {
        var bus = new AppEventBus();

        bus.Publish("alpha");
    }

    [Fact]
    public void Publish_InvokesSubscribedHandler()
    {
        var bus = new AppEventBus();
        var values = new List<string>();

        bus.Subscribe<string>(values.Add);
        bus.Publish("alpha");

        Assert.Equal(new[] { "alpha" }, values);
    }

    [Fact]
    public void Unsubscribe_RemovesSubscribedHandler()
    {
        var bus = new AppEventBus();
        var values = new List<string>();
        Action<string> handler = values.Add;

        bus.Subscribe(handler);
        bus.Unsubscribe(handler);
        bus.Publish("alpha");

        Assert.Empty(values);
    }

    [Fact]
    public void Publish_SwallowsHandlerExceptions()
    {
        var logger = new Mock<ILogger<AppEventBus>>();
        var bus = new AppEventBus(logger.Object);

        bus.Subscribe<string>(_ => throw new InvalidOperationException("boom"));

        var exception = Record.Exception(() => bus.Publish("alpha"));

        Assert.Null(exception);
    }
}