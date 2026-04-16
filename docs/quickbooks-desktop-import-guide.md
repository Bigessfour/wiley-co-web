# QuickBooks Desktop Import Guide

This guide is the current operating procedure for moving QuickBooks Desktop ledger data into Wiley Widget and then into Aurora through the QuickBooks Import panel.

This guide is for the recurring monthly import flow.

- The repo-local `Import Data/` folder is a bootstrap/reference-data source for admin seeding and development.
- It contains QuickBooks-style `.csv` and `.xlsx` files, not XAML files.
- Town-clerk monthly imports should use the QuickBooks Import panel and the API commit path described here.

## What The Import Panel Accepts

The QuickBooks Import panel is a ledger import, not a general workbook loader.

Supported QuickBooks Desktop report families:

- `Transaction List by Date`
- `General Ledger`

Supported file types:

- `.csv`
- `.xlsx`
- `.xls`

Current non-goals for this panel:

- `Profit and Loss` exports
- `Trial Balance` exports
- `Chart of Accounts` exports
- `Customers` exports
- `Vendors` exports

Those files belong to the broader source-data pipeline, not the clerk-facing QuickBooks ledger import panel.

Production policy:

- The App Runner image should not be treated as the storage location for recurring import files.
- Monthly files should be uploaded through the panel, validated in preview, and then committed into Aurora.

## Required Columns

The import code reads the following ledger-style columns when they are present:

- `Date` or `Transaction Date`
- `Type` or `Transaction Type`
- `Num` or `Transaction Number`
- `Name`, `Customer`, or `Vendor`
- `Memo` or `Description`
- `Account`
- `Split`
- `Amount`
- `Balance`
- `Clr`

Minimum practical requirement:

- The file must contain usable `Date` and `Type` values on each transaction row.

General Ledger note:

- QuickBooks Desktop General Ledger exports usually do not include an `Account` column on each detail row.
- Wiley Widget now carries the account name from the report section header and stores the row `Split` value as the split account.

## Report Shape The Import Now Handles

The parser is designed for real QuickBooks Desktop exports, including:

- spacer columns between headers
- report title rows such as `Jan - Dec 26`
- General Ledger account section headers
- General Ledger `Total ...` rows

Those non-transaction rows are ignored during import.

## Town Clerk Workflow

1. In QuickBooks Desktop, open the company file that matches the utility or district you are exporting.
2. Run either `Transaction List by Date` or `General Ledger`.
3. Set the report date range to the period you intend to import.
4. Export the report to Excel or CSV.
5. If you use Excel, keep the QuickBooks export workbook in its original sheet order and save it as `.xlsx` unless you intentionally prefer `.csv`.
6. Do not convert the report into a summary sheet, pivot table, custom layout, or manually edited ledger.
7. In Wiley Widget, open the `QuickBooks Import` panel.
8. Select the correct `Enterprise` and `Fiscal Year` before uploading the file.
9. Upload one file, choose `Analyze file`, and review the preview.
10. Commit the import only after the preview matches the report you intended to load.

## Excel Handling Rules

When the clerk uses Microsoft Excel as the transport format:

- QuickBooks Desktop exports can include a leading `QuickBooks Desktop Export Tips` worksheet before the actual ledger sheet.
- Wiley Widget reads the first worksheet that contains recognizable QuickBooks ledger headers such as `Type`, `Date`, `Account`, `Split`, `Amount`, or `Balance`.
- Keep the exported data worksheet in the workbook. Do not move it behind custom summary sheets or copy the data into a new workbook layout.
- Keep the header row intact.
- Do not insert new rows above the header row.
- Do not delete or rename the ledger columns listed above.
- Do not add formulas, subtotal rows, or handwritten notes that change the transaction area.
- File size must remain under 50 MB.

Safe Excel use:

- export from QuickBooks Desktop
- optionally review the file in Excel
- save once as `.xlsx` if needed
- leave the workbook sheet order alone
- upload that saved file without further edits

## Enterprise And Fiscal Year Selection

The importer does not derive enterprise or fiscal year from the file contents.

- `Enterprise` comes from the panel selection
- `Fiscal Year` comes from the panel selection

That means the clerk must choose the correct enterprise and fiscal year before commit.

If the wrong enterprise is selected, the imported rows will still be committed under the wrong enterprise context.

## Duplicate Protection

Current duplicate blocking is file-based.

- Wiley Widget blocks a commit when the same uploaded file hash has already been imported.
- This protects against importing the exact same file twice.

Operator rule:

- Import the original export file once.
- Do not reopen and resave the same report repeatedly in Excel and assume the system will treat it as the same file.
- If a file was imported under the wrong enterprise or needs to be replaced with corrected data, coordinate with an administrator before re-importing overlapping data.

## Recommended File Naming

Use one clear file per import event. Recommended names:

- `transaction-list-by-date-all-fy2026-water.xlsx`
- `general-ledger-fy2026-water.xlsx`
- `general-ledger-fy2026-wsd.xlsx`

The goal is operational clarity, not strict filename enforcement.

## What To Review In The Preview

Before commit, confirm:

- the row count looks reasonable for the report and date range
- the `Type` and `Date` columns are populated
- the `Account` and `Split` values look correct
- the amounts have the expected sign
- the import is not marked as a duplicate

## Troubleshooting

If the preview is empty or clearly wrong:

- verify the file is a `Transaction List by Date` or `General Ledger` export
- verify the workbook still contains the original exported ledger worksheet
- verify the ledger headers were not renamed or removed
- retry with the original QuickBooks export instead of an edited workbook

If the commit is blocked as a duplicate:

- the exact file was already imported
- do not force a second import of the same file
- use the existing data or coordinate cleanup if a correction is required

If the preview shows the wrong enterprise context:

- stop before commit
- reset the panel
- select the correct enterprise and fiscal year
- upload the file again