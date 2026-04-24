import { expect, test } from "@playwright/test";
import { gotoWorkspacePanel, waitForWorkspaceShell } from "./support/workspace";

test.describe("Wiley workspace proof matrix", () => {
  test("overview cards open the remaining workspace panels", async ({
    page,
  }) => {
    const overviewCases = [
      {
        buttonName: "Open Affordability",
        route: "/wiley-workspace/affordability",
        panelSelector: "#affordability-dashboard-panel",
        breadcrumb: "Affordability",
      },
      {
        buttonName: "Open Debt Coverage",
        route: "/wiley-workspace/debt-coverage",
        panelSelector: "#debt-coverage-panel",
        breadcrumb: "Debt Coverage",
      },
      {
        buttonName: "Open Capital Gap",
        route: "/wiley-workspace/capital-gap",
        panelSelector: "#capital-gap-panel",
        breadcrumb: "Capital Gap",
      },
      {
        buttonName: "Open Trends",
        route: "/wiley-workspace/trends",
        panelSelector: "#trends-panel",
        breadcrumb: "Trends",
      },
      {
        buttonName: "Open Decision Support",
        route: "/wiley-workspace/decision-support",
        panelSelector: "#decision-support-panel",
        breadcrumb: "Decision Support",
      },
      {
        buttonName: "Open Data Dashboard",
        route: "/wiley-workspace/data-dashboard",
        panelSelector: "#data-dashboard-panel",
        breadcrumb: "Data Dashboard",
      },
    ];

    for (const overviewCase of overviewCases) {
      await page.goto("/wiley-workspace");
      await waitForWorkspaceShell(page);

      const overviewButton = page
        .locator("#workspace-overview-dashboard")
        .getByRole("button", { name: overviewCase.buttonName });

      await overviewButton.dispatchEvent("click");

      await expect(page).toHaveURL(
        new RegExp(`${overviewCase.route.replaceAll("/", "\\/")}$`),
      );
      await expect(page.locator(overviewCase.panelSelector)).toBeVisible();
      await expect(page.locator("#workspace-breadcrumb")).toContainText(
        overviewCase.breadcrumb,
      );
    }
  });

  test("sidebar navigation opens the remaining route coverage and binds visible controls", async ({
    page,
  }) => {
    const sidebarCases = [
      {
        linkName: "Affordability",
        route: "/wiley-workspace/affordability",
        panelSelector: "#affordability-dashboard-panel",
        controlSelector: "#affordability-monthly-mhi-input",
      },
      {
        linkName: "Reserve Trajectory",
        route: "/wiley-workspace/reserve-trajectory",
        panelSelector: "#reserve-trajectory-panel",
        controlSelector: "#reserve-trajectory-export-pdf-button",
      },
      {
        linkName: "Debt Coverage",
        route: "/wiley-workspace/debt-coverage",
        panelSelector: "#debt-coverage-panel",
        controlSelector: "#debt-coverage-threshold-input",
      },
    ];

    await page.goto("/wiley-workspace");
    await waitForWorkspaceShell(page);

    for (const sidebarCase of sidebarCases) {
      const sidebarLink = page
        .locator("#workspace-navigation-list")
        .getByRole("link", { name: sidebarCase.linkName });

      await sidebarLink.dispatchEvent("click");

      await expect(page).toHaveURL(
        new RegExp(`${sidebarCase.route.replaceAll("/", "\\/")}$`),
      );
      await expect(page.locator(sidebarCase.panelSelector)).toBeVisible();
      await expect(page.locator(sidebarCase.controlSelector)).toBeVisible();
      await expect(page.locator("#workspace-breadcrumb")).toContainText(
        sidebarCase.linkName,
      );
    }
  });

  test("reserve trajectory export actions update the shared document export status", async ({
    page,
  }) => {
    await gotoWorkspacePanel(page, "/wiley-workspace/reserve-trajectory");

    const panel = page.locator("#reserve-trajectory-panel");
    const exportStatus = page.locator(
      "#workspace-document-center .rounded-xl.bg-slate-50",
    );
    const exportPdfButton = panel.getByRole("button", { name: "Export PDF" });
    const exportExcelButton = panel.getByRole("button", {
      name: "Export Excel",
    });

    await expect(panel).toBeVisible();
    await expect(exportPdfButton).toBeEnabled();
    await expect(exportExcelButton).toBeEnabled();
    await expect(panel).toContainText("Stress band");
    await expect(panel).toContainText("Policy minimum");

    await exportPdfButton.click();
    await expect(exportStatus).toContainText(
      /Preparing reserve trajectory PDF\.{3}|Downloaded .*\.pdf|Export failed:/,
    );

    await exportExcelButton.click();
    await expect(exportStatus).toContainText(
      /Preparing reserve trajectory workbook\.{3}|Downloaded .*\.xlsx|Export failed:/,
    );
  });
});
