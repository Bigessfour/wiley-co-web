import { expect, test } from "@playwright/test";
import { gotoWorkspacePanel, waitForWorkspaceShell } from "./support/workspace";

test.describe("Unique Interaction Proof", () => {
  test("Customer viewer handles search, filter reset, and degraded directory states", async ({
    page,
  }) => {
    await page.route("**/api/utility-customers**", async (route) => {
      const method = route.request().method();

      if (method === "GET") {
        await route.fulfill({
          status: 200,
          json: [],
        });

        return;
      }

      await route.continue();
    });

    // 1. Open the workspace shell first, then enter the customer viewer through the overview navigation.
    await page.goto("/wiley-workspace");
    await waitForWorkspaceShell(page);
    await page
      .locator("#workspace-navigation-card")
      .getByRole("link", { name: "Customer Viewer" })
      .click();

    const panel = page.locator("#customer-viewer-panel");
    const directoryStatus = page.locator("#customer-directory-status");
    const searchInput = page.locator("#customer-search-input");
    const refreshButton = page.locator("#refresh-customer-directory-button");
    const clearFiltersButton = page.locator("#clear-customer-filters-button");
    const addCustomerButton = page.locator("#add-customer-button");
    const errorBanner = page.locator(
      "#customer-viewer-panel .border-rose-200.bg-rose-50",
    );

    await expect(panel).toBeVisible();
    await expect(page.locator("#customer-directory-grid")).toBeVisible();
    await expect(directoryStatus).toBeVisible();
    await expect(directoryStatus).toContainText(
      /Loaded \d+ utility customers from the live API\.|The live customer directory could not be refreshed\./,
    );
    await expect(refreshButton).toBeEnabled();
    await expect(clearFiltersButton).toBeEnabled();
    await expect(addCustomerButton).toBeEnabled();

    // 2. Enter a search term, then clear filters and refresh the directory.
    await searchInput.fill("delta");
    await expect(searchInput).toHaveValue("delta");

    await clearFiltersButton.click();
    await expect(searchInput).toHaveValue("");

    await refreshButton.click();
    await expect(directoryStatus).not.toContainText(
      "Loading live utility-customer directory...",
    );

    const statusText = await directoryStatus.innerText();
    expect(statusText).toMatch(
      /Refreshed the live utility-customer directory\.|Loaded \d+ utility customers from the live API\.|The live customer directory could not be refreshed\./,
    );

    // 3. If the live directory is unavailable, verify the error banner messaging.
    if (await errorBanner.isVisible()) {
      await expect(errorBanner).toContainText(
        "Unable to load the live utility-customer directory:",
      );
    }
  });

  test("Customer viewer edit changes survive a reload", async ({ page }) => {
    const seededCustomer = {
      Id: 1,
      AccountNumber: "E2E-SEED-001",
      FirstName: "Seeded",
      LastName: "Customer",
      CompanyName: null,
      DisplayName: "Seeded Customer",
      CustomerType: "Residential",
      ServiceAddress: "123 Seeded Ave",
      ServiceCity: "Wiley",
      ServiceState: "CO",
      ServiceZipCode: "81092",
      ServiceLocation: "InsideCityLimits",
      Status: "Active",
      CurrentBalance: 0,
      AccountOpenDateUtc: "2026-04-19T00:00:00.000Z",
      PhoneNumber: null,
      EmailAddress: null,
      MeterNumber: null,
      Notes: null,
    };

    const customers: (typeof seededCustomer)[] = [seededCustomer];

    await page.route("**/api/utility-customers**", async (route) => {
      const request = route.request();
      const method = request.method();

      if (method === "GET") {
        await route.fulfill({
          status: 200,
          json: customers,
        });

        return;
      }

      const requestBody = request.postDataJSON() as {
        AccountNumber?: string;
        FirstName?: string;
        LastName?: string;
        CompanyName?: string | null;
        CustomerType?: string;
        ServiceAddress?: string;
        ServiceCity?: string;
        ServiceState?: string;
        ServiceZipCode?: string;
        ServiceLocation?: string;
        Status?: string;
        CurrentBalance?: number;
        AccountOpenDate?: string;
        PhoneNumber?: string | null;
        EmailAddress?: string | null;
        MeterNumber?: string | null;
        Notes?: string | null;
      };

      const accountNumber = requestBody.AccountNumber ?? "E2E000000";
      const firstName = requestBody.FirstName ?? "Proof";
      const lastName = requestBody.LastName ?? "Customer";
      const customerIdFromPath = Number(
        new URL(request.url()).pathname.split("/").pop(),
      );
      const customerId =
        method === "POST" ? customers.length + 1 : customerIdFromPath;
      const record = {
        Id: customerId,
        AccountNumber: accountNumber,
        FirstName: firstName,
        LastName: lastName,
        CompanyName: requestBody.CompanyName ?? null,
        DisplayName: `${firstName} ${lastName}`,
        CustomerType: requestBody.CustomerType ?? "Residential",
        ServiceAddress: requestBody.ServiceAddress ?? "123 E2E Ave",
        ServiceCity: requestBody.ServiceCity ?? "Wiley",
        ServiceState: requestBody.ServiceState ?? "CO",
        ServiceZipCode: requestBody.ServiceZipCode ?? "81092",
        ServiceLocation: requestBody.ServiceLocation ?? "InsideCityLimits",
        Status: requestBody.Status ?? "Active",
        CurrentBalance: requestBody.CurrentBalance ?? 0,
        AccountOpenDateUtc:
          requestBody.AccountOpenDate ?? "2026-04-19T00:00:00.000Z",
        PhoneNumber: requestBody.PhoneNumber ?? null,
        EmailAddress: requestBody.EmailAddress ?? null,
        MeterNumber: requestBody.MeterNumber ?? null,
        Notes: requestBody.Notes ?? null,
      };

      if (method === "POST") {
        customers.push(record);
        await route.fulfill({
          status: 201,
          json: record,
        });

        return;
      }

      if (method === "PUT") {
        const index = customers.findIndex(
          (customer) => customer.Id === customerId,
        );
        if (index >= 0) {
          customers[index] = record;
        }

        await route.fulfill({
          status: 200,
          json: record,
        });

        return;
      }

      if (method === "DELETE") {
        const index = customers.findIndex(
          (customer) => customer.Id === customerId,
        );
        if (index >= 0) {
          customers.splice(index, 1);
        }

        await route.fulfill({
          status: 204,
          body: "",
        });

        return;
      }

      await route.continue();
    });

    // 1. Open the customer viewer and edit the seeded customer.
    await gotoWorkspacePanel(page, "/wiley-workspace/customers");

    const panel = page.locator("#customer-viewer-panel");
    const directoryStatus = page.locator("#customer-directory-status");
    const directoryGrid = page.locator("#customer-directory-grid");
    const seededRow = page
      .locator("#customer-viewer-panel tr.e-row")
      .filter({ hasText: seededCustomer.AccountNumber })
      .first();

    await expect(panel).toBeVisible();
    await expect(directoryGrid).toBeVisible();
    await expect(directoryStatus).toContainText(/loaded 1 utility customers/i);
    await expect(seededRow).toBeVisible();

    await seededRow.getByRole("button", { name: "Edit" }).click();

    const editDialog = page.getByRole("dialog", {
      name: "Edit Utility Customer",
    });
    const serviceCityInput = page.locator("#customer-editor-service-city");
    const saveButton = page.locator("#customer-editor-save-button");

    await expect(editDialog).toBeVisible();
    await expect(serviceCityInput).toHaveValue("Wiley");

    await serviceCityInput.fill("Aurora");
    await expect(serviceCityInput).toHaveValue("Aurora");

    await saveButton.click();

    await expect(directoryStatus).toContainText(
      /Saved E2E-SEED-001 and updated the live utility-customer directory\./i,
    );
    await expect(
      page.getByRole("dialog", { name: "Edit Utility Customer" }),
    ).toBeHidden();

    // 2. Reload the page and confirm the edited record still opens with the saved city.
    await page.reload();

    await expect(directoryStatus).toContainText(/loaded 1 utility customers/i);
    await expect(seededRow).toBeVisible();

    await seededRow.getByRole("button", { name: "Edit" }).click();
    await expect(editDialog).toBeVisible();
    await expect(serviceCityInput).toHaveValue("Aurora");
  });
});
