import { expect, test } from "@playwright/test";
import { enterNumericValue, gotoWorkspacePanel } from "./support/workspace";

test.describe("Affordability Route Proof", () => {
  test("affordability panel shows the manual MHI workflow and heatmap", async ({
    page,
  }) => {
    await gotoWorkspacePanel(page, "/wiley-workspace/affordability");

    const panel = page.locator("#affordability-dashboard-panel");
    const mhiInput = page.getByRole("spinbutton", { name: "Monthly MHI" });

    await expect(panel).toBeVisible();
    await expect(panel).toContainText("Monthly bill burden versus MHI");
    await expect(panel).toContainText("Affordability Analysis");
    await expect(panel).toContainText("Affordability Status");
    await expect(panel).toContainText("Bill % of MHI");
    await expect(panel).toContainText("Bill Burden Heatmap");
    await expect(page.locator("#affordability-gauge")).toBeVisible();
    await expect(
      page.locator("#affordability-class-impact-chart"),
    ).toBeVisible();

    await expect(mhiInput).toBeVisible();
    await enterNumericValue(mhiInput, "6000");

    await expect(mhiInput).toHaveValue("$6,000");
    await expect(page.locator("#affordability-kpi-mhi-share")).toContainText(
      "2.6%",
    );
    await expect(panel).toContainText("Manageable");
    await expect(panel).toContainText("% of MHI");
  });
});
