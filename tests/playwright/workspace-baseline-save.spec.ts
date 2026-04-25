import { expect, test } from "@playwright/test";
import {
  enterNumericValue,
  gotoWorkspacePanel,
  waitForWorkspaceShell,
} from "./support/workspace";

test.describe("Workspace baseline save proof", () => {
  test("Break-even save updates the API payload and reloads workspace status", async ({
    page,
  }) => {
    const initialSnapshot = {
      selectedEnterprise: "Water",
      selectedFiscalYear: 2026,
      activeScenarioName: "Base Planning Scenario",
      currentRate: 28.5,
      totalCosts: 12000,
      projectedVolume: 500,
      lastUpdatedUtc: "2026-04-19T00:00:00Z",
      enterpriseOptions: ["Water"],
      fiscalYearOptions: [2026],
      customerServiceOptions: ["All Services"],
      customerCityLimitOptions: ["All", "Yes", "No"],
      customerRows: [],
      projectionRows: [],
      scenarioItems: [],
    };

    let capturedRequest: BaselineUpdateRequest | undefined;

    await page.route("**/api/workspace/snapshot**", async (route) => {
      if (route.request().method() !== "GET") {
        await route.continue();
        return;
      }

      await route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify(initialSnapshot),
      });
    });

    await page.route("**/api/workspace/scenarios**", async (route) => {
      if (route.request().method() !== "GET") {
        await route.continue();
        return;
      }

      await route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify({ scenarios: [] }),
      });
    });

    await page.route("**/api/workspace/baseline", async (route) => {
      if (route.request().method() !== "PUT") {
        await route.continue();
        return;
      }

      const requestBody = route.request().postDataJSON() as Partial<{
        selectedEnterprise: string;
        selectedFiscalYear: number;
        currentRate: number;
        totalCosts: number;
        projectedVolume: number;
        SelectedEnterprise: string;
        SelectedFiscalYear: number;
        CurrentRate: number;
        TotalCosts: number;
        ProjectedVolume: number;
      }>;

      const selectedEnterprise =
        requestBody.selectedEnterprise ??
        requestBody.SelectedEnterprise ??
        initialSnapshot.selectedEnterprise;
      const selectedFiscalYear =
        requestBody.selectedFiscalYear ??
        requestBody.SelectedFiscalYear ??
        initialSnapshot.selectedFiscalYear;
      const currentRate =
        requestBody.currentRate ??
        requestBody.CurrentRate ??
        initialSnapshot.currentRate;
      const totalCosts =
        requestBody.totalCosts ??
        requestBody.TotalCosts ??
        initialSnapshot.totalCosts;
      const projectedVolume =
        requestBody.projectedVolume ??
        requestBody.ProjectedVolume ??
        initialSnapshot.projectedVolume;

      capturedRequest = {
        selectedEnterprise,
        selectedFiscalYear,
        currentRate,
        totalCosts,
        projectedVolume,
      };

      const responseSnapshot = {
        ...initialSnapshot,
        selectedEnterprise,
        selectedFiscalYear,
        currentRate,
        totalCosts,
        projectedVolume,
        lastUpdatedUtc: "2026-04-19T00:10:00Z",
      };

      await route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify({
          selectedEnterprise,
          selectedFiscalYear,
          savedAtUtc: "2026-04-19T00:10:00Z",
          message: `Saved baseline values for ${selectedEnterprise} FY ${selectedFiscalYear}.`,
          snapshot: responseSnapshot,
        }),
      });
    });

    // 1. Open /wiley-workspace/break-even from a blank session and confirm the baseline status is ready.
    await gotoWorkspacePanel(page, "/wiley-workspace/break-even");

    const panel = page.locator("#break-even-panel");
    const kpiGrid = page.locator("#break-even-kpi-grid");
    const status = page.locator("#baseline-save-status");
    const loadStatus = page.locator("#workspace-load-status");
    const saveButton = page.getByRole("button", { name: "Save baseline" });
    const breakEvenInputs = page.locator("#break-even-input-row input");

    await expect(panel).toBeVisible();
    await expect(kpiGrid).toBeVisible();
    await expect(status).toHaveText(
      "Baseline changes are local until you save them.",
    );
    await expect(loadStatus).toContainText("Water FY 2026");
    await expect(saveButton).toBeEnabled();

    // 2. Change the break-even inputs, then save through the shell action.
    await enterNumericValue(breakEvenInputs.nth(0), "24000");
    await enterNumericValue(breakEvenInputs.nth(1), "400");

    await expect(kpiGrid).toContainText(/Total Costs\s*\$24,000/);
    await expect(kpiGrid).toContainText(/Projected Volume\s*400/);

    await saveButton.click();

    await expect(status).toContainText(
      "Saved baseline values for Water FY 2026.",
    );
    await expect(loadStatus).toContainText(
      "Reloaded Water FY 2026 | Base Planning Scenario after baseline save.",
    );

    // 3. Verify the backend request used the edited values.
    expect(capturedRequest).toMatchObject({
      selectedEnterprise: "Water",
      selectedFiscalYear: 2026,
      currentRate: 28.5,
      totalCosts: 24000,
      projectedVolume: 400,
    });
  });

  test("Rates save persists the snapshot payload across a reload", async ({
    page,
  }) => {
    const initialSnapshot = {
      selectedEnterprise: "Water",
      selectedFiscalYear: 2026,
      activeScenarioName: "Base Planning Scenario",
      currentRate: 28.5,
      totalCosts: 12000,
      projectedVolume: 500,
      lastUpdatedUtc: "2026-04-19T00:00:00Z",
      enterpriseOptions: ["Water"],
      fiscalYearOptions: [2026],
      customerServiceOptions: ["All Services"],
      customerCityLimitOptions: ["All", "Yes", "No"],
      customerRows: [],
      projectionRows: [],
      scenarioItems: [],
    };

    let snapshotState = {
      ...initialSnapshot,
    };

    let capturedRequest: RateSnapshotSaveRequest | undefined;

    await page.route("**/api/workspace/snapshot**", async (route) => {
      if (route.request().method() !== "GET") {
        await route.continue();
        return;
      }

      await route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify(snapshotState),
      });
    });

    await page.route("**/api/workspace/scenarios**", async (route) => {
      if (route.request().method() !== "GET") {
        await route.continue();
        return;
      }

      await route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify({ scenarios: [] }),
      });
    });

    await page.route("**/api/workspace/snapshot", async (route) => {
      if (route.request().method() !== "POST") {
        await route.continue();
        return;
      }

      const requestBody = route.request().postDataJSON() as Partial<{
        selectedEnterprise: string;
        selectedFiscalYear: number;
        currentRate: number;
        totalCosts: number;
        projectedVolume: number;
        SelectedEnterprise: string;
        SelectedFiscalYear: number;
        CurrentRate: number;
        TotalCosts: number;
        ProjectedVolume: number;
      }>;

      const selectedEnterprise =
        requestBody.selectedEnterprise ??
        requestBody.SelectedEnterprise ??
        snapshotState.selectedEnterprise;
      const selectedFiscalYear =
        requestBody.selectedFiscalYear ??
        requestBody.SelectedFiscalYear ??
        snapshotState.selectedFiscalYear;
      const currentRate =
        requestBody.currentRate ??
        requestBody.CurrentRate ??
        snapshotState.currentRate;
      const totalCosts =
        requestBody.totalCosts ??
        requestBody.TotalCosts ??
        snapshotState.totalCosts;
      const projectedVolume =
        requestBody.projectedVolume ??
        requestBody.ProjectedVolume ??
        snapshotState.projectedVolume;

      capturedRequest = {
        selectedEnterprise,
        selectedFiscalYear,
        currentRate,
        totalCosts,
        projectedVolume,
      };

      snapshotState = {
        ...snapshotState,
        selectedEnterprise,
        selectedFiscalYear,
        currentRate,
        totalCosts,
        projectedVolume,
        lastUpdatedUtc: "2026-04-19T00:10:00Z",
      };

      await route.fulfill({
        status: 201,
        contentType: "application/json",
        body: JSON.stringify({
          snapshotId: 43,
          snapshotName: "Water FY2026 rate snapshot",
          savedAtUtc: "2026-04-19T00:10:00Z",
        }),
      });
    });

    // 1. Open the rates panel, change the current rate, and save the snapshot.
    await gotoWorkspacePanel(page, "/wiley-workspace/rates");

    const ratesPanel = page.locator("#rates-panel");
    const currentRateInput = page.locator("#current-rate-input");
    const comparisonChart = page.locator("#rates-comparison-chart");
    const snapshotStatus = page.locator("#snapshot-save-status");
    const saveSnapshotButton = page.getByRole("button", {
      name: "Save rate snapshot",
    });

    await expect(ratesPanel).toBeVisible();
    await expect(currentRateInput).toBeVisible();
    await expect(comparisonChart).toBeVisible();
    await expect(snapshotStatus).toContainText(/Ready to save rate snapshot/i);

    await enterNumericValue(currentRateInput, "29.50");
    await expect(currentRateInput).toHaveValue(/\$?29\.50/);

    await saveSnapshotButton.click();

    await expect(snapshotStatus).toContainText(
      "Saved Water FY2026 rate snapshot at 2026-04-19T00:10:00Z",
    );
    expect(capturedRequest).toMatchObject({
      selectedEnterprise: "Water",
      selectedFiscalYear: 2026,
      currentRate: 29.5,
      totalCosts: 12000,
      projectedVolume: 500,
    });

    // 2. Reload the workspace and confirm the saved rate still comes back from the snapshot API.
    await page.reload();
    await waitForWorkspaceShell(page);

    await expect(ratesPanel).toBeVisible();
    await expect(currentRateInput).toHaveValue(/\$?29\.50/);
    await expect(snapshotStatus).toContainText(/Ready to save rate snapshot/i);
  });
});

type BaselineUpdateRequest = {
  selectedEnterprise: string;
  selectedFiscalYear: number;
  currentRate: number;
  totalCosts: number;
  projectedVolume: number;
};

type RateSnapshotSaveRequest = {
  selectedEnterprise: string;
  selectedFiscalYear: number;
  currentRate: number;
  totalCosts: number;
  projectedVolume: number;
};
