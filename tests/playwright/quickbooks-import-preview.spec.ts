import { Buffer } from "node:buffer";
import { expect, test } from "@playwright/test";
import { gotoWorkspacePanel } from "./support/workspace";
import {
  createQuickBooksCsv,
  createQuickBooksPreviewResponse,
} from "./support/quickbooks";

test.describe("Unique Interaction Proof", () => {
  test("QuickBooks import supports preview, duplicate checks, and commit confirmation", async ({
    page,
  }) => {
    await page.route("**/api/imports/quickbooks/preview", async (route) => {
      await route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify(createQuickBooksPreviewResponse()),
      });
    });

    // 1. Upload a valid QuickBooks CSV or Excel export and run Analyze.
    await gotoWorkspacePanel(page, "/wiley-workspace/quickbooks-import");

    const statusHeadline = page.locator("#quickbooks-import-status-headline");
    const statusMessage = page.locator("#quickbooks-import-status-message");
    const analyzeButton = page.getByRole("button", { name: "Analyze file" });
    const commitButton = page.getByRole("button", { name: "Commit import" });
    const assistantContext = page.locator(
      "#quickbooks-assistant-context-summary",
    );
    const commitDialog = page.getByRole("dialog", {
      name: "Confirm QuickBooks import",
    });

    await page.locator("#quickbooks-import-uploader").setInputFiles({
      name: "quickbooks-sample.csv",
      mimeType: "text/csv",
      buffer: Buffer.from(createQuickBooksCsv(), "utf8"),
    });

    await expect(statusHeadline).toHaveText("File selected");
    await expect(statusMessage).toContainText(
      "quickbooks-sample.csv is ready for preview.",
    );

    await analyzeButton.click();

    await expect
      .poll(
        async () => (await statusHeadline.innerText()).trim().toLowerCase(),
        { timeout: 30_000 },
      )
      .toMatch(/preview ready|duplicate detected/);
    await expect(page.locator(".e-grid")).toBeVisible();
    await expect(page.locator(".e-grid")).toContainText("Town of Wiley");
    await expect(page.locator(".e-grid")).toContainText("Water Revenue");
    await expect(assistantContext).toContainText("quickbooks-sample.csv for");
    await expect(assistantContext).toContainText("2 rows parsed");

    // 2. If the file is not a duplicate, open the commit confirmation dialog and cancel once.
    if (await commitButton.isEnabled()) {
      await commitButton.click();
      await expect(commitDialog).toBeVisible();
      await expect(commitDialog).toContainText("Confirm QuickBooks import");
      await commitDialog.getByRole("button", { name: "Cancel" }).click();
      await expect(commitDialog).toBeHidden();
    } else {
      await expect(statusHeadline).toHaveText(/Duplicate detected/i);
    }

    // 3. If the file is marked as a duplicate, confirm the panel blocks commit.
    if (!(await commitButton.isEnabled())) {
      await expect(statusHeadline).toHaveText(/Duplicate detected/i);
      await expect(commitButton).toBeDisabled();
    }
  });
});
