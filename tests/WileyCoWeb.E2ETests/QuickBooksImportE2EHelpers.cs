using System;
using System.IO;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace WileyCoWeb.E2ETests;

internal static class QuickBooksImportE2EHelpers
{
	public static async Task UploadQuickBooksFileAsync(IPage page, string filePath, int timeoutMilliseconds)
	{
		var fileInput = page.Locator("input#quickbooks-import-uploader").First;
		var statusHeadline = page.Locator("#quickbooks-import-status-headline");
		var statusMessage = page.Locator("#quickbooks-import-status-message");

		await fileInput.WaitForAsync(new() { State = WaitForSelectorState.Attached, Timeout = timeoutMilliseconds });
		await fileInput.SetInputFilesAsync(filePath);
        await Expect(statusHeadline).ToContainTextAsync("File selected", new() { Timeout = timeoutMilliseconds, IgnoreCase = true });
        await Expect(statusMessage).ToContainTextAsync(Path.GetFileName(filePath), new() { Timeout = timeoutMilliseconds });
	}

	public static async Task<bool> WaitForPreviewReadyOrDuplicateAsync(IPage page, int timeoutMilliseconds)
	{
		const string statusHeadlineSelector = "#quickbooks-import-status-headline";
		var statusHeadline = page.Locator(statusHeadlineSelector);
		var statusMessage = page.Locator("#quickbooks-import-status-message");

		await page.WaitForFunctionAsync(
			"selector => { const headline = document.querySelector(selector)?.textContent?.trim() ?? ''; return headline.length > 0 && headline !== 'Ready' && headline !== 'File selected'; }",
			statusHeadlineSelector,
			new() { Timeout = timeoutMilliseconds });

		var headline = (await statusHeadline.InnerTextAsync()).Trim();
		var message = (await statusMessage.InnerTextAsync()).Trim();
        var previewReady = string.Equals(headline, "Preview ready", StringComparison.OrdinalIgnoreCase);
        var duplicateDetected = string.Equals(headline, "Duplicate detected", StringComparison.OrdinalIgnoreCase);

        Assert.False(string.IsNullOrWhiteSpace(message));
		Assert.True(
			previewReady || duplicateDetected,
			$"Expected QuickBooks preview to end in 'Preview ready' or 'Duplicate detected', but headline was '{headline}' and message was '{message}'.");

		return duplicateDetected;
	}

	public static async Task CommitImportIfReadyAsync(IPage page, int timeoutMilliseconds)
	{
		var commitButton = page.GetByRole(AriaRole.Button, new() { Name = "Commit import" });
		await Expect(commitButton).ToBeEnabledAsync(new() { Timeout = timeoutMilliseconds });
		await commitButton.ClickAsync();

		var commitNowButton = page.GetByRole(AriaRole.Button, new() { Name = "Commit now" });
		await Expect(commitNowButton).ToBeVisibleAsync(new() { Timeout = timeoutMilliseconds });
		await commitNowButton.ClickAsync();
        await Expect(page.Locator("#quickbooks-import-status-headline")).ToContainTextAsync("Import complete", new() { Timeout = timeoutMilliseconds, IgnoreCase = true });
    }
}