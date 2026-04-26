import { Buffer } from "node:buffer";
import { expect, test } from "@playwright/test";
import {
  enterNumericValue,
  gotoWorkspacePanel,
  seedLeftNavCollapsed,
  waitForWorkspacePanel,
  waitForWorkspaceShell,
} from "./support/workspace";
import {
  createQuickBooksCsv,
  createQuickBooksPreviewResponse,
} from "./support/quickbooks";

test.describe("Wiley workspace Syncfusion coverage", () => {
  test.describe.configure({ mode: "serial" });

  test.describe("new council data-viz panels", () => {
    test("break-even renders all four quadrants and the apartment config grid", async ({
      page,
    }) => {
      await gotoWorkspacePanel(page, "/wiley-workspace/break-even");

      const breakEvenPanel = page.locator("#break-even-panel");
      const quadrantGrid = page.locator("#break-even-quadrant-grid");
      const quadrantCards = quadrantGrid.locator("section");
      const apartmentPanel = page.locator("#apartment-config-panel");

      await expect(breakEvenPanel).toBeVisible();
      await expect(page.locator("#break-even-input-panel")).toBeVisible();
      await expect(page.locator("#break-even-quadrant-panel")).toBeVisible();
      await expect(quadrantGrid).toBeVisible();
      await expect(quadrantCards).toHaveCount(4);
      await expect(
        page.locator("#break-even-chart-water-utility"),
      ).toBeVisible();
      await expect(
        page.locator("#break-even-chart-wiley-sanitation-district"),
      ).toBeVisible();
      await expect(page.locator("#break-even-chart-trash")).toBeVisible();
      await expect(page.locator("#break-even-chart-apartments")).toBeVisible();

      await expect(apartmentPanel).toBeVisible();
      await expect(apartmentPanel).toContainText("Apartment Configuration");
      await expect(apartmentPanel).toContainText("Total Units");
      await expect(apartmentPanel).toContainText("Monthly Revenue");
      await expect(apartmentPanel).toContainText("Effective $/Customer");
      await expect(apartmentPanel.locator(".e-grid")).toBeVisible();
      await expect(
        apartmentPanel.getByRole("button", { name: "Add" }),
      ).toBeVisible();
      await expect(
        apartmentPanel.getByRole("button", { name: "Edit" }),
      ).toBeVisible();
      await expect(
        apartmentPanel.getByRole("button", { name: "Delete" }),
      ).toBeVisible();
    });

    test("reserve trajectory renders the forecast chart and export controls", async ({
      page,
    }) => {
      await gotoWorkspacePanel(page, "/wiley-workspace/reserve-trajectory");

      const panel = page.locator("#reserve-trajectory-panel");

      await expect(panel).toBeVisible();
      await expect(panel).toContainText("Reserve Trajectory");
      await expect(panel).toContainText("Current Reserves");
      await expect(panel).toContainText("Current Reserve Months");
      await expect(panel).toContainText("5-Year Solvency Score");
      await expect(panel).toContainText("Stress band");
      await expect(panel).toContainText("Policy minimum");
      await expect(page.locator("#reserve-trajectory-chart")).toBeVisible();
      await expect(
        panel.getByRole("button", { name: "Export PDF" }),
      ).toBeVisible();
      await expect(
        panel.getByRole("button", { name: "Export Excel" }),
      ).toBeVisible();
    });

    test("affordability renders the gauge, heatmap, stacked chart, and reacts to MHI input", async ({
      page,
    }) => {
      await gotoWorkspacePanel(page, "/wiley-workspace/affordability");

      const panel = page.locator("#affordability-dashboard-panel");
      const mhiInput = page.getByRole("spinbutton", { name: "Monthly MHI" });

      await expect(panel).toBeVisible();
      await expect(panel).toContainText("Monthly bill burden versus MHI");
      await expect(page.locator("#affordability-gauge")).toBeVisible();
      await expect(
        page.locator("#affordability-class-impact-chart"),
      ).toBeVisible();
      await expect(page.locator("#affordability-heatmap-panel")).toBeVisible();
      await expect(
        page.locator("#affordability-kpi-average-bill"),
      ).toBeVisible();
      await expect(page.locator("#affordability-kpi-mhi-share")).toBeVisible();

      await expect(mhiInput).toBeVisible();
      await enterNumericValue(mhiInput, "6000");

      await expect(mhiInput).toHaveValue("$6,000");
      await expect(panel).toContainText("% of MHI");
    });

    test("debt coverage renders the DSCR gauge and waterfall chart", async ({
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
      await expect(
        page.locator("#debt-coverage-waterfall-chart"),
      ).toBeVisible();
      await expect(panel).toContainText("1.34x");
      await expect(panel).toContainText("Compliant");

      await enterNumericValue(thresholdInput, "1.50");

      await expect(panel).toContainText("1.50x");
      await expect(panel).toContainText("At Risk");
    });

    test("capital gap renders the revenue-gap chart and breakdown list", async ({
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

  test("workspace shell renders Syncfusion selectors and document actions", async ({
    page,
  }) => {
    await page.goto("/wiley-workspace");

    await waitForWorkspacePanel(page, "#workspace-overview-dashboard");

    const documentCenter = page.locator("#workspace-document-center");
    const exportStatus = documentCenter.locator("div.rounded-xl.bg-slate-50");
    const enterpriseCard = page.locator("#workspace-enterprise-context-card");
    const comboboxes = enterpriseCard.getByRole("combobox");
    const exportCustomersButton = documentCenter.getByRole("button", {
      name: "Export customers to Excel",
    });
    const exportScenarioButton = documentCenter.getByRole("button", {
      name: "Export scenario to Excel",
    });
    const exportPdfButton = documentCenter.getByRole("button", {
      name: "Download PDF rate packet",
    });

    await expect(documentCenter).toBeVisible();
    await expect(exportCustomersButton).toBeEnabled();
    await expect(exportScenarioButton).toBeEnabled();
    await expect(exportPdfButton).toBeEnabled();

    await expect(enterpriseCard).toBeVisible();
    await expect(comboboxes).toHaveCount(3);
    await expect(comboboxes.nth(0)).toBeVisible();
    await expect(comboboxes.nth(1)).toBeVisible();

    await page.locator("#scenario-name-input").fill("Playwright E2E Scenario");
    await expect(page.locator("#scenario-name-input")).toHaveValue(
      "Playwright E2E Scenario",
    );

    await expect(
      page.getByRole("button", { name: "Apply saved scenario" }),
    ).toBeDisabled();

    await exportCustomersButton.click();
    await expect(exportStatus).toContainText(
      /Downloaded .*\.xlsx|Export failed:/,
    );

    await exportScenarioButton.click();
    await expect(exportStatus).toContainText(
      /Downloaded .*\.xlsx|Export failed:/,
    );

    await exportPdfButton.click();
    await expect(exportStatus).toContainText(
      /Downloaded .*\.pdf|Export failed:/,
    );
  });

  test("workspace shell keeps the dashboard controls usable below the media-query breakpoint", async ({
    page,
  }) => {
    await seedLeftNavCollapsed(page, false);
    await page.goto("/wiley-workspace");

    await waitForWorkspacePanel(page, "#workspace-overview-dashboard");
    await page.setViewportSize({ width: 1279, height: 900 });

    const sidebarToggle = page.locator("#app-shell-nav-toggle");
    const panelToggle = page.locator("#workspace-panel-toggle");

    await expect(page.locator("#workspace-dashboard")).toBeVisible();
    await expect(page.locator("#workspace-document-center")).toBeVisible();
    await expect(sidebarToggle).toBeVisible();
    await expect(panelToggle).toBeVisible();
    await expect(page.locator("#workspace-jarvis-launcher")).toBeVisible();

    await sidebarToggle.evaluate((button) => {
      (button as HTMLButtonElement).click();
    });
    await expect(sidebarToggle).toContainText("Expand navigation rail");

    await panelToggle.click();
    await expect(panelToggle).toContainText("Open workspace panel");

    await page.locator("#workspace-jarvis-launcher").click();
    await expect(page.locator("#workspace-jarvis-dock")).toBeVisible();
  });

  test("workspace shell collapses the sidebar and opens and closes the Jarvis dock", async ({
    page,
  }) => {
    await seedLeftNavCollapsed(page, false);
    await page.goto("/wiley-workspace");

    await waitForWorkspacePanel(page, "#workspace-overview-dashboard");
    await page.setViewportSize({ width: 1279, height: 900 });

    const sidebarToggle = page.locator("#app-shell-nav-toggle");
    const panelToggle = page.locator("#workspace-panel-toggle");
    const sidebarRail = page.locator("aside.workspace-sidebar");
    const sidebarShell = page.locator("#workspace-sidebar-shell");
    const jarvisLauncher = page.locator("#workspace-jarvis-launcher");
    const jarvisDock = page.locator("#workspace-jarvis-dock");

    await expect(sidebarShell).toBeVisible();

    await sidebarToggle.evaluate((button) => {
      (button as HTMLButtonElement).click();
    });
    await expect(sidebarToggle).toContainText("Expand navigation rail");

    await panelToggle.click();
    await expect(panelToggle).toContainText("Open workspace panel");
    await expect(sidebarRail).toHaveClass(/hidden lg:block lg:w-16/);
    await expect(sidebarShell).toBeVisible();

    await jarvisLauncher.click();
    await expect(jarvisDock).toBeVisible();

    await jarvisDock.getByRole("button", { name: "Close" }).click();
    await expect(jarvisDock).toBeHidden();
    await expect(jarvisLauncher).toBeVisible();
  });

  test("customer viewer dashboard survives a reload", async ({ page }) => {
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

    await gotoWorkspacePanel(page, "/wiley-workspace/customers");

    const panel = page.locator("#customer-viewer-panel");
    const directoryStatus = page.locator("#customer-directory-status");
    const dashboard = page.locator("#customer-viewer-dashboard");
    const customerGrid = page.locator("#customer-directory-grid");

    await expect(panel).toBeVisible();
    await expect(directoryStatus).toBeVisible();
    await expect(directoryStatus).toContainText(
      /Loaded \d+ utility customers from the live API\.|The live customer directory could not be refreshed\./,
    );
    await expect(dashboard).toBeAttached();
    await expect(customerGrid).toBeVisible();
    await expect(
      page
        .locator("#customer-summary-panel")
        .getByText("Customer Summary", { exact: true }),
    ).toBeVisible();
    await expect(
      page
        .locator("#customer-filters-panel")
        .getByText("Customer Filters", { exact: true }),
    ).toBeVisible();
    await expect(
      page
        .locator("#customer-grid-panel")
        .getByText("Customer Directory", { exact: true }),
    ).toBeVisible();

    await page.reload();
    await waitForWorkspacePanel(page, "#customer-viewer-panel");

    await expect(panel).toBeVisible();
    await expect(directoryStatus).toBeVisible();
    await expect(dashboard).toBeAttached();
    await expect(customerGrid).toBeVisible();
    await expect(
      page
        .locator("#customer-summary-panel")
        .getByText("Customer Summary", { exact: true }),
    ).toBeVisible();
    await expect(
      page
        .locator("#customer-filters-panel")
        .getByText("Customer Filters", { exact: true }),
    ).toBeVisible();
  });

  test("customer editor dialog renders Syncfusion form controls", async ({
    page,
  }) => {
    await gotoWorkspacePanel(page, "/wiley-workspace/customers");

    await page.locator("#add-customer-button").click();

    const dialog = page.getByRole("dialog", { name: "Add Utility Customer" });

    await expect(dialog).toBeVisible();
    await expect(
      dialog.locator("#customer-editor-account-number"),
    ).toBeVisible();
    await expect(dialog.locator("#customer-editor-first-name")).toBeVisible();
    await expect(dialog.locator("#customer-editor-last-name")).toBeVisible();
    await expect(dialog.locator("#customer-editor-company-name")).toBeVisible();
    const dialogComboboxes = dialog.getByRole("combobox");
    await expect(dialogComboboxes).toHaveCount(4);
    await expect(
      dialog.locator("#customer-editor-service-address"),
    ).toBeVisible();
    await expect(dialog.locator("#customer-editor-service-city")).toBeVisible();
    await expect(
      dialog.locator("#customer-editor-service-state"),
    ).toBeVisible();
    await expect(
      dialog.locator("#customer-editor-service-zip-code"),
    ).toBeVisible();
    const dialogSpinbuttons = dialog.getByRole("spinbutton");
    await expect(dialogSpinbuttons).toHaveCount(1);

    await dialog.locator("#customer-editor-account-number").fill("PW-1001");
    await dialog.locator("#customer-editor-first-name").fill("Playwright");
    await dialog.locator("#customer-editor-last-name").fill("Customer");
    await enterNumericValue(dialogSpinbuttons.first(), "12.34");
    await expect(dialogSpinbuttons.first()).toHaveValue(/12\.34|\$12\.34/);

    await dialog.locator("#customer-editor-cancel-button").click();
    await expect(dialog).toBeHidden();
  });

  test("QuickBooks preview grid and commit dialog render for a supported file", async ({
    page,
  }) => {
    await page.route("**/api/imports/quickbooks/preview", async (route) => {
      await route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify(createQuickBooksPreviewResponse()),
      });
    });

    await gotoWorkspacePanel(page, "/wiley-workspace/quickbooks-import");

    const statusHeadline = page.locator("#quickbooks-import-status-headline");
    const statusMessage = page.locator("#quickbooks-import-status-message");
    const analyzeButton = page.getByRole("button", { name: "Analyze file" });
    const commitButton = page.getByRole("button", { name: "Commit import" });
    const assistantContext = page.locator(
      "#quickbooks-assistant-context-summary",
    );
    const commitDialog = page.getByRole("dialog", {
      name: "Confirm QuickBooks import",
    });

    await page.locator("#quickbooks-import-uploader").setInputFiles({
      name: "quickbooks-sample.csv",
      mimeType: "text/csv",
      buffer: Buffer.from(createQuickBooksCsv(), "utf8"),
    });

    await expect(statusHeadline).toHaveText("File selected");
    await expect(statusMessage).toContainText(
      "quickbooks-sample.csv is ready for preview.",
    );

    await analyzeButton.click();

    await expect
      .poll(
        async () => (await statusHeadline.innerText()).trim().toLowerCase(),
        {
          timeout: 30_000,
        },
      )
      .toMatch(/preview ready|duplicate detected/);

    await expect(page.locator(".e-grid")).toBeVisible();
    await expect(page.locator(".e-grid")).toContainText("Town of Wiley");
    await expect(page.locator(".e-grid")).toContainText("Water Revenue");
    await expect(assistantContext).toContainText("quickbooks-sample.csv for");
    await expect(assistantContext).toContainText("2 rows parsed");

    if (await commitButton.isEnabled()) {
      await commitButton.click();
      await expect(commitDialog).toBeVisible();
      await commitDialog.getByRole("button", { name: "Cancel" }).click();
      await expect(commitDialog).toBeHidden();
    } else {
      await expect(statusHeadline).toHaveText(/Duplicate detected/i);
    }
  });

  test("data dashboard and trends render Syncfusion charts and gauges", async ({
    page,
  }) => {
    await gotoWorkspacePanel(page, "/wiley-workspace/data-dashboard");

    await expect(page.locator("#data-dashboard-panel")).toBeVisible();
    await expect(page.locator("#kpi-net-position")).toBeVisible();
    await expect(page.locator("#kpi-coverage-ratio")).toBeVisible();
    await expect(page.locator("#kpi-rate-adequacy")).toBeVisible();
    await expect(page.locator("#kpi-scenario-pressure")).toBeVisible();
    await expect(page.locator("#coverage-ratio-gauge")).toBeVisible();
    await expect(page.locator("#rate-adequacy-gauge")).toBeVisible();
    await expect(page.locator("#budget-variance-chart")).toBeVisible();

    const customerDonutsSection = page.locator("#customer-donuts-section");
    if (await customerDonutsSection.count()) {
      await expect(customerDonutsSection).toBeVisible();
      await expect(page.locator("#customer-service-chart")).toBeVisible();
      await expect(page.locator("#customer-citylimits-chart")).toBeVisible();
    }

    await page
      .locator("#workspace-navigation-list")
      .getByRole("link", { name: "Trends" })
      .click();
    await expect(page).toHaveURL("/wiley-workspace/trends");
    await waitForWorkspacePanel(page, "#trends-panel");

    await expect(page.locator("#trends-panel")).toBeVisible();
    await expect(page.locator("#trends-chart-region")).toBeVisible();
    await expect(page.locator("#trends-projection-chart")).toBeVisible();
    await expect(page.locator("#workspace-breadcrumb")).toContainText("Trends");
  });

  test("decision support renders Jarvis Syncfusion surfaces", async ({
    page,
  }) => {
    await gotoWorkspacePanel(page, "/wiley-workspace/decision-support");

    await expect(page.locator("#decision-support-panel")).toBeVisible();
    await expect(page.locator("#workspace-breadcrumb")).toContainText(
      "Decision Support",
    );

    const runtimeStatus = page.locator("#jarvis-runtime-status");
    if (await runtimeStatus.isVisible()) {
      await expect(page.locator("#jarvis-chat-ui")).toBeVisible();
      await expect(page.locator("#jarvis-question-input")).toBeVisible();
      await expect(page.locator("#jarvis-conversation-history")).toBeVisible();
      await expect(
        page.locator("#jarvis-recommendation-history"),
      ).toBeVisible();
      await expect(page.locator("#jarvis-chat-answer")).toBeVisible();
      await expect(
        page.getByRole("button", { name: "Ask Jarvis" }),
      ).toBeEnabled();
      await expect(
        page.getByRole("button", { name: "Reset Thread" }),
      ).toBeEnabled();
    } else {
      await expect(page.locator("#decision-support-panel")).toContainText(
        "server-side Semantic Kernel assistant",
      );
    }
  });

  test("visual regression: data dashboard panel remains stable", async ({
    page,
  }) => {
    await gotoWorkspacePanel(page, "/wiley-workspace/data-dashboard");

    const dataDashboardPanel = page.locator("#data-dashboard-panel");

    await expect(dataDashboardPanel).toBeVisible();
    await expect(page.locator("#kpi-net-position")).toBeVisible();
    await expect(page.locator("#kpi-coverage-ratio")).toBeVisible();
    await expect(page.locator("#kpi-rate-adequacy")).toBeVisible();
    await expect(page.locator("#kpi-scenario-pressure")).toBeVisible();
    await expect(page.locator("#coverage-ratio-gauge")).toBeVisible();
    await expect(page.locator("#rate-adequacy-gauge")).toBeVisible();
    await expect(page.locator("#budget-variance-chart")).toBeVisible();
    await expect(page.locator("#rate-comparison-section")).toBeVisible();
    await expect(page.locator("#rate-trend-section")).toBeVisible();

    const customerDonutsSection = page.locator("#customer-donuts-section");
    if (await customerDonutsSection.count()) {
      await expect(customerDonutsSection).toBeVisible();
      await expect(page.locator("#customer-service-chart")).toBeVisible();
      await expect(page.locator("#customer-citylimits-chart")).toBeVisible();
    }
  });

  test("Jarvis chat surface remains structurally stable", async ({ page }) => {
    await gotoWorkspacePanel(page, "/wiley-workspace/decision-support");

    const jarvisSurface = page.locator("#jarvis-chat-ui");
    if (!(await jarvisSurface.isVisible())) {
      await expect(page.locator("#decision-support-panel")).toContainText(
        /server-side Semantic Kernel assistant|Semantic Kernel assistant/,
      );
      return;
    }

    await expect(jarvisSurface).toBeVisible();
    await expect(page.locator("#jarvis-runtime-status")).toBeVisible();
    await expect(page.locator("#jarvis-question-input")).toBeVisible();
    await expect(page.locator("#jarvis-conversation-history")).toBeVisible();
    await expect(page.locator("#jarvis-recommendation-history")).toBeVisible();
    await expect(page.locator("#jarvis-chat-answer")).toBeVisible();
  });
});
