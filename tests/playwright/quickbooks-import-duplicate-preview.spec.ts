import { Buffer } from "node:buffer";
import { expect, test } from "@playwright/test";
import { gotoWorkspacePanel } from "./support/workspace";
import { createQuickBooksCsv } from "./support/quickbooks";

test.describe("quickbooks-import-duplicate", () => {
  test("duplicate preview blocks commit and shows warning status", async ({
    page,
  }) => {
    await page.route("**/api/imports/quickbooks/preview", async (route) => {
      await route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify({
          fileName: "duplicate-test.xlsx",
          fileHash: "duplicate-playwright-hash",
          selectedEnterprise: "Water Utility",
          selectedFiscalYear: 2026,
          totalRows: 2,
          duplicateRows: 2,
          isDuplicate: true,
          statusMessage:
            "Duplicate QuickBooks import blocked. The file was already imported.",
          rows: [
            {
              rowNumber: 1,
              entryDate: "01/01/2026",
              entryType: "Invoice",
              transactionNumber: "1001",
              name: "Town of Wiley",
              memo: "Water Billing",
              accountName: "Water Revenue",
              splitAccount: "Accounts Receivable",
              amount: 125.0,
              runningBalance: 125.0,
              clearedFlag: "C",
              isDuplicate: true,
            },
            {
              rowNumber: 2,
              entryDate: "01/02/2026",
              entryType: "Payment",
              transactionNumber: "1002",
              name: "Town of Wiley",
              memo: "Payment Received",
              accountName: "Accounts Receivable",
              splitAccount: "Water Revenue",
              amount: -125.0,
              runningBalance: 0.0,
              clearedFlag: "C",
              isDuplicate: true,
            },
          ],
        }),
      });
    });

    // Additional workspace shell load mocks for reliable CI runs (prevents real degraded-mode calls from interfering with waitForWorkspaceShell).
    await page.route("**/api/workspace/knowledge**", async (route) => {
      await route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify({
          selectedEnterprise: "Water Utility",
          selectedFiscalYear: 2026,
          operationalStatus: "Ready",
          executiveSummary: "Mocked for QB duplicate test.",
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
            selectedEnterprise: "Water Utility",
            selectedFiscalYear: 2026,
          }),
        });
        return;
      }
      await route.continue();
    });

    await gotoWorkspacePanel(page, "/wiley-workspace/quickbooks-import");

    const statusHeadline = page.locator("#quickbooks-import-status-headline");
    const statusMessage = page.locator("#quickbooks-import-status-message");
    const analyzeButton = page.getByRole("button", { name: "Analyze file" });
    const commitButton = page.getByRole("button", { name: "Commit import" });

    const fileInput = page.locator("input#quickbooks-import-uploader").first();
    await fileInput.waitFor({ state: "attached", timeout: 30_000 });
    await fileInput.setInputFiles({
      name: "duplicate-test.csv",
      mimeType: "text/csv",
      buffer: Buffer.from(createQuickBooksCsv(), "utf8"),
    });

    await expect(statusHeadline).toHaveText("File selected", {
      timeout: 45_000,
    });
    await expect(statusMessage).toContainText(
      "duplicate-test.csv is ready for preview.",
      { timeout: 45_000 },
    );

    await expect(analyzeButton).toBeEnabled({ timeout: 15_000 });

    await analyzeButton.click();

    await expect(statusHeadline).toHaveText(/Duplicate detected/i, {
      timeout: 30_000,
    });
    await expect(commitButton).toBeDisabled();
    await expect(page.locator(".e-toast-content")).toContainText(
      /Duplicate QuickBooks file|duplicate/i,
    );
  });
});
