import { Buffer } from "node:buffer";
import { expect, test } from "@playwright/test";
import {
  breakEvenPanelSpinbuttons,
  enterScenarioGridDialogCost,
  gotoWorkspacePanel,
  ratesPanelCurrentRateInput,
  readCurrencyValueByLabel,
  setNumericInputValue,
  waitForWorkspaceShell,
} from "./support/workspace";

test.describe("Wiley workspace browser depth", () => {
  test.describe.configure({ mode: "serial" });

  test("break-even recalculates cards when baseline inputs change", async ({
    page,
  }) => {
    await gotoWorkspacePanel(page, "/wiley-workspace/break-even");

    const kpiGrid = page.locator("#break-even-kpi-grid");
    const breakEvenSpinners = breakEvenPanelSpinbuttons(page);
    await setNumericInputValue(breakEvenSpinners.nth(0), "24000");
    await setNumericInputValue(breakEvenSpinners.nth(1), "400");

    await expect
      .poll(
        async () => {
          const t = await kpiGrid.innerText();
          return (
            /Total Costs[\s\S]*\$24,000/.test(t) &&
            /Projected Volume[\s\S]*\b400\b/.test(t) &&
            /Recommended Rate[\s\S]*\$60\.00/.test(t)
          );
        },
        {
          timeout: 25_000,
          message: "Break-even KPIs should reflect 24000 / 400 => $60.00",
        },
      )
      .toBe(true);
  });

  test("rates panel updates the current-rate KPI when the editor changes", async ({
    page,
  }) => {
    await gotoWorkspacePanel(page, "/wiley-workspace/rates");

    await setNumericInputValue(ratesPanelCurrentRateInput(page), "29.50");

    await expect(page.locator("#rates-kpi-grid")).toContainText(
      /Current Rate\s*\$29\.50/,
    );
    await expect(page.locator("#rates-comparison-chart")).toBeVisible();
  });

  test("scenario planner updates derived totals when a cost item is added", async ({
    page,
  }) => {
    await gotoWorkspacePanel(page, "/wiley-workspace/scenario");

    const metrics = page.locator("#scenario-metrics-panel");
    const grid = page.locator("#scenario-grid");
    const dialog = page
      .getByRole("dialog")
      .filter({ has: page.locator('input[name="Name"]') })
      .filter({ has: page.locator('input[name="Cost"]') });
    const initialScenarioCostTotal = await readCurrencyValueByLabel(
      metrics,
      "Scenario Cost Total",
    );
    const initialScenarioBreakEven = await readCurrencyValueByLabel(
      metrics,
      "Scenario Break-Even",
    );
    const scenarioItemName = `Live test ${Date.now()}`;

    await page.getByRole("button", { name: "Add" }).click();
    await expect(dialog).toBeVisible();
    await dialog.locator('input[name="Name"]').fill(scenarioItemName);
    await enterScenarioGridDialogCost(dialog, "1234");
    await dialog.getByRole("button", { name: "Save" }).click();

    await expect(grid).toContainText(scenarioItemName);
    await expect
      .poll(() => readCurrencyValueByLabel(metrics, "Scenario Cost Total"))
      .toBe(initialScenarioCostTotal + 1234);
    await expect
      .poll(
        async () =>
          (await readCurrencyValueByLabel(metrics, "Scenario Break-Even")) >
          initialScenarioBreakEven,
      )
      .toBe(true);
  });

  test("workspace shell resolves startup state and keeps the overview usable", async ({
    page,
  }) => {
    await page.goto("/wiley-workspace");
    await waitForWorkspaceShell(page);

    const statusCard = page.locator("#workspace-status-card");
    const loadStatus = page.locator("#workspace-load-status");

    await expect(statusCard).toBeVisible();
    await expect(loadStatus).toHaveText(
      /Workspace ready\.|Loaded .*from the workspace API|Loaded .*snapshot/i,
    );
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
