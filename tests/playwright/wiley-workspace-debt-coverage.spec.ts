import { expect, test } from "@playwright/test";
import { enterNumericValue, gotoWorkspacePanel } from "./support/workspace";

test.describe("Wiley workspace debt coverage proof", () => {
  test("renders the DSCR dashboard and reacts to covenant threshold changes", async ({
    page,
  }) => {
    await page.route("**/api/workspace/debt-coverage", async (route) => {
      const requestBody = route.request().postDataJSON() as {
        selectedEnterprise?: string;
        selectedFiscalYear?: number;
      };

      await route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify({
          selectedEnterprise:
            requestBody?.selectedEnterprise ?? "Town of Wiley",
          selectedFiscalYear: requestBody?.selectedFiscalYear ?? 2026,
          annualRevenue: 42000,
          annualDebtService: 31250,
          reserveHeadroom: 10750,
          debtServiceCoverageRatio: 1.34,
          covenantThreshold: 1.25,
          covenantHeadroom: 0.09,
          covenantStatus: "Compliant",
          executiveSummary:
            "Town of Wiley FY 2026 posts a 1.34x DSCR against a 1.25x covenant floor.",
          generatedAtUtc: "2026-04-19T00:00:00Z",
          waterfallPoints: [
            { label: "Annual Revenue", value: 42000 },
            { label: "Debt Service", value: -31250 },
            { label: "Reserve Headroom", value: 10750 },
          ],
        }),
      });
    });

    await gotoWorkspacePanel(page, "/wiley-workspace/debt-coverage");

    const panel = page.locator("#debt-coverage-panel");
    const thresholdInput = panel.getByRole("spinbutton");

    await expect(panel).toBeVisible();
    await expect(page.locator("#debt-coverage-summary-card")).toBeVisible();
    await expect(page.locator("#debt-coverage-dscr-gauge")).toBeVisible();
    await expect(page.locator("#debt-coverage-waterfall-chart")).toBeVisible();
    await expect(panel).toContainText("1.34x");
    await expect(panel).toContainText("Compliant");

    await enterNumericValue(thresholdInput, "1.50");

    await expect(panel).toContainText("1.50x");
    await expect(panel).toContainText("At Risk");
  });
});
