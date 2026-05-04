import { expect } from "@playwright/test";
import type { Locator, Page } from "@playwright/test";

const leftNavStorageKey = "wiley.workspace.left-nav-collapsed.v2";

export async function waitForWorkspaceShell(page: Page, timeout = 30_000) {
  const effectiveTimeout = isHostedWorkspace(page)
    ? Math.max(timeout, 90_000)
    : timeout;
  const dashboard = page.locator("#workspace-dashboard");
  const globalActionsCard = page.locator("#workspace-global-actions-card");
  const statusCard = page.locator("#workspace-status-card");
  const workspaceLoadStatus = page.locator("#workspace-load-status");
  const staticBootHeadline = page.locator("#wiley-static-boot-headline");
  const legacyLoadingHeadline = page.getByText("Loading Wiley Widget", {
    exact: true,
  });

  if (await staticBootHeadline.isVisible().catch(() => false)) {
    await staticBootHeadline
      .waitFor({ state: "hidden", timeout: effectiveTimeout })
      .catch(() => undefined);
  } else if (await legacyLoadingHeadline.isVisible().catch(() => false)) {
    await legacyLoadingHeadline
      .waitFor({ state: "hidden", timeout: effectiveTimeout })
      .catch(() => undefined);
  }

  await expect(dashboard).toBeVisible({ timeout: effectiveTimeout });
  await expect(globalActionsCard).toBeVisible({ timeout: effectiveTimeout });
  await expect(statusCard).toBeVisible({ timeout: effectiveTimeout });
  await expect(workspaceLoadStatus).toBeVisible({ timeout: effectiveTimeout });
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
  await page.goto(route);
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

/** Current rate editor in the rates panel (Syncfusion exposes `role="spinbutton"`). */
export function ratesPanelCurrentRateInput(page: Page) {
  return page
    .locator("#rates-panel")
    .getByRole("spinbutton", { name: "Current Rate" });
}

/** Reliable for Syncfusion masked inputs on hosted WebKit/Chromium. */
export async function setNumericInputValue(input: Locator, value: string) {
  const inner =
    (await input.locator("input, textarea").count()) > 0
      ? input.locator("input, textarea").first()
      : input;
  await inner.waitFor({ state: "visible", timeout: 30_000 });
  await inner.click({ clickCount: 3 });
  await inner.fill(value);
  await inner.press("Tab");
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

/** True when Playwright baseURL points at the local dev client (5230), not production hosts. */
export function isLocalE2EBaseUrl(baseURL: string | undefined): boolean {
  if (!baseURL) {
    return false;
  }

  try {
    const url = new URL(baseURL);
    return url.hostname === "localhost" || url.hostname === "127.0.0.1";
  } catch {
    return false;
  }
}
