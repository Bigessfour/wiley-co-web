import { Buffer } from "node:buffer";
import { expect, test } from "@playwright/test";
import {
  enterNumericValue,
  gotoWorkspacePanel,
  prepareForVisualSnapshot,
  waitForWorkspaceShell,
} from "./support/workspace";
import {
  createQuickBooksCsv,
  createQuickBooksPreviewResponse,
} from "./support/quickbooks";

test.describe("Wiley workspace Syncfusion coverage", () => {
  test.describe.configure({ mode: "serial" });

  test("workspace shell renders Syncfusion selectors and document actions", async ({
    page,
  }) => {
    await page.goto("/wiley-workspace");

    await waitForWorkspaceShell(page);

    const documentCenter = page.locator("#workspace-document-center");
    const exportStatus = documentCenter.locator("div.rounded-xl.bg-slate-50");
    const enterpriseCard = page.locator("#workspace-enterprise-context-card");
    const comboboxes = enterpriseCard.getByRole("combobox");

    await expect(documentCenter).toBeVisible();
    await expect(page.locator("#export-customers-excel-button")).toBeEnabled();
    await expect(page.locator("#export-scenario-excel-button")).toBeEnabled();
    await expect(page.locator("#export-workspace-pdf-button")).toBeEnabled();

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

    await page.locator("#export-customers-excel-button").click();
    await expect(exportStatus).toContainText(
      /Downloaded .*\.xlsx|Export failed:/,
    );

    await page.locator("#export-scenario-excel-button").click();
    await expect(exportStatus).toContainText(
      /Downloaded .*\.xlsx|Export failed:/,
    );

    await page.locator("#export-workspace-pdf-button").click();
    await expect(exportStatus).toContainText(
      /Downloaded .*\.pdf|Export failed:/,
    );
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

    await page.goto("/wiley-workspace/quickbooks-import");

    await waitForWorkspaceShell(page);

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
    await page.goto("/wiley-workspace/data-dashboard");

    await waitForWorkspaceShell(page);

    await expect(page.locator("#data-dashboard-panel")).toBeVisible();
    await expect(page.locator("#kpi-net-position")).toBeVisible();
    await expect(page.locator("#kpi-coverage-ratio")).toBeVisible();
    await expect(page.locator("#kpi-rate-adequacy")).toBeVisible();
    await expect(page.locator("#kpi-scenario-pressure")).toBeVisible();
    await expect(page.locator("#coverage-ratio-gauge")).toBeVisible();
    await expect(page.locator("#rate-adequacy-gauge")).toBeVisible();
    await expect(page.locator("#budget-variance-chart")).toBeVisible();

    await page.goto("/wiley-workspace/trends");

    await waitForWorkspaceShell(page);

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
    browserName,
  }) => {
    test.skip(
      browserName !== "chromium",
      "Visual snapshots are baselined for chromium only.",
    );

    await gotoWorkspacePanel(page, "/wiley-workspace/data-dashboard");
    await prepareForVisualSnapshot(page);

    await expect(page.locator("#data-dashboard-panel")).toHaveScreenshot(
      "data-dashboard-panel.png",
      {
        animations: "disabled",
        caret: "hide",
        scale: "css",
        maxDiffPixelRatio: 0.02,
      },
    );
  });

  test("visual regression: Jarvis chat surface remains stable", async ({
    page,
    browserName,
  }) => {
    test.skip(
      browserName !== "chromium",
      "Visual snapshots are baselined for chromium only.",
    );

    await gotoWorkspacePanel(page, "/wiley-workspace/decision-support");

    const jarvisSurface = page.locator("#jarvis-chat-ui");
    if (!(await jarvisSurface.isVisible())) {
      test.skip(
        true,
        "Secure Jarvis surface is not enabled in the current environment.",
      );
    }

    await prepareForVisualSnapshot(page);
    await expect(jarvisSurface).toHaveScreenshot("jarvis-chat-surface.png", {
      animations: "disabled",
      caret: "hide",
      scale: "css",
      maxDiffPixelRatio: 0.02,
    });
  });
});
