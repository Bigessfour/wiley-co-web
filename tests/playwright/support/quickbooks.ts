export function createQuickBooksCsv() {
  return [
    "Date,Type,Num,Name,Memo,Account,Split,Amount,Balance,Clr",
    "01/01/2026,Invoice,1001,Town of Wiley,Water Billing,Water Revenue,Accounts Receivable,125.00,125.00,C",
    "01/02/2026,Payment,1002,Town of Wiley,Payment Received,Accounts Receivable,Water Revenue,-125.00,0.00,C",
  ].join("\n");
}

export function createQuickBooksPreviewResponse() {
  return {
    fileName: "quickbooks-sample.csv",
    fileHash: "playwright-preview-hash",
    selectedEnterprise: "Water",
    selectedFiscalYear: 2026,
    totalRows: 2,
    duplicateRows: 0,
    isDuplicate: false,
    statusMessage: "Preview loaded for 2 QuickBooks rows.",
    rows: [
      {
        rowNumber: 1,
        entryDate: "01/01/2026",
        entryType: "Invoice",
        transactionNumber: "1001",
        name: "Town of Wiley",
        memo: "Water Billing",
        accountName: "Water Revenue",
        splitAccount: "Accounts Receivable",
        amount: 125.0,
        runningBalance: 125.0,
        clearedFlag: "C",
        isDuplicate: false,
      },
      {
        rowNumber: 2,
        entryDate: "01/02/2026",
        entryType: "Payment",
        transactionNumber: "1002",
        name: "Town of Wiley",
        memo: "Payment Received",
        accountName: "Accounts Receivable",
        splitAccount: "Water Revenue",
        amount: -125.0,
        runningBalance: 0.0,
        clearedFlag: "C",
        isDuplicate: false,
      },
    ],
  };
}
