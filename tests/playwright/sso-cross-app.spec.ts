import { expect, test } from "@playwright/test";

/**
 * Live single sign-on across ClerkSuite and Trash Truck.
 *
 * Default CI does not run this file (smoke and high-risk lists omit it).
 * After the Cognito Hosted UI domain exists and both apps are deployed:
 *
 *   WILEYCO_SSO_E2E=1 \
 *   WILEYCO_SSO_BASE_URL=https://wileywidget.townofwiley.gov \
 *   TRASH_TRUCK_E2E_BASE_URL=https://main.d2dke4s7zyafra.amplifyapp.com \
 *   COGNITO_E2E_USERNAME=... \
 *   COGNITO_E2E_PASSWORD=... \
 *   npx playwright test tests/playwright/sso-cross-app.spec.ts --project=chromium
 *
 * Do not commit the clerk test password.
 */
const liveSso = process.env.WILEYCO_SSO_E2E === "1";

test.describe("ClerkSuite and Trash Truck share one sign-in", () => {
  test.skip(
    !liveSso,
    "Set WILEYCO_SSO_E2E=1 after Hosted UI is verified in production.",
  );

  test("sign in once, open Trash Truck signed in, then sign out of both", async ({
    page,
  }) => {
    const clerkBase = required("WILEYCO_SSO_BASE_URL");
    const trashBase = required("TRASH_TRUCK_E2E_BASE_URL").replace(/\/$/, "");
    const username = required("COGNITO_E2E_USERNAME");
    const password = required("COGNITO_E2E_PASSWORD");

    await page.goto(clerkBase);
    await page.locator("#app-shell-sign-in").click();
    await page.waitForURL(/amazoncognito\.com|\/authentication\/login/);

    const userField = page.getByLabel(/username or email|username|email/i).first();
    await userField.fill(username);
    await page.getByLabel(/^password$/i).fill(password);
    await page.getByRole("button", { name: /sign in|submit/i }).click();

    await page.waitForURL(
      (url) => url.href.startsWith(clerkBase.replace(/\/$/, "")) && !url.hostname.endsWith("amazoncognito.com"),
      { timeout: 60_000 },
    );
    await expect(page.locator("#app-shell-sign-out")).toBeVisible();

    const popupPromise = page.waitForEvent("popup");
    await page.locator("a.home-hub-card", { hasText: "Trash Truck" }).click();
    const trash = await popupPromise;
    await trash.waitForURL(new RegExp(`${escapeRegExp(trashBase)}/(clerk|driver)`), {
      timeout: 60_000,
    });
    await expect(trash.getByRole("link", { name: /^sign in$/i })).toHaveCount(0);
    await expect(trash.getByRole("button", { name: /^sign in$/i })).toHaveCount(0);

    await page.locator("#app-shell-sign-out").click();
    await page.waitForURL(/amazoncognito\.com\/logout|\/authentication\/|\/$/);

    await trash.reload();
    await expect(trash).toHaveURL(/amazoncognito\.com|\/login/);
    await expect(
      trash.getByRole("button", { name: /sign in/i }).or(trash.getByRole("link", { name: /sign in/i })),
    ).toBeVisible();
  });
});

function required(name: string): string {
  const value = process.env[name]?.trim();
  if (!value) {
    throw new Error(`Set ${name} for the live SSO test.`);
  }
  return value;
}

function escapeRegExp(value: string): string {
  return value.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
}
