import { expect, test } from "@playwright/test";
import { gotoWorkspacePanel } from "./support/workspace";

test.describe("Capital gap proof", () => {
  test("renders the capital gap panel and chart-backed breakdown", async ({
    page,
  }) => {
    await page.route("**/api/workspace/capital-gap", async (route) => {
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
          annualRateRevenue: 45000,
          annualCapitalNeed: 17500,
          rateRevenueGap: 27500,
          capitalNeedCoverageRatio: 2.57,
          capitalItemCount: 3,
          capitalStatus: "Covered",
          executiveSummary:
            "Town of Wiley FY 2026 has a 2.57x coverage ratio and positive revenue headroom.",
          generatedAtUtc: "2026-04-19T00:00:00Z",
          capitalItems: [
            {
              label: "Operating Station Rehabilitation",
              tag: "Capital",
              budgetedAmount: 17500,
              actualAmount: 4500,
              cumulativeGap: 27500,
              departmentName: "Public Works",
              accountName: "Operating Station Rehabilitation",
            },
            {
              label: "Pump Replacement",
              tag: "Capital",
              budgetedAmount: 15000,
              actualAmount: 8000,
              cumulativeGap: 10000,
              departmentName: "Public Works",
              accountName: "Pump Replacement",
            },
            {
              label: "Equipment Purchases",
              tag: "Capital",
              budgetedAmount: 12500,
              actualAmount: 5000,
              cumulativeGap: -2500,
              departmentName: "Parks",
              accountName: "Equipment Purchases",
            },
          ],
        }),
      });
    });

    await gotoWorkspacePanel(page, "/wiley-workspace/capital-gap");

    const panel = page.locator("#capital-gap-panel");

    await expect(panel).toBeVisible();
    await expect(page.locator("#capital-gap-summary-card")).toBeVisible();
    await expect(page.locator("#capital-gap-chart")).toBeVisible();
    await expect(page.locator("#capital-gap-breakdown")).toBeVisible();
    await expect(panel).toContainText("2.57x");
    await expect(panel).toContainText("Covered");
    await expect(panel).toContainText("Operating Station Rehabilitation");
    await expect(panel).toContainText("Pump Replacement");
    await expect(panel).toContainText("Equipment Purchases");
  });
});
