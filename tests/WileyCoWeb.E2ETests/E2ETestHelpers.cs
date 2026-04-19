using Microsoft.Playwright;

namespace WileyCoWeb.E2ETests;

internal static class E2ETestHelpers
{
    public static async Task EnterNumericValueAsync(ILocator input, string value)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        await input.ClickAsync();
        await input.SelectTextAsync();
        await input.PressAsync("Backspace");
        await input.PressSequentiallyAsync(value, new() { Delay = 20 });
        await input.PressAsync("Tab");
    }
}