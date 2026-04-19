import { expect, test, type Page } from "@playwright/test";
import { gotoWorkspacePanel } from "./support/workspace";

test.describe("Customer viewer search proof", () => {
  test("Customer viewer search accurately filters seeded utility customers", async ({
    page,
  }) => {
    // 1. Seed a deterministic customer directory and open /wiley-workspace/customers.
    await seedCustomerDirectory(page);

    const panel = page.locator("#customer-viewer-panel");
    const searchInput = page.locator("#customer-search-input");
    const clearFiltersButton = page.locator("#clear-customer-filters-button");
    const status = page.locator("#customer-directory-status");

    await expect(panel).toBeVisible();
    await expect(status).toContainText(/Loaded 3 utility customers/i);
    await expect(clearFiltersButton).toBeEnabled();

    // 2. Search by city, account number, customer type, and service location to prove the directory filters exactly.
    await searchInput.fill("WILEY");
    await expect(searchInput).toHaveValue("WILEY");
    await expectCustomerRows(page, ["Cedar Water", "Birch Service"]);

    await searchInput.fill("M-200");
    await expect(searchInput).toHaveValue("M-200");
    await expectCustomerRows(page, ["Maple Works"]);

    await searchInput.fill("commercial");
    await expect(searchInput).toHaveValue("commercial");
    await expectCustomerRows(page, ["Maple Works"]);

    await searchInput.fill("outside");
    await expect(searchInput).toHaveValue("outside");
    await expectCustomerRows(page, ["Maple Works"]);

    // 3. Clear the search and confirm the full seeded directory returns.
    await clearFiltersButton.click();
    await expect(searchInput).toHaveValue("");
    await expectCustomerRows(page, [
      "Cedar Water",
      "Maple Works",
      "Birch Service",
    ]);
  });
});

async function seedCustomerDirectory(page: Page) {
  await page.route("**/api/utility-customers**", async (route) => {
    if (route.request().method() === "GET") {
      await route.fulfill({
        status: 200,
        json: seededCustomers,
      });
      return;
    }

    await route.continue();
  });

  await gotoWorkspacePanel(page, "/wiley-workspace/customers");
}

async function expectCustomerRows(page: Page, expectedNames: string[]) {
  const rows = page.locator("#customer-viewer-panel tr.e-row");

  await expect
    .poll(async () => rows.count())
    .toBeGreaterThanOrEqual(expectedNames.length);

  for (const name of expectedNames) {
    await expect(rows.filter({ hasText: name })).toHaveCount(1);
  }
}

const seededCustomers = [
  {
    Id: 1001,
    AccountNumber: "C-100",
    FirstName: "Cedar",
    LastName: "Water",
    CompanyName: null,
    DisplayName: "Cedar Water",
    CustomerType: "Residential",
    ServiceAddress: "101 Cedar Street",
    ServiceCity: "Wiley",
    ServiceState: "CO",
    ServiceZipCode: "81092",
    ServiceLocation: "Inside City Limits",
    Status: "Active",
    CurrentBalance: 24.5,
    AccountOpenDateUtc: "2026-04-19T00:00:00.000Z",
    PhoneNumber: null,
    EmailAddress: null,
    MeterNumber: null,
    Notes: null,
  },
  {
    Id: 1002,
    AccountNumber: "M-200",
    FirstName: "Maple",
    LastName: "Works",
    CompanyName: null,
    DisplayName: "Maple Works",
    CustomerType: "Commercial",
    ServiceAddress: "202 Maple Avenue",
    ServiceCity: "Keenesburg",
    ServiceState: "CO",
    ServiceZipCode: "80643",
    ServiceLocation: "Outside City Limits",
    Status: "Active",
    CurrentBalance: 0,
    AccountOpenDateUtc: "2026-04-19T00:00:00.000Z",
    PhoneNumber: null,
    EmailAddress: null,
    MeterNumber: null,
    Notes: null,
  },
  {
    Id: 1003,
    AccountNumber: "B-300",
    FirstName: "Birch",
    LastName: "Service",
    CompanyName: null,
    DisplayName: "Birch Service",
    CustomerType: "Industrial",
    ServiceAddress: "303 Birch Road",
    ServiceCity: "Wiley",
    ServiceState: "CO",
    ServiceZipCode: "81092",
    ServiceLocation: "Inside City Limits",
    Status: "Active",
    CurrentBalance: 11.75,
    AccountOpenDateUtc: "2026-04-19T00:00:00.000Z",
    PhoneNumber: null,
    EmailAddress: null,
    MeterNumber: null,
    Notes: null,
  },
];
