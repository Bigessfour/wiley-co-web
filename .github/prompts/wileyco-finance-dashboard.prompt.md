---
description: "Generate a Wiley.co Syncfusion finance dashboard page"
name: "Wiley.co Finance Dashboard"
agent: "agent"
model: "GPT-5 (copilot)"
---

<!-- trunk-ignore(markdownlint/MD041) -->

#sf_blazor_ui_builder Create a responsive Wiley.co finance operations dashboard page for the current Blazor Web App using Syncfusion Essential Studio 33.2.3. Match the existing dark slate and sky-accent visual style used in the app, and preserve the panel-based workflow.

Use SfDashboardLayout as the page shell, with these sections:

- A hero header summarizing fiscal year, status, and last updated information.
- KPI cards for revenue, expenses, variance, and cash flow.
- A primary editable data panel using SfSpreadsheet.
- A secondary analysis panel using SfChart.
- A detailed SfGrid for line items with filtering, sorting, paging, and Excel export.
- Responsive behavior for desktop, tablet, and mobile.
- Clear loading, empty, and error states for every data-driven region.

Keep the implementation Blazor-first and Syncfusion-only for the core UI. Do not use plain HTML tables for the data experience. If you need existing app structure, align the page with the current layout and routing in [Components/Layout/MainLayout.razor](../../Components/Layout/MainLayout.razor) and the current dashboard pattern in [Components/Pages/BudgetDashboard.razor](../../Components/Pages/BudgetDashboard.razor).
