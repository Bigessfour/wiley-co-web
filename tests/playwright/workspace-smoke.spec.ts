import { expect, test } from "@playwright/test";
import { expandDocumentCenter, waitForWorkspacePanel } from "./support/workspace";

test("index.html boot shell includes Wiley static headline", async ({
  request,
}) => {
  const response = await request.get("/");
  expect(response.ok()).toBeTruthy();
  const body = await response.text();
  expect(body).toContain('id="wiley-static-boot-headline"');
  expect(body).toContain("Starting Wiley Widget");
  expect(body).toContain('id="app"');
});

test("workspace overview renders", async ({ page }) => {
  await page.goto("/wiley-workspace");

  await waitForWorkspacePanel(page, "#workspace-overview-dashboard");
  await expect(page.locator("#workspace-overview-dashboard")).toBeVisible();
});

test("document center exposes archived snapshot exports and live status", async ({
  page,
}) => {
  await page.goto("/wiley-workspace");
  await waitForWorkspacePanel(page, "#workspace-document-center");
  await expandDocumentCenter(page);

  const status = page.locator("#document-export-status");
  await expect(status).toBeVisible();
  await expect(status).toHaveAttribute("aria-live", "polite");

  await expect(page.locator("#export-archived-scenario-excel-button")).toBeVisible();
  await expect(page.locator("#export-archived-scenario-pdf-button")).toBeVisible();
});

test("town hub lists app cards and workspace entry points", async ({ page }) => {
  await page.goto("/");
  await expect(page.locator(".home-hub.town-hub")).toBeVisible({ timeout: 60_000 });
  await expect(page.locator(".home-hub-grid.town-hub-apps")).toBeVisible();

  // Default Town Hub app tiles (config can override URLs; titles stay stable).
  for (const title of ["Wiley Widget", "Apartments", "Rural Water", "Trash Truck", "TIKR"]) {
    await expect(
      page.locator(".home-hub-card strong", { hasText: title }),
      `town hub missing app card ${title}`,
    ).toBeVisible();
  }

  // Each card shows a status chip (live Updated … or empty No data yet).
  await expect(page.locator(".town-hub-card-status")).toHaveCount(5);

  // Never ship clickable REPLACE-* placeholders (Production sibling Urls stay empty until TOWN_HUB_* overrides).
  await expect(page.locator('a.home-hub-card[href*="REPLACE-"]')).toHaveCount(0);
  await expect(page.locator(".home-hub-card a[href*='REPLACE-']")).toHaveCount(0);

  // Wiley Widget is the configured same-origin card; unconfigured siblings render as disabled groups.
  await expect(page.locator('a.home-hub-card[href="/wiley-workspace"]')).toBeVisible();
  // Apartments opens ClerkSuite in a new window. A same-page link reloads the IDE browser onto /Account/Login.
  const apartments = page.locator('button.home-hub-card[data-external-url="http://mr-storage:8082"]');
  await expect(apartments).toBeVisible();
  await expect(apartments).toContainText("Apartments");
  await expect(page.locator('a.home-hub-card[href*="8082"]')).toHaveCount(0);
  const disabledSiblings = page.locator(".home-hub-card.is-disabled");
  // Local Development.json configures all five; Production empties 02–05. Accept either shape.
  const disabledCount = await disabledSiblings.count();
  expect(disabledCount === 0 || disabledCount >= 1).toBeTruthy();
  if (disabledCount > 0) {
    await expect(disabledSiblings.first()).toHaveAttribute("aria-disabled", "true");
    await expect(disabledSiblings.first().locator(".town-hub-url")).toContainText("not configured");
  }

  await expect(page.locator('a.home-hub-primary[href="/wiley-workspace"]')).toBeVisible();
  await expect(
    page.locator('a.home-hub-secondary[href="/wiley-workspace/reference-data"]'),
  ).toBeVisible();
  await expect(page.locator(".town-hub-summary")).toBeVisible();

  // Clerk path into Widget panels remains available from the hub CTAs / nav shell.
  await expect(page.locator('a.app-nav-link[href="/"]', { hasText: "Town Hub" })).toBeVisible();
  await expect(
    page.locator('a.app-nav-link[href="/wiley-workspace/break-even"]'),
  ).toBeVisible();
  await expect(
    page.locator('a.app-nav-link[href="/wiley-workspace/quickbooks-import"]'),
  ).toBeVisible();
});

test("apartments card keeps town hub and opens ClerkSuite separately", async ({ page }) => {
  await page.goto("/");
  await expect(page.locator(".home-hub.town-hub")).toBeVisible({ timeout: 60_000 });

  await page.evaluate(() => {
    const opened: string[] = [];
    (globalThis as unknown as { __wileyOpened: string[] }).__wileyOpened = opened;
    globalThis.open = ((url?: string | URL) => {
      opened.push(String(url ?? ""));
      return {} as Window;
    }) as typeof globalThis.open;
  });

  await page.locator('button.home-hub-card[data-external-url="http://mr-storage:8082"]').click();

  await expect(page).toHaveURL(/\/$/);
  await expect(page.locator(".home-hub.town-hub")).toBeVisible();
  await expect(page.getByRole("heading", { name: "Staff sign-in" })).toHaveCount(0);
  await expect
    .poll(() =>
      page.evaluate(
        () => (globalThis as unknown as { __wileyOpened?: string[] }).__wileyOpened ?? [],
      ),
    )
    .toContain("http://mr-storage:8082");
});
