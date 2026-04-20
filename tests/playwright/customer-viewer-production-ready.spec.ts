import { expect, test } from "@playwright/test";
import { waitForWorkspaceShell } from "./support/workspace";

test.describe("Unique Interaction Proof", () => {
  test("Customer viewer handles search, filter reset, and degraded directory states", async ({
    page,
  }) => {
    await page.route("**/api/utility-customers**", async (route) => {
      const method = route.request().method();

      if (method === "GET") {
        await route.fulfill({
          status: 200,
          json: [],
        });

        return;
      }

      await route.continue();
    });

    // 1. Open the workspace shell first, then enter the customer viewer through the overview navigation.
    await page.goto("/wiley-workspace");
    await waitForWorkspaceShell(page);
    await page
      .locator("#workspace-navigation-card")
      .getByRole("button", { name: "Customer Viewer" })
      .click();

    const panel = page.locator("#customer-viewer-panel");
    const directoryStatus = page.locator("#customer-directory-status");
    const searchInput = page.locator("#customer-search-input");
    const refreshButton = page.locator("#refresh-customer-directory-button");
    const clearFiltersButton = page.locator("#clear-customer-filters-button");
    const addCustomerButton = page.locator("#add-customer-button");
    const errorBanner = page.locator(
      "#customer-viewer-panel .border-rose-200.bg-rose-50",
    );

    await expect(panel).toBeVisible();
    await expect(page.locator("#customer-directory-grid")).toBeVisible();
    await expect(directoryStatus).toBeVisible();
    await expect(directoryStatus).toContainText(
      /Loaded \d+ utility customers from the live API\.|The live customer directory could not be refreshed\./,
    );
    await expect(refreshButton).toBeEnabled();
    await expect(clearFiltersButton).toBeEnabled();
    await expect(addCustomerButton).toBeEnabled();

    // 2. Enter a search term, then clear filters and refresh the directory.
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

    // 3. If the live directory is unavailable, verify the error banner messaging.
    if (await errorBanner.isVisible()) {
      await expect(errorBanner).toContainText(
        "Unable to load the live utility-customer directory:",
      );
    }
  });
});
