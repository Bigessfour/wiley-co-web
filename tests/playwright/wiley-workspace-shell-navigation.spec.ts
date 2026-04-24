import { expect, test } from "@playwright/test";
import { gotoWorkspacePanel, waitForWorkspaceShell } from "./support/workspace";

test.describe("Shell Navigation Proof", () => {
  test("workspace shell exposes the planned enterprise routes", async ({
    page,
  }) => {
    await gotoWorkspacePanel(page, "/wiley-workspace");

    const navigation = page.locator("#workspace-navigation-list");

    await expect(navigation).toBeVisible();
    await expect(
      navigation.getByRole("link", { name: "Break-Even" }),
    ).toBeVisible();
    await expect(
      navigation.getByRole("link", { name: "QuickBooks Import" }),
    ).toBeVisible();
    await expect(
      navigation.getByRole("link", { name: "Scenario Planner" }),
    ).toBeVisible();
    await expect(
      navigation.getByRole("link", { name: "Customer Viewer" }),
    ).toBeVisible();
    await expect(
      navigation.getByRole("link", { name: "Trends" }),
    ).toBeVisible();
    await expect(
      navigation.getByRole("link", { name: "Capital Gap" }),
    ).toBeVisible();
    await expect(
      navigation.getByRole("link", { name: "Decision Support" }),
    ).toBeVisible();
    await expect(
      navigation.getByRole("link", { name: "Data Dashboard" }),
    ).toBeVisible();

    await navigation.getByRole("link", { name: "Break-Even" }).click();
    await expect(page.locator("#break-even-panel")).toBeVisible();
    await expect(page.locator("#workspace-breadcrumb")).toContainText(
      "Break-Even",
    );

    await expect(
      navigation.getByRole("link", { name: "QuickBooks Import" }),
    ).toHaveAttribute("href", "/wiley-workspace/quickbooks-import");
    await page.goto("/wiley-workspace/quickbooks-import");
    await expect(page).toHaveURL(/\/wiley-workspace\/quickbooks-import$/);
    await expect(
      page.locator("#quickbooks-import-status-headline"),
    ).toBeVisible();

    await expect(
      navigation.getByRole("link", { name: "Scenario Planner" }),
    ).toHaveAttribute("href", "/wiley-workspace/scenario");

    await expect(
      navigation.getByRole("link", { name: "Customer Viewer" }),
    ).toHaveAttribute("href", "/wiley-workspace/customers");

    await expect(
      navigation.getByRole("link", { name: "Trends" }),
    ).toHaveAttribute("href", "/wiley-workspace/trends");

    await expect(
      navigation.getByRole("link", { name: "Capital Gap" }),
    ).toHaveAttribute("href", "/wiley-workspace/capital-gap");

    await expect(
      navigation.getByRole("link", { name: "Decision Support" }),
    ).toHaveAttribute("href", "/wiley-workspace/decision-support");

    await expect(
      navigation.getByRole("link", { name: "Data Dashboard" }),
    ).toHaveAttribute("href", "/wiley-workspace/data-dashboard");

    await page.goto("/wiley-workspace");
    await waitForWorkspaceShell(page);
    await expect(page.locator("#workspace-overview-dashboard")).toBeVisible();
  });
});
