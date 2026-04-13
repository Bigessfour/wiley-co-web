using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace WileyCoWeb.E2ETests;

internal static class VisualTestHarness
{
    private const string WorkspaceOverviewPath = "/wiley-workspace";

    internal static void EnsureConfigured(string? baseUrl, string? applitoolsKey)
    {
        if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(applitoolsKey))
        {
            throw Xunit.Sdk.SkipException.ForSkip(
                "Visual tests require both WILEYCO_E2E_BASE_URL and APPLITOOLS_API_KEY.");
        }
    }

    internal static void AttachDiagnostics(IPage page, List<string> consoleMessages, List<string> pageErrors)
    {
        page.Console += (_, message) => consoleMessages.Add($"{message.Type}: {message.Text}");
        page.PageError += (_, exception) => pageErrors.Add(exception);
    }

    internal static async Task LoadWorkspaceAsync(
        IPage page,
        string baseUrl,
        string targetPath,
        int readyTimeoutMilliseconds,
        int navigationTimeoutMilliseconds)
    {
        await page.GotoAsync(
            $"{baseUrl.TrimEnd('/')}/",
            new() { WaitUntil = WaitUntilState.DOMContentLoaded });

        await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);
        await WaitForWorkspaceReadyAsync(page, readyTimeoutMilliseconds);

        if (!string.Equals(targetPath, WorkspaceOverviewPath, StringComparison.Ordinal))
        {
            await NavigateWithinWorkspaceAsync(page, targetPath, navigationTimeoutMilliseconds);
            await WaitForWorkspaceSettledAsync(page, navigationTimeoutMilliseconds);
        }

        var statusText = await page.Locator("#workspace-load-status").InnerTextAsync();
        Assert.DoesNotContain("failed", statusText, StringComparison.OrdinalIgnoreCase);
    }

    internal static string BuildDiagnostics(
        IPage page,
        IReadOnlyCollection<string> consoleMessages,
        IReadOnlyCollection<string> pageErrors)
    {
        var safeConsoleMessages = consoleMessages.Count > 0
            ? consoleMessages.Select(message => $"- {message}")
            : ["- <none>"];
        var safePageErrors = pageErrors.Count > 0
            ? pageErrors.Select(error => $"- {error}")
            : ["- <none>"];

        return string.Join(Environment.NewLine, [
            $"Page URL: {page.Url}",
            "Console messages:",
            .. safeConsoleMessages,
            "Page errors:",
            .. safePageErrors
        ]);
    }

    private static async Task WaitForWorkspaceReadyAsync(IPage page, int timeoutMilliseconds)
    {
        await Expect(page.Locator("#workspace-load-status"))
            .ToContainTextAsync("Workspace ready.", new() { Timeout = timeoutMilliseconds });
    }

    private static async Task NavigateWithinWorkspaceAsync(IPage page, string targetPath, int timeoutMilliseconds)
    {
        var navLink = page.Locator($"a[href='{targetPath}']").First;

        if (await navLink.CountAsync() > 0)
        {
            await navLink.ClickAsync();
        }
        else
        {
            await page.EvaluateAsync(
                @"path => {
                    if (window.location.pathname === path) {
                        return;
                    }

                    window.history.pushState({}, '', path);
                    window.dispatchEvent(new PopStateEvent('popstate'));
                }",
                targetPath);
        }

        await page.WaitForFunctionAsync(
            "path => window.location.pathname === path",
            targetPath,
            new PageWaitForFunctionOptions { Timeout = timeoutMilliseconds });
    }

    private static async Task WaitForWorkspaceSettledAsync(IPage page, int timeoutMilliseconds)
    {
        await page.WaitForFunctionAsync(
            @"() => {
                const el = document.getElementById('workspace-load-status');
                if (!el) return false;
                const txt = (el.innerText || '').trim();
                return txt.length > 0
                    && !txt.includes('pending')
                    && !txt.includes('Loading ');
            }",
            null,
            new PageWaitForFunctionOptions { Timeout = timeoutMilliseconds });
    }
}