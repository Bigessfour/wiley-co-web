import { expect, test } from "@playwright/test";
import { gotoWorkspacePanel } from "./support/workspace";

test.describe("Unique Interaction Proof", () => {
  test("Customer viewer editor proof covers add and edit dialog behavior", async ({
    page,
  }) => {
    const seededCustomer = {
      Id: 1,
      AccountNumber: "E2E-SEED-001",
      FirstName: "Seeded",
      LastName: "Customer",
      CompanyName: null,
      DisplayName: "Seeded Customer",
      CustomerType: "Residential",
      ServiceAddress: "101 Seeded Ave",
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

    const customers: Array<{
      Id: number;
      AccountNumber: string;
      FirstName: string;
      LastName: string;
      CompanyName: string | null;
      DisplayName: string;
      CustomerType: string;
      ServiceAddress: string;
      ServiceCity: string;
      ServiceState: string;
      ServiceZipCode: string;
      ServiceLocation: string;
      Status: string;
      CurrentBalance: number;
      AccountOpenDateUtc: string;
      PhoneNumber: string | null;
      EmailAddress: string | null;
      MeterNumber: string | null;
      Notes: string | null;
    }> = [seededCustomer];

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
        const customerIndex = customers.findIndex(
          (customer) => customer.Id === customerId,
        );
        if (customerIndex >= 0) {
          customers[customerIndex] = record;
        }

        await route.fulfill({
          status: 200,
          json: record,
        });

        return;
      }

      if (method === "DELETE") {
        const customerIndex = customers.findIndex(
          (customer) => customer.Id === customerId,
        );
        if (customerIndex >= 0) {
          customers.splice(customerIndex, 1);
        }

        await route.fulfill({
          status: 204,
          body: "",
        });

        return;
      }

      await route.fulfill({
        status: 405,
        contentType: "text/plain",
        body: "Method not allowed",
      });
    });

    // 1. Open /wiley-workspace/customers and launch the add-customer dialog.
    await gotoWorkspacePanel(page, "/wiley-workspace/customers");

    const directoryStatus = page.locator("#customer-directory-status");
    const panel = page.locator("#customer-viewer-panel");
    const grid = page.locator("#customer-directory-grid");

    await expect(panel).toBeVisible();
    await expect(grid).toBeVisible();

    await page.locator("#add-customer-button").click();
    const createDialog = page.getByRole("dialog", {
      name: "Add Utility Customer",
    });

    await expect(createDialog).toBeVisible();
    await expect(
      createDialog.locator("#customer-editor-account-number"),
    ).toBeVisible();
    await expect(
      createDialog.locator("#customer-editor-first-name"),
    ).toBeVisible();
    await expect(
      createDialog.locator("#customer-editor-last-name"),
    ).toBeVisible();
    await expect(
      createDialog.locator("#customer-editor-company-name"),
    ).toBeVisible();
    await expect(
      createDialog.locator("#customer-editor-service-address"),
    ).toBeVisible();
    await expect(
      createDialog.locator("#customer-editor-service-city"),
    ).toBeVisible();
    await expect(
      createDialog.locator("#customer-editor-service-state"),
    ).toBeVisible();
    await expect(
      createDialog.locator("#customer-editor-service-zip-code"),
    ).toBeVisible();

    await createDialog
      .locator("#customer-editor-account-number")
      .fill("E2E-NEW-001");
    await createDialog.locator("#customer-editor-first-name").fill("Proof");
    await createDialog.locator("#customer-editor-last-name").fill("Customer");
    await createDialog
      .locator("#customer-editor-service-address")
      .fill("123 E2E Ave");
    await createDialog.locator("#customer-editor-service-city").fill("Wiley");
    await createDialog.locator("#customer-editor-service-state").fill("CO");
    await createDialog
      .locator("#customer-editor-service-zip-code")
      .fill("81092");

    await expect(
      createDialog.locator("#customer-editor-save-button"),
    ).toBeEnabled();

    await createDialog.getByRole("button", { name: "Cancel" }).click();
    await expect(createDialog).toBeHidden();

    // 2. Reopen the seeded customer in edit mode and verify the dialog is prefilled.
    const createdRow = page
      .locator("#customer-viewer-panel tr.e-row")
      .filter({ hasText: seededCustomer.AccountNumber })
      .first();

    await expect(createdRow).toContainText(seededCustomer.DisplayName);
    await createdRow.getByRole("button", { name: "Edit" }).click();

    const editDialog = page.getByRole("dialog", {
      name: "Edit Utility Customer",
    });
    await expect(editDialog).toBeVisible();
    await expect(
      editDialog.locator("#customer-editor-account-number"),
    ).toHaveValue(seededCustomer.AccountNumber);
    await expect(editDialog.locator("#customer-editor-first-name")).toHaveValue(
      seededCustomer.FirstName,
    );
    await expect(editDialog.locator("#customer-editor-last-name")).toHaveValue(
      seededCustomer.LastName,
    );
    await expect(
      editDialog.locator("#customer-editor-service-city"),
    ).toHaveValue(seededCustomer.ServiceCity);
    await expect(
      editDialog.locator("#customer-editor-service-state"),
    ).toHaveValue(seededCustomer.ServiceState);
    await expect(
      editDialog.locator("#customer-editor-service-zip-code"),
    ).toHaveValue(seededCustomer.ServiceZipCode);

    await editDialog.getByRole("button", { name: "Cancel" }).click();
    await expect(editDialog).toBeHidden();

    await expect(directoryStatus).toContainText(/loaded 1 utility customers/i);
  });
});
