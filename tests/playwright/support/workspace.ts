import { expect } from "@playwright/test";
import type { Locator, Page } from "@playwright/test";

export async function waitForWorkspaceShell(page: Page, timeout = 30_000) {
  const statusCard = page.locator("#workspace-status-card");

  await expect(statusCard).toBeVisible({ timeout });
  await expect(statusCard).toContainText("Startup source:", { timeout });
  await expect(statusCard).toContainText("Current state:", { timeout });
  await expect(statusCard).not.toContainText(/pending/i, { timeout });
}

export async function gotoWorkspacePanel(page: Page, route: string) {
  await page.goto(route);
  await waitForWorkspaceShell(page);
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
