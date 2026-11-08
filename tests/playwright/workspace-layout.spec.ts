import { test } from "@playwright/test";
import { expect } from "./support/layout-expect";
import { expectVerticalStack } from "./support/layout-rules";
import { expandDocumentCenter, waitForWorkspaceShell } from "./support/workspace";

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
    const contextRail = page.locator("#workspace-sidebar-shell");
    const mainContent = page.locator("#workspace-main-content");

    await expect(hero).toBeInside(workspaceRoot, { padding: 0 });
    await expect(documentCenter).toBeInside(workspaceRoot, { padding: 0 });
    await expect(mainContent).toBeInside(workspaceRoot, { padding: 0 });
    await expectVerticalStack([hero, documentCenter]);
    await expect(contextRail).toBeLeftOf(mainContent, { gap: [0, 40] });
  });

  test("document center export buttons stay inside the deliverables toolbar", async ({
    page,
  }) => {
    await page.setViewportSize({ width: 1366, height: 900 });
    await page.goto("/wiley-workspace");
    await waitForWorkspaceShell(page);
    await expandDocumentCenter(page);

    const documentCenter = page.locator("#workspace-document-center");
    const customerExport = page.locator("#export-customers-excel-button");
    const scenarioExport = page.locator("#export-scenario-excel-button");
    const pdfExport = page.locator("#export-workspace-pdf-button");

    await expect(customerExport).toBeInside(documentCenter, { padding: 0 });
    await expect(scenarioExport).toBeInside(documentCenter, { padding: 0 });
    await expect(pdfExport).toBeInside(documentCenter, { padding: 0 });
    await expect(customerExport).toBeVisible();
    await expect(pdfExport).toBeVisible();
    await expect(scenarioExport).toBeVisible();
  });

  test("scenario panel keeps overview and grid workbench in a readable vertical stack", async ({
    page,
  }) => {
    await page.setViewportSize({ width: 1366, height: 900 });
    await page.goto("/wiley-workspace/scenario");
    await waitForWorkspaceShell(page);

    const scenarioPanel = page.locator("#scenario-panel");
    const overview = page.locator("#scenario-overview-section");
    const gridWorkbench = page.locator("#scenario-grid-workbench-card");

    await expect(overview).toBeInside(scenarioPanel, { padding: 0 });
    await expect(gridWorkbench).toBeInside(scenarioPanel, { padding: 0 });
    await expectVerticalStack([overview, gridWorkbench]);
  });
});
