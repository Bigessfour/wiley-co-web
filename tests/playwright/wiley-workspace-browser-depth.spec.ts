import { Buffer } from "node:buffer";
import { expect, test } from "@playwright/test";

test.describe("Wiley workspace browser depth", () => {
  test("workspace shell resolves startup state and keeps the overview usable", async ({
    page,
  }) => {
    await page.goto("/wiley-workspace");

    const statusCard = page.locator("#workspace-status-card");
    const loadStatus = page.locator("#workspace-load-status");

    await expect(statusCard).toBeVisible();
    await expect(loadStatus).toContainText("Workspace ready.");
    await expect(statusCard).toContainText("Startup source:");
    await expect(statusCard).toContainText("Current state:");
    await expect(statusCard).not.toContainText(/pending/i);

    await expect(page.locator("#workspace-overview-dashboard")).toBeVisible();
    await expect(page.locator("#overview-break-even")).toBeVisible();
    await expect(page.locator("#overview-import")).toBeVisible();
  });

  test("customer viewer stays operable when the live directory is empty or degraded", async ({
    page,
  }) => {
    await page.goto("/wiley-workspace/customers");

    const directoryStatus = page.locator("#customer-directory-status");
    const searchInput = page.locator("#customer-search-input");
    const refreshButton = page.locator("#refresh-customer-directory-button");
    const clearFiltersButton = page.locator("#clear-customer-filters-button");
    const addCustomerButton = page.locator("#add-customer-button");
    const errorBanner = page.locator(
      "#customer-viewer-panel .border-rose-200.bg-rose-50",
    );

    await expect(page.locator("#customer-viewer-panel")).toBeVisible();
    await expect(page.locator("#customer-directory-grid")).toBeVisible();
    await expect(directoryStatus).toBeVisible();
    await expect(directoryStatus).not.toContainText(
      "Loading live utility-customer directory...",
    );

    await expect(refreshButton).toBeEnabled();
    await expect(clearFiltersButton).toBeEnabled();
    await expect(addCustomerButton).toBeEnabled();

    await searchInput.fill("delta");
    await expect(searchInput).toHaveValue("delta");

    await clearFiltersButton.click();
    await expect(searchInput).toHaveValue("");

    await refreshButton.click();
    await expect(directoryStatus).not.toContainText(
      "Loading live utility-customer directory...",
    );

    const statusText = await directoryStatus.innerText();
    expect(statusText).toMatch(
      /Refreshed the live utility-customer directory\.|Loaded \d+ utility customers from the live API\.|The live customer directory could not be refreshed\./,
    );

    if (await errorBanner.isVisible()) {
      await expect(errorBanner).toContainText(
        "Unable to load the live utility-customer directory:",
      );
    }
  });

  test("quickbooks import rejects unsupported uploads and reset restores the idle state", async ({
    page,
  }) => {
    await page.goto("/wiley-workspace/quickbooks-import");

    const statusHeadline = page.locator("#quickbooks-import-status-headline");
    const statusMessage = page.locator("#quickbooks-import-status-message");
    const assistantContext = page.locator(
      "#quickbooks-assistant-context-summary",
    );
    const analyzeButton = page.getByRole("button", { name: "Analyze file" });
    const commitButton = page.getByRole("button", { name: "Commit import" });
    const resetButton = page.getByRole("button", { name: "Reset panel" });

    await expect(statusHeadline).toHaveText("Ready");
    await expect(statusMessage).toContainText(
      "Choose a QuickBooks export to begin.",
    );
    await expect(assistantContext).toContainText(
      "No QuickBooks preview has been loaded yet.",
    );
    await expect(analyzeButton).toBeDisabled();
    await expect(commitButton).toBeDisabled();
    await expect(resetButton).toBeEnabled();

    await page.locator("#quickbooks-import-uploader").setInputFiles({
      name: "not-quickbooks.txt",
      mimeType: "text/plain",
      buffer: Buffer.from("This is not a QuickBooks export.", "utf8"),
    });

    await expect(statusHeadline).toHaveText("File selected");
    await expect(statusMessage).toContainText(
      "not-quickbooks.txt is ready for preview.",
    );
    await expect(analyzeButton).toBeEnabled();

    await analyzeButton.click();

    await expect(statusHeadline).toHaveText("Unsupported file type");
    await expect(statusMessage).toContainText(
      "QuickBooks imports support CSV or Excel files only.",
    );
    await expect(commitButton).toBeDisabled();
    await expect(assistantContext).toContainText(
      "No QuickBooks preview has been loaded yet.",
    );

    await resetButton.click();

    await expect(statusHeadline).toHaveText("Ready");
    await expect(statusMessage).toContainText(
      "Choose a QuickBooks export to begin.",
    );
    await expect(analyzeButton).toBeDisabled();
    await expect(commitButton).toBeDisabled();
  });
});
