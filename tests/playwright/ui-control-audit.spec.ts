/**
 * Automated UI control audit — visits each workspace panel, exercises primary
 * controls, and writes a JSON report to TestResults/ui-control-audit.json.
 *
 * Run: npm run playwright:test -- tests/playwright/ui-control-audit.spec.ts
 */
import { writeFileSync, mkdirSync } from "node:fs";
import { dirname, join } from "node:path";
import { expect, test } from "@playwright/test";
import { gotoWorkspacePanel, waitForWorkspaceShell } from "./support/workspace";

type ControlResult = {
  panel: string;
  route: string;
  control: string;
  status: "pass" | "fail" | "skip";
  detail?: string;
};

const report: ControlResult[] = [];

function record(
  panel: string,
  route: string,
  control: string,
  status: ControlResult["status"],
  detail?: string,
) {
  report.push({ panel, route, control, status, detail });
}

const PANEL_ROUTES: Array<{
  panel: string;
  route: string;
  panelSelector: string;
}> = [
  {
    panel: "overview",
    route: "/wiley-workspace",
    panelSelector: "#workspace-overview-dashboard",
  },
  {
    panel: "break-even",
    route: "/wiley-workspace/break-even",
    panelSelector: "#break-even-panel",
  },
  {
    panel: "apartment-config",
    route: "/wiley-workspace/apartment-config",
    panelSelector: "#apartment-config-panel",
  },
  {
    panel: "rates",
    route: "/wiley-workspace/rates",
    panelSelector: "#rates-panel",
  },
  {
    panel: "quickbooks-import",
    route: "/wiley-workspace/quickbooks-import",
    panelSelector: "#quickbooks-import-panel",
  },
  {
    panel: "scenario",
    route: "/wiley-workspace/scenario",
    panelSelector: "#scenario-panel",
  },
  {
    panel: "customers",
    route: "/wiley-workspace/customers",
    panelSelector: "#customer-viewer-panel",
  },
  {
    panel: "affordability",
    route: "/wiley-workspace/affordability",
    panelSelector: "#affordability-dashboard-panel",
  },
  {
    panel: "debt-coverage",
    route: "/wiley-workspace/debt-coverage",
    panelSelector: "#debt-coverage-panel",
  },
  {
    panel: "capital-gap",
    route: "/wiley-workspace/capital-gap",
    panelSelector: "#capital-gap-panel",
  },
  {
    panel: "reserve-trajectory",
    route: "/wiley-workspace/reserve-trajectory",
    panelSelector: "#reserve-trajectory-panel",
  },
  {
    panel: "trends",
    route: "/wiley-workspace/trends",
    panelSelector: "#trends-panel",
  },
  {
    panel: "decision-support",
    route: "/wiley-workspace/decision-support",
    panelSelector: "#decision-support-panel",
  },
  {
    panel: "data-dashboard",
    route: "/wiley-workspace/data-dashboard",
    panelSelector: "#data-dashboard-panel",
  },
];

test.describe("UI control audit", () => {
  test.describe.configure({ mode: "serial" });

  test.afterAll(() => {
    const outDir = join(process.cwd(), "TestResults");
    mkdirSync(outDir, { recursive: true });
    const outPath = join(outDir, "ui-control-audit.json");
    writeFileSync(
      outPath,
      JSON.stringify(
        { generatedAt: new Date().toISOString(), results: report },
        null,
        2,
      ),
    );
    console.log(`UI control audit written to ${outPath}`);
  });

  test("left nav navigates to Rates", async ({ page }) => {
    await page.goto("/wiley-workspace");
    await waitForWorkspaceShell(page);
    await page
      .getByRole("navigation", { name: "Primary workspace navigation" })
      .getByRole("button", { name: "Rates" })
      .click();
    await expect(page).toHaveURL(/\/wiley-workspace\/rates$/);
    record("nav", "/wiley-workspace/rates", "left-nav Rates", "pass");
  });

  test("overview Open Rates navigates", async ({ page }) => {
    await page.goto("/wiley-workspace");
    await waitForWorkspaceShell(page);
    await page.getByRole("button", { name: "Open Rates" }).click();
    await expect(page).toHaveURL(/\/wiley-workspace\/rates$/);
    record("overview", "/wiley-workspace/rates", "Open Rates card", "pass");
  });

  for (const { panel, route, panelSelector } of PANEL_ROUTES) {
    test(`panel ${panel} loads and primary shell visible`, async ({ page }) => {
      if (route === "/wiley-workspace") {
        await page.goto(route);
        await waitForWorkspaceShell(page);
      } else {
        await gotoWorkspacePanel(page, route);
      }

      const shell = page.locator(panelSelector);
      if ((await shell.count()) === 0) {
        record(panel, route, panelSelector, "fail", "Panel root not found");
        expect.soft(shell).toBeVisible();
        return;
      }

      const target = shell.first();
      await target.scrollIntoViewIfNeeded();
      await expect(target).toBeVisible({ timeout: 30_000 });
      record(panel, route, panelSelector, "pass", "visible");
    });
  }

  test("context rail navigation Break-Even button", async ({ page }) => {
    await page.goto("/wiley-workspace");
    await waitForWorkspaceShell(page);
    await page
      .locator("#workspace-navigation-card")
      .getByRole("button", { name: "Break-Even" })
      .click();
    await expect(page).toHaveURL(/\/wiley-workspace\/break-even$/);
    record(
      "nav",
      "/wiley-workspace/break-even",
      "context-rail Break-Even",
      "pass",
    );
  });
});
