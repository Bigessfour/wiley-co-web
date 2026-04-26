import { expect, test } from "@playwright/test";
import {
  seedLeftNavCollapsed,
  waitForWorkspaceShell,
} from "./support/workspace";

test.describe("Workspace Shell", () => {
  test("Workspace shell and navigation stay usable on a fresh load", async ({
    page,
  }) => {
    test.setTimeout(150000);

    // 1. Open /wiley-workspace in a fresh browser session.
    await page.goto("/wiley-workspace");

    await waitForWorkspaceShell(page);
    await expect(page.locator("#workspace-overview-dashboard")).toBeVisible();
    await expect(page.locator("#workspace-status-card")).toContainText(
      "Startup source:",
    );
    await expect(page.locator("#workspace-status-card")).toContainText(
      "Current state:",
    );
    await expect(page.locator("#workspace-status-card")).not.toContainText(
      /pending/i,
    );

    // 2. Use the left navigation to visit every panel route once, then return to the overview.
    const primaryNavigation = page.locator("#workspace-navigation-list");

    await primaryNavigation.getByRole("link", { name: "Break-Even" }).click();
    await expect(page.locator("#break-even-panel")).toBeVisible();
    await expect(page.locator("#break-even-quadrant-panel")).toBeVisible();
    await expect(page.locator("#apartment-config-panel")).toBeVisible();
    await expect(page.locator("#break-even-panel")).toContainText(
      "Break-Even Quadrants",
    );
    await expect(page.locator("#workspace-breadcrumb")).toContainText(
      "Break-Even",
    );

    await primaryNavigation.getByRole("link", { name: "Rates" }).click();
    await expect(page.locator("#rates-panel")).toBeVisible();
    await expect(page.locator("#workspace-breadcrumb")).toContainText("Rates");

    await primaryNavigation
      .getByRole("link", { name: "QuickBooks Import" })
      .click();
    await expect(
      page.locator("#quickbooks-import-status-headline"),
    ).toBeVisible();
    await expect(page.locator("#workspace-breadcrumb")).toContainText(
      "QuickBooks Import",
    );

    await primaryNavigation
      .getByRole("link", { name: "Scenario Planner" })
      .click();
    await expect(page.locator("#scenario-panel")).toBeVisible();
    await expect(page.locator("#scenario-grid")).toBeVisible();
    await expect(page.locator("#scenario-edit-status")).toBeVisible();
    await expect(page.locator("#workspace-breadcrumb")).toContainText(
      "Scenario Planner",
    );

    await primaryNavigation
      .getByRole("link", { name: "Customer Viewer" })
      .click();
    await expect(page.locator("#customer-viewer-panel")).toBeVisible();
    await expect(page.locator("#customer-viewer-dashboard")).toBeVisible();
    await expect(page.locator("#customer-directory-grid")).toBeVisible();
    await expect(page.locator("#workspace-breadcrumb")).toContainText(
      "Customer Viewer",
    );

    await primaryNavigation.getByRole("link", { name: "Trends" }).click();
    await expect(page.locator("#trends-panel")).toBeVisible();
    await expect(page.locator("#workspace-breadcrumb")).toContainText("Trends");

    await primaryNavigation
      .getByRole("link", { name: "Decision Support" })
      .click();
    await expect(page.locator("#decision-support-panel")).toBeAttached();
    await expect(page.locator("#workspace-breadcrumb")).toContainText(
      "Decision Support",
    );

    await primaryNavigation
      .getByRole("link", { name: "Data Dashboard" })
      .click();
    await expect(page.locator("#data-dashboard-panel")).toBeAttached();
    await expect(page.locator("#workspace-breadcrumb")).toContainText(
      "Data Dashboard",
    );

    // 3. Resize to a narrower desktop width and confirm the shell still fits the viewport.
    await page.setViewportSize({ width: 1280, height: 900 });

    await expect(page.locator("#workspace-navigation-list")).toBeVisible();
    await expect(page.locator("#workspace-status-card")).toBeVisible();

    await page.goto("/wiley-workspace");

    await waitForWorkspaceShell(page);
    await expect(page.locator("#workspace-overview-dashboard")).toBeVisible();
  });

  test("Workspace dashboard stays visible just below the Syncfusion media-query breakpoint", async ({
    page,
  }) => {
    await seedLeftNavCollapsed(page, false);
    await page.goto("/wiley-workspace");

    await waitForWorkspaceShell(page);
    await page.setViewportSize({ width: 1279, height: 900 });

    const sidebarToggle = page.locator("#app-shell-nav-toggle");

    await expect(page.locator("#workspace-dashboard")).toBeVisible();
    await expect(page.locator("#workspace-document-center")).toBeVisible();
    await expect(page.locator("#workspace-overview-dashboard")).toBeVisible();
    await expect(page.locator("#workspace-status-card")).toBeVisible();
    await expect(sidebarToggle).toBeVisible();

    await sidebarToggle.evaluate((button) => {
      (button as HTMLButtonElement).click();
    });
    await expect(sidebarToggle).toContainText("Expand navigation rail");
    await expect(page.locator("#workspace-navigation-list")).toBeVisible();
    await expect(
      page
        .locator("#workspace-navigation-list")
        .getByRole("link", { name: "Rates" }),
    ).toBeVisible();
    await expect(page.locator("#workspace-overview-dashboard")).toBeVisible();

    await page
      .locator("#workspace-navigation-list")
      .getByRole("link", { name: "Rates" })
      .click();
    await expect(sidebarToggle).toContainText("Expand navigation rail");
    await expect(page.locator("#rates-panel")).toBeVisible();
  });

  test("Collapsing the left rail stays consistent across key desktop routes", async ({
    page,
  }) => {
    test.setTimeout(180000);
    await seedLeftNavCollapsed(page, false);

    const routeChecks = [
      {
        route: "/wiley-workspace/break-even",
        visibleSelector: "#break-even-panel",
      },
      { route: "/wiley-workspace/rates", visibleSelector: "#rates-panel" },
      {
        route: "/wiley-workspace/scenario",
        visibleSelector: "#scenario-panel",
      },
      {
        route: "/wiley-workspace/customers",
        visibleSelector: "#customer-viewer-panel",
      },
      {
        route: "/wiley-workspace/data-dashboard",
        visibleSelector: "#data-dashboard-panel",
      },
    ];

    await page.setViewportSize({ width: 1366, height: 900 });

    for (const routeCheck of routeChecks) {
      await page.goto(routeCheck.route);
      await waitForWorkspaceShell(page, 30_000);

      const sidebarToggle = page.locator("#app-shell-nav-toggle");
      await expect(page.locator(routeCheck.visibleSelector)).toBeVisible();
      await expect(sidebarToggle).toContainText("Collapse navigation rail");
      await sidebarToggle.evaluate((button) => {
        (button as HTMLButtonElement).click();
      });
      await expect(sidebarToggle).toContainText("Expand navigation rail");
      await expect(page.locator("#workspace-navigation-list")).toBeVisible();
      await expect(
        page
          .locator("#workspace-navigation-list")
          .getByRole("link", { name: "Rates" }),
      ).toBeVisible();
      await expect
        .poll(() =>
          page.evaluate(() => {
            const storedState = window.localStorage.getItem(
              "wiley.workspace.layout.v2",
            );

            return storedState
              ? JSON.parse(storedState).LeftNavCollapsed
              : null;
          }),
        )
        .toBe(true);
      await sidebarToggle.evaluate((button) => {
        (button as HTMLButtonElement).click();
      });
      await expect(sidebarToggle).toContainText("Collapse navigation rail");
    }
  });
});
