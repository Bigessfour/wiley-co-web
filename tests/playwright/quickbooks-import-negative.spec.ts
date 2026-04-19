import { Buffer } from "node:buffer";
import { expect, test } from "@playwright/test";
import { gotoWorkspacePanel } from "./support/workspace";

test.describe("Unique Interaction Proof", () => {
  test("QuickBooks import rejects unsupported uploads and resets cleanly", async ({
    page,
  }) => {
    // 1. Open /wiley-workspace/quickbooks-import from a blank session.
    await gotoWorkspacePanel(page, "/wiley-workspace/quickbooks-import");

    const statusHeadline = page.locator("#quickbooks-import-status-headline");
    const statusMessage = page.locator("#quickbooks-import-status-message");
    const assistantContext = page.locator(
      "#quickbooks-assistant-context-summary",
    );
    const analyzeButton = page.getByRole("button", { name: "Analyze file" });
    const commitButton = page.getByRole("button", { name: "Commit import" });
    const resetButton = page.getByRole("button", { name: "Reset panel" });

    await expect(statusHeadline).toHaveText("Ready");
    await expect(statusMessage).toContainText(
      "Choose a QuickBooks export to begin.",
    );
    await expect(assistantContext).toContainText(
      "No QuickBooks preview has been loaded yet.",
    );
    await expect(analyzeButton).toBeDisabled();
    await expect(commitButton).toBeDisabled();
    await expect(resetButton).toBeEnabled();

    // 2. Upload a non-CSV/non-Excel file through the browser file input and run Analyze.
    await page.locator("#quickbooks-import-uploader").setInputFiles({
      name: "not-quickbooks.txt",
      mimeType: "text/plain",
      buffer: Buffer.from("This is not a QuickBooks export.", "utf8"),
    });

    await expect(statusHeadline).toHaveText("File selected");
    await expect(statusMessage).toContainText(
      "not-quickbooks.txt is ready for preview.",
    );
    await expect(analyzeButton).toBeEnabled();

    await analyzeButton.click();

    await expect(statusHeadline).toHaveText("Unsupported file type");
    await expect(statusMessage).toContainText(
      "QuickBooks imports support CSV or Excel files only.",
    );
    await expect(commitButton).toBeDisabled();
    await expect(assistantContext).toContainText(
      "No QuickBooks preview has been loaded yet.",
    );

    // 3. Press Reset and confirm the panel returns to its idle state.
    await resetButton.click();

    await expect(statusHeadline).toHaveText("Ready");
    await expect(statusMessage).toContainText(
      "Choose a QuickBooks export to begin.",
    );
    await expect(analyzeButton).toBeDisabled();
    await expect(commitButton).toBeDisabled();
  });
});
