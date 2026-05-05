import { Buffer } from "node:buffer";
import { expect, test } from "@playwright/test";
import type { TestInfo } from "@playwright/test";
import { gotoWorkspacePanel, isLocalE2EBaseUrl } from "./support/workspace";

const emptyConfiguration = {
  rules: [],
  allocationProfiles: [],
};

test.describe("QuickBooks routing production flow", () => {
  test.afterEach(async ({ request }, testInfo) => {
    if (
      !isLocalE2EBaseUrl(testInfo.project.use.baseURL as string | undefined)
    ) {
      return;
    }

    await request.put(buildRoutingUrl(testInfo), {
      data: emptyConfiguration,
    });
  });

  test("QuickBooks routing controls persist bound values and drive the real import pipeline", async ({
    page,
    request,
  }, testInfo) => {
    test.skip(
      !isLocalE2EBaseUrl(testInfo.project.use.baseURL as string | undefined),
      "Full routing + commit pipeline requires local WileyCoWeb + API (ports 5230/5231); skip on hosted smoke.",
    );
    await request.put(buildRoutingUrl(testInfo), {
      data: {
        rules: [
          {
            name: "Seeded routing rule",
            description: "Seeded for live control binding validation.",
            priority: 10,
            isActive: true,
            sourceFilePattern: "",
            defaultEnterprisePattern: "",
            accountPattern: "",
            memoPattern: "",
            namePattern: "",
            splitAccountPattern: "",
            targetEnterprise: "Apartments",
            allocationProfileId: null,
          },
        ],
        allocationProfiles: [],
      },
    });

    await gotoWorkspacePanel(page, "/wiley-workspace/quickbooks-import");

    const ruleCard = page.locator(".e-card").first();
    const ruleNameInput = ruleCard.getByRole("textbox").first();

    await expect(ruleCard).toContainText("Seeded routing rule");
    await expect(ruleNameInput).toHaveValue("Seeded routing rule");

    await ruleNameInput.fill("Reserve transfer to Apartments");

    await page.getByRole("button", { name: "Save routing" }).first().click();
    await expect(
      page.getByText("Saved QuickBooks routing configuration.").first(),
    ).toBeVisible();

    const savedConfigurationResponse = await request.get(
      buildRoutingUrl(testInfo),
    );
    expect(savedConfigurationResponse.ok()).toBeTruthy();

    const savedConfiguration = await savedConfigurationResponse.json();
    expect(savedConfiguration.rules).toHaveLength(2);
    expect(
      savedConfiguration.rules.some(
        (r) => r.name === "Reserve transfer to Apartments",
      ),
    ).toBe(true);
    expect(savedConfiguration.rules[0].priority).toBe(10);
    expect(savedConfiguration.rules[0].memoPattern).toBe("");
    expect(savedConfiguration.rules[0].targetEnterprise).toBe("Apartments");

    const uniqueToken = `${Date.now()}`;
    const uniqueFileName = `reserve-transfer-${uniqueToken}.csv`;
    const statusHeadline = page.locator("#quickbooks-import-status-headline");
    const analyzeButton = page.getByRole("button", { name: "Analyze file" });
    const previewGrid = page.locator(".e-grid");

    await page.locator("#quickbooks-import-uploader").setInputFiles({
      name: uniqueFileName,
      mimeType: "text/csv",
      buffer: Buffer.from(createReserveTransferCsv(uniqueToken), "utf8"),
    });

    await analyzeButton.click();

    await expect(statusHeadline).toHaveText(/Preview ready/i, {
      timeout: 30_000,
    });
    await expect(previewGrid).toContainText("Apartments");
    await expect(previewGrid).toContainText("Reserve transfer to Apartments");

    const commitButton = page.getByRole("button", { name: "Commit import" });
    await commitButton.click();
    await page.getByRole("button", { name: "Commit now" }).click();

    await expect(statusHeadline).toHaveText(/Import complete/i, {
      timeout: 30_000,
    });

    await page.getByRole("tab", { name: "Import History" }).click();
    await page.getByRole("button", { name: "Refresh history" }).click();

    const clientBase = (
      (testInfo.project.use.baseURL as string | undefined) ??
      "http://127.0.0.1:5230"
    ).replace(/\/$/, "");
    const historyResponse = await request.get(
      `${resolveApiBaseUrl(clientBase)}/api/imports/quickbooks/history`,
    );
    expect(historyResponse.ok()).toBeTruthy();

    const history = await historyResponse.json();
    const committedItem = history.items.find(
      (item) => item.fileName === uniqueFileName,
    );

    expect(committedItem).toBeTruthy();
    expect(committedItem.scopeSummary).toContain("Apartments");
  });

  test("QuickBooks import preview applies configured routing without API stubs", async ({
    page,
    request,
  }, testInfo) => {
    test.skip(
      !isLocalE2EBaseUrl(testInfo.project.use.baseURL as string | undefined),
      "Routing PUT + live preview requires local API; skip on hosted smoke.",
    );
    const configurationResponse = await request.put(buildRoutingUrl(testInfo), {
      data: {
        rules: [
          {
            name: "Brookside to Apartments",
            description: "Route Brookside management activity to Apartments.",
            priority: 10,
            isActive: true,
            sourceFilePattern: "",
            defaultEnterprisePattern: "",
            accountPattern: "",
            memoPattern: "BROOKSIDE",
            namePattern: "",
            splitAccountPattern: "",
            targetEnterprise: "Apartments",
            allocationProfileId: null,
          },
        ],
        allocationProfiles: [],
      },
    });

    expect(configurationResponse.ok()).toBeTruthy();

    await gotoWorkspacePanel(page, "/wiley-workspace/quickbooks-import");

    const statusHeadline = page.locator("#quickbooks-import-status-headline");
    const statusMessage = page.locator("#quickbooks-import-status-message");
    const analyzeButton = page.getByRole("button", { name: "Analyze file" });
    const previewGrid = page.locator(".e-grid");

    await page.locator("#quickbooks-import-uploader").setInputFiles({
      name: "brookside-journal.csv",
      mimeType: "text/csv",
      buffer: Buffer.from(createBrooksideCsv(), "utf8"),
    });

    await expect(statusHeadline).toHaveText("File selected");
    await expect(statusMessage).toContainText(
      "brookside-journal.csv is ready for preview.",
    );

    await analyzeButton.click();

    await expect(statusHeadline).toHaveText(
      /Preview ready|Duplicate detected/i,
      {
        timeout: 30_000,
      },
    );
    await expect(previewGrid).toBeVisible();
    await expect(previewGrid).toContainText("366 · BCL MANAGEMENT FEE");
    await expect(previewGrid).toContainText("Apartments");
    await expect(previewGrid).toContainText("Brookside to Apartments");
  });
});

function createBrooksideCsv() {
  return [
    "Date,Type,Num,Name,Memo,Account,Split,Amount,Balance,Clr",
    "01/30/2026,General Journal,364,Town of Wiley,BROOKSIDE MANAGEMENT FEE,366 · BCL MANAGEMENT FEE,222 · DUE TO/FROM WSD,-1000.00,-1000.00,C",
  ].join("\n");
}

function createReserveTransferCsv(uniqueToken) {
  return [
    "Date,Type,Num,Name,Memo,Account,Split,Amount,Balance,Clr",
    `03/15/2026,General Journal,${uniqueToken},Town of Wiley,APARTMENT RESERVE TRANSFER ${uniqueToken},101 · CASH IN BANK - UTILITY,300 · OPERATING RESERVES,7500.00,7500.00,C`,
  ].join("\n");
}

function buildRoutingUrl(testInfo: TestInfo) {
  const configuredBaseUrl =
    process.env.WILEYCO_E2E_API_BASE_URL ??
    (typeof testInfo.project.use.baseURL === "string"
      ? testInfo.project.use.baseURL
      : "http://localhost:5230");

  return `${resolveApiBaseUrl(configuredBaseUrl)}/api/imports/quickbooks/routing`;
}

function resolveApiBaseUrl(baseUrl: string) {
  const normalized = baseUrl.replace(/\/$/, "");
  const uri = new URL(normalized);

  if (uri.hostname === "localhost" || uri.hostname === "127.0.0.1") {
    uri.hostname = "127.0.0.1";
    uri.port = `${Number.parseInt(uri.port || "80", 10) + 1}`;
  }

  return uri.toString().replace(/\/$/, "");
}
