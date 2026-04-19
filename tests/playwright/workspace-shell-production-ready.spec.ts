import { expect, test } from "@playwright/test";
import { waitForWorkspaceShell } from "./support/workspace";

test.describe("Workspace Shell", () => {
  test("Workspace shell and navigation stay usable on a fresh load", async ({
    page,
  }) => {
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
    const primaryNavigation = page.locator("#workspace-navigation-card");

    await primaryNavigation.getByRole("link", { name: "Break-Even" }).click();
    await expect(page.locator("#break-even-panel")).toBeVisible();
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
    await expect(page.locator("#workspace-breadcrumb")).toContainText(
      "Scenario Planner",
    );

    await primaryNavigation
      .getByRole("link", { name: "Customer Viewer" })
      .click();
    await expect(page.locator("#customer-viewer-panel")).toBeVisible();
    await expect(page.locator("#workspace-breadcrumb")).toContainText(
      "Customer Viewer",
    );

    await primaryNavigation.getByRole("link", { name: "Trends" }).click();
    await expect(page.locator("#trends-panel")).toBeVisible();
    await expect(page.locator("#workspace-breadcrumb")).toContainText("Trends");

    await primaryNavigation
      .getByRole("link", { name: "Decision Support" })
      .click();
    await expect(page.locator("#decision-support-panel")).toBeVisible();
    await expect(page.locator("#workspace-breadcrumb")).toContainText(
      "Decision Support",
    );

    await primaryNavigation
      .getByRole("link", { name: "Data Dashboard" })
      .click();
    await expect(page.locator("#data-dashboard-panel")).toBeVisible();
    await expect(page.locator("#workspace-breadcrumb")).toContainText(
      "Data Dashboard",
    );

    await primaryNavigation
      .getByRole("link", { name: "Overview", exact: true })
      .click();
    await expect(page.locator("#workspace-overview-dashboard")).toBeVisible();

    // 3. Resize to a narrower desktop width and confirm the shell still fits the viewport.
    await page.setViewportSize({ width: 1280, height: 900 });

    await expect(
      page.getByRole("navigation", { name: "Primary" }),
    ).toBeVisible();
    await expect(page.locator("#workspace-status-card")).toBeVisible();
    await expect(page.locator("#workspace-overview-dashboard")).toBeVisible();
  });
});
