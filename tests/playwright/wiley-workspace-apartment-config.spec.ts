import { expect, test } from "@playwright/test";

test.describe("Apartment Config Proof", () => {
  test("apartment configuration shows the seeded mix and roll-up totals", async ({
    page,
  }) => {
    await page.goto("/wiley-workspace");

    const navigation = page.locator("#workspace-navigation-list");
    const apartmentPanel = page.locator("#apartment-config-panel");

    await expect(
      navigation.getByRole("link", { name: "Break-Even" }),
    ).toBeVisible();
    await navigation.getByRole("link", { name: "Break-Even" }).click();
    await expect(apartmentPanel).toBeVisible();
    await expect(apartmentPanel).toContainText("Apartment Configuration");
    await expect(apartmentPanel).toContainText("2 Bedroom");
    await expect(apartmentPanel).toContainText("3 Bedroom");
    await expect(apartmentPanel).toContainText(/Total Units\s*16/);
    await expect(apartmentPanel).toContainText(/Monthly Revenue\s*\$8,000/);
    await expect(apartmentPanel).toContainText(
      /Effective \$\/Customer\s*\$200\.00/,
    );

    await expect(
      apartmentPanel.getByRole("button", { name: "Add" }),
    ).toBeVisible();
    await expect(
      apartmentPanel.getByRole("button", { name: "Edit" }),
    ).toBeVisible();
    await expect(
      apartmentPanel.getByRole("button", { name: "Delete" }),
    ).toBeVisible();

    const grid = apartmentPanel.locator(".e-grid");

    await expect(grid).toBeVisible();
    await expect(grid).toContainText("Unit Type");
    await expect(grid).toContainText("Bedrooms");
    await expect(grid).toContainText("Units");
    await expect(grid).toContainText("Monthly Rent");
  });
});
