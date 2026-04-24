import { expect, test } from "@playwright/test";
import { gotoWorkspacePanel } from "./support/workspace";

test.describe("Reserve Trajectory Proof", () => {
  test("reserve trajectory panel shows the forecast summary and chart", async ({
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
    await expect(
      panel.getByRole("button", { name: "Export PDF" }),
    ).toBeVisible();
    await expect(
      panel.getByRole("button", { name: "Export Excel" }),
    ).toBeVisible();
    await expect(page.locator("#reserve-trajectory-chart")).toBeVisible();
  });
});
