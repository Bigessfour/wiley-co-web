import { expect, test } from "@playwright/test";
import { gotoWorkspacePanel } from "./support/workspace";

test.describe("customer-export", () => {
  test("Customer Viewer exports filtered grid to Excel", async ({ page }) => {
    const customers = [
      {
        Id: 101,
        AccountNumber: "EXP-001",
        FirstName: "Alpha",
        LastName: "Customer",
        CompanyName: null,
        DisplayName: "Alpha Customer",
        CustomerType: "Residential",
        ServiceAddress: "100 Alpha St",
        ServiceCity: "Wiley",
        ServiceState: "CO",
        ServiceZipCode: "81092",
        ServiceLocation: "InsideCityLimits",
        Status: "Active",
        CurrentBalance: 42.5,
        AccountOpenDateUtc: "2026-01-01T00:00:00.000Z",
        PhoneNumber: "555-0101",
        EmailAddress: null,
        MeterNumber: null,
        Notes: null,
      },
      {
        Id: 102,
        AccountNumber: "EXP-002",
        FirstName: "Beta",
        LastName: "Commercial",
        CompanyName: "Beta LLC",
        DisplayName: "Beta Commercial",
        CustomerType: "Commercial",
        ServiceAddress: "200 Beta Ave",
        ServiceCity: "Wiley",
        ServiceState: "CO",
        ServiceZipCode: "81092",
        ServiceLocation: "OutsideCityLimits",
        Status: "Active",
        CurrentBalance: 120,
        AccountOpenDateUtc: "2026-02-01T00:00:00.000Z",
        PhoneNumber: "555-0102",
        EmailAddress: null,
        MeterNumber: null,
        Notes: null,
      },
    ];

    await page.route("**/api/utility-customers**", async (route) => {
      if (route.request().method() === "GET") {
        await route.fulfill({
          status: 200,
          contentType: "application/json",
          body: JSON.stringify(customers),
        });
        return;
      }

      await route.continue();
    });

    // Additional workspace shell load mocks for reliable CI runs (prevents real degraded-mode calls from interfering with waitForWorkspaceShell).
    await page.route("**/api/workspace/knowledge**", async (route) => {
      await route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify({
          selectedEnterprise: "Town of Wiley",
          selectedFiscalYear: 2026,
          operationalStatus: "Ready",
          executiveSummary: "Mocked for export test.",
          rateRationale: "",
          currentRate: 27.5,
          totalCosts: 12500,
          projectedVolume: 450,
          scenarioCostTotal: 0,
          breakEvenRate: 30.25,
          adjustedBreakEvenRate: 30.25,
          rateGap: 2.75,
          adjustedRateGap: 2.75,
          monthlyRevenue: 12375,
          netPosition: 0,
          coverageRatio: 1.0,
          currentReserveBalance: 7200,
          recommendedReserveLevel: 6800,
          reserveRiskAssessment: "Low",
          generatedAtUtc: "2026-04-19T00:00:00Z",
          insights: [],
          recommendedActions: [],
          topVariances: [],
        }),
      });
    });

    await page.route("**/api/ai/health**", async (route) => {
      await route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify({
          status: "healthy",
          semanticKernelAvailable: false,
          latestAnswerSource: "deterministic_fallback",
          latestUsedFallback: true,
          latestFailureCode: null,
          lastTurnAtUtc: null,
        }),
      });
    });

    await page.route("**/api/workspace/snapshot**", async (route) => {
      if (route.request().method() === "GET") {
        await route.fulfill({
          status: 200,
          contentType: "application/json",
          body: JSON.stringify({
            id: "mock",
            selectedEnterprise: "Town of Wiley",
            selectedFiscalYear: 2026,
          }),
        });
        return;
      }
      await route.continue();
    });

    await gotoWorkspacePanel(page, "/wiley-workspace/customers");

    const exportButton = page.locator("#export-customer-grid-excel-button");
    const directoryStatus = page.locator("#customer-directory-status");
    const searchInput = page.locator("#customer-search-input");

    await expect(exportButton).toBeVisible();
    await expect(exportButton).toHaveText("Export to Excel");
    await expect(page.locator("#customer-directory-grid")).toBeVisible();

    // Stabilize: wait for the mocked customer rows to render in the grid before filtering/export.
    await expect(page.locator("#customer-directory-grid")).toContainText(
      "Alpha Customer",
      { timeout: 10_000 },
    );

    await searchInput.fill("Alpha");
    await expect(searchInput).toHaveValue("Alpha");

    const downloadPromise = page.waitForEvent("download");
    await exportButton.click();

    const download = await downloadPromise;
    expect(download.suggestedFilename()).toMatch(/customers.*\.xlsx/i);

    await expect(directoryStatus).toContainText(
      "Customer directory Excel export generated.",
    );
  });
});
