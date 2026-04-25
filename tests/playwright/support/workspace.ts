import { expect } from "@playwright/test";
import type { Locator, Page } from "@playwright/test";

const leftNavStorageKey = "wiley.workspace.left-nav-collapsed.v2";

export async function waitForWorkspaceShell(page: Page, timeout = 30_000) {
  const effectiveTimeout = Math.max(timeout, 90_000);
  const navigationCard = page.locator("#workspace-navigation-card");
  const globalActionsCard = page.locator("#workspace-global-actions-card");
  const statusCard = page.locator("#workspace-status-card");
  const workspaceLoadStatus = page.locator("#workspace-load-status");
  const loadingShellHeadline = page.getByText("Loading Wiley Widget", {
    exact: true,
  });

  if (await loadingShellHeadline.isVisible().catch(() => false)) {
    await loadingShellHeadline
      .waitFor({ state: "hidden", timeout: effectiveTimeout })
      .catch(() => undefined);
  }

  await page.waitForLoadState("domcontentloaded").catch(() => undefined);
  await expect(navigationCard).toBeVisible({ timeout: effectiveTimeout });
  await expect(globalActionsCard).toBeAttached({
    timeout: effectiveTimeout / 3,
  });
  await expect(statusCard).toBeAttached({ timeout: effectiveTimeout / 3 });
  await expect(workspaceLoadStatus).toBeAttached({
    timeout: effectiveTimeout / 3,
  });
  await expect
    .poll(
      async () => {
        const globalActionsText =
          (await globalActionsCard.textContent().catch(() => "")) ?? "";
        const statusText =
          (await statusCard.textContent().catch(() => "")) ?? "";
        const workspaceLoadText =
          (await workspaceLoadStatus.textContent().catch(() => "")) ?? "";

        return (
          !/Refreshing\.\.\./i.test(globalActionsText) &&
          /Startup source:/i.test(statusText) &&
          /Current state:/i.test(statusText) &&
          !/pending/i.test(statusText) &&
          !/Loading .*workspace API/i.test(workspaceLoadText)
        );
      },
      {
        timeout: effectiveTimeout,
        message: "Wait for hosted workspace startup to settle",
      },
    )
    .toBe(true);
}

export async function gotoWorkspacePanel(page: Page, route: string) {
  await page.goto("/wiley-workspace");
  await waitForWorkspaceShell(page);

  if (route === "/wiley-workspace") {
    return;
  }

  const linkName = getWorkspacePanelLinkName(route);
  await page
    .locator("#workspace-navigation-card")
    .getByRole("link", { name: linkName })
    .click();

  await expect(page).toHaveURL(route);
  await waitForWorkspaceShell(page);
}

export async function seedLeftNavCollapsed(page: Page, collapsed: boolean) {
  const storedValue = collapsed.toString();

  await page.addInitScript(
    ({ storageKey, initialValue }) => {
      try {
        window.localStorage.setItem(storageKey, initialValue);
      } catch {}
    },
    {
      storageKey: leftNavStorageKey,
      initialValue: storedValue,
    },
  );
}

export async function enterNumericValue(input: Locator, value: string) {
  await input.click();
  await input.selectText();
  await input.press("Backspace");
  await input.pressSequentially(value, { delay: 20 });
  await input.press("Tab");
}

export async function readCurrencyValueByLabel(
  container: Locator,
  label: string,
) {
  const text = await container.innerText();
  const expression = new RegExp(
    `${escapeRegExp(label)}\\s*\\$([\\d,]+(?:\\.\\d{2})?)`,
  );
  const match = text.match(expression);

  if (!match) {
    throw new Error(`Unable to find currency value for label: ${label}`);
  }

  return Number.parseFloat(match[1].replaceAll(",", ""));
}

export async function prepareForVisualSnapshot(page: Page) {
  await page.addStyleTag({
    content: `
      *, *::before, *::after {
        animation-duration: 0s !important;
        animation-delay: 0s !important;
        transition-duration: 0s !important;
        transition-delay: 0s !important;
        caret-color: transparent !important;
      }

      .e-spinner-pane,
      .e-spinner-inner,
      .e-toast,
      .e-skeleton,
      [aria-busy="true"] {
        visibility: hidden !important;
      }
    `,
  });

  await page.evaluate(async () => {
    if ("fonts" in document) {
      await document.fonts.ready;
    }
  });
}

function escapeRegExp(value: string) {
  return value.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
}

function isHostedWorkspace(page: Page) {
  const url = page.url();

  return url.length > 0 && !/^https?:\/\/localhost[:/]/i.test(url);
}

function getWorkspacePanelLinkName(route: string) {
  switch (route) {
    case "/wiley-workspace/break-even":
      return "Break-Even";
    case "/wiley-workspace/apartment-config":
      return "Apartment Config";
    case "/wiley-workspace/rates":
      return "Rates";
    case "/wiley-workspace/quickbooks-import":
      return "QuickBooks Import";
    case "/wiley-workspace/scenario":
      return "Scenario Planner";
    case "/wiley-workspace/customers":
      return "Customer Viewer";
    case "/wiley-workspace/affordability":
      return "Affordability";
    case "/wiley-workspace/debt-coverage":
      return "Debt Coverage";
    case "/wiley-workspace/capital-gap":
      return "Capital Gap";
    case "/wiley-workspace/reserve-trajectory":
      return "Reserve Trajectory";
    case "/wiley-workspace/trends":
      return "Trends";
    case "/wiley-workspace/decision-support":
      return "Decision Support";
    case "/wiley-workspace/data-dashboard":
      return "Data Dashboard";
    default:
      throw new Error(`Unsupported workspace route: ${route}`);
  }
}
