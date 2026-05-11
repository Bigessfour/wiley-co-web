import { test } from "@playwright/test";
import { expect } from "./support/layout-expect";
import {
  expectHorizontalButtonGroup,
  expectVerticalStack,
} from "./support/layout-rules";
import { waitForWorkspaceShell } from "./support/workspace";

test.describe("Workspace box-method layout", () => {
  test("workspace shell sections keep their desktop box relationships", async ({
    page,
  }) => {
    await page.setViewportSize({ width: 1366, height: 900 });
    await page.goto("/wiley-workspace");
    await waitForWorkspaceShell(page);

    const workspaceRoot = page.locator(".wiley-workspace").first();
    const hero = page.locator("#workspace-overview-hero");
    const documentCenter = page.locator("#workspace-document-center");
    const sidebar = page.locator(".workspace-sidebar");
    const mainContent = page.locator("#workspace-main-content");

    await expect(hero).toBeInside(workspaceRoot, { padding: 0 });
    await expect(documentCenter).toBeInside(workspaceRoot, { padding: 0 });
    await expect(mainContent).toBeInside(workspaceRoot, { padding: 0 });
    await expectVerticalStack([hero, documentCenter]);
    await expect(sidebar).toBeLeftOf(mainContent, { gap: [12, 28] });
  });

  test("document center export buttons align as equal-width boxes", async ({
    page,
  }) => {
    await page.setViewportSize({ width: 1366, height: 900 });
    await page.goto("/wiley-workspace");
    await waitForWorkspaceShell(page);

    const documentCenter = page.locator("#workspace-document-center");
    const customerExport = page.locator("#export-customers-excel-button");
    const scenarioExport = page.locator("#export-scenario-excel-button");
    const pdfExport = page.locator("#export-workspace-pdf-button");

    await expect(customerExport).toBeInside(documentCenter, { padding: 0 });
    await expect(scenarioExport).toBeInside(documentCenter, { padding: 0 });
    await expect(pdfExport).toBeInside(documentCenter, { padding: 0 });
    await expectHorizontalButtonGroup([
      customerExport,
      scenarioExport,
      pdfExport,
    ]);
  });

  test("scenario panel keeps summary, metrics, and grid in a readable vertical stack", async ({
    page,
  }) => {
    await page.setViewportSize({ width: 1366, height: 900 });
    await page.goto("/wiley-workspace/scenario");
    await waitForWorkspaceShell(page);

    const scenarioPanel = page.locator("#scenario-panel");
    const summary = page.locator("#scenario-summary-panel");
    const metrics = page.locator("#scenario-metrics-panel");
    const grid = page.locator("#scenario-grid-panel");

    await expect(summary).toBeInside(scenarioPanel, { padding: 0 });
    await expect(metrics).toBeInside(scenarioPanel, { padding: 0 });
    await expect(grid).toBeInside(scenarioPanel, { padding: 0 });
    await expectVerticalStack([summary, metrics, grid]);
  });
});
