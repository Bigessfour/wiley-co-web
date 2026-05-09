# Wiley.co Project Instructions

This file supplements the repository root copilot-instructions.md and is the always-on instruction source for the Wiley.co Blazor project. Follow it on every prompt unless the user explicitly overrides it.

## Key Management

- Use environment variables for all Syncfusion keys.
- `SYNCFUSION_LICENSE_KEY` is the runtime license key for `Program.cs`.
- `SYNCFUSION_API_KEY` is the Syncfusion MCP server key.
- Never hard-code Syncfusion keys in source files or committed config.
- Prefer `#sf_blazor_ui_builder` and the Syncfusion MCP tools when generating UI code.

## AWS Amplify CLI Reference

- `amplify init` initializes a new Amplify project and must be run from an empty directory.
- `amplify add hosting` adds hosting resources to an Amplify backend project.
- `amplify configure hosting` configures hosting resources such as S3, CloudFront, and publish ignore rules.
- `amplify publish` builds and publishes the backend and frontend.
- `amplify pull <app-id>` pulls an existing Amplify app into a local workspace.
- `amplify push` deploys backend changes from the local project.
- `amplify status` shows the local and cloud backend status.
- `aws amplify create-app` creates an Amplify hosting app from the AWS CLI.
- `aws amplify create-branch` creates the production or preview branch for a hosted app.
- `aws amplify update-app` updates app-level hosting settings and environment variables.
- `aws amplify list-apps` and `aws amplify list-branches` are useful for confirming hosted app state.

## AWS MCP Server Policy

- The AWS MCP servers configured in `.vscode/mcp.json` are mandatory for AWS-related work in this repository.
- Use `aws-knowledge-mcp` first for AWS processes, operational steps, architecture guidance, best practices, Amplify, App Runner, Aurora, IAM, networking, CDK, CloudFormation, and Well-Architected guidance.
- Use `aws-documentation-mcp` to search and read exact AWS documentation, API references, CLI behavior, parameters, quotas, prerequisites, and service-specific details.
- Do not answer AWS process or setup questions from memory alone when an AWS MCP server is available.
- If an AWS MCP server is unavailable, state that explicitly before falling back to official AWS documentation or checked-in repo docs.

## Core Rules

- This is a Blazor WebAssembly application for Wiley.co, a simple responsive financial dashboard website.
- Syncfusion Essential Studio 33.2.3 is mandatory for all UI components.
- Never use plain HTML tables, MudBlazor, or built-in Blazor components for grids, charts, dashboards, spreadsheets, or PDF viewing.
- Always use Syncfusion Blazor NuGet packages for UI work, including Syncfusion.Blazor, Syncfusion.Blazor.Spreadsheet, Syncfusion.Blazor.PdfViewer or SmartPdfViewer, and Syncfusion.Blazor.DocumentEditor when relevant.
- Always use Syncfusion Blazor NuGet packages for UI work, including Syncfusion.Blazor, Syncfusion.Blazor.Spreadsheet, Syncfusion.Blazor.SfPdfViewer or Syncfusion.Blazor.SfSmartPdfViewer, and Syncfusion.Blazor.WordProcessor when relevant.
- Register the Syncfusion license in Program.cs by reading `SYNCFUSION_LICENSE_KEY` from the environment and passing it to `SyncfusionLicenseProvider.RegisterLicense`.

## Architecture And UI Standards

- Use SfDashboardLayout for dashboard composition and panel layout.
- Prefer SfDataGrid or SfSpreadsheet for tabular data, reports, budgets, and editable finance views.
- Use SfChart for visualizations, especially financial, waterfall, stock, and trend charts.
- Keep the app responsive by default using Syncfusion responsive behavior or utility CSS. Do not introduce fixed desktop-only sizing patterns.
- For exports, use Syncfusion server-side libraries such as Pdf.Net.Core, DocIO.Net.Core, and XlsIO.Net.Core through a minimal backend API or AWS Lambda.
- Semantic Kernel and Grok integration belongs in the backend, with secrets stored in AWS Secrets Manager.
- Use an OpenAI-compatible connector when integrating Grok.

## Coding Style

- Use C# 12 or newer features where they improve clarity.
- Prefer async and await throughout.
- Keep Razor components clean, reusable, and purpose-specific.
- Add explicit loading, empty, and error states for async UI flows.
- For reports, use SfPdfViewer for preview and server-side export for the final artifact.
- Never suggest FastReport, plain JavaScript charting libraries, or non-Syncfusion replacements for core UI work.

## Working In This Repo

- When suggesting code, always show the full @using statements and the Syncfusion component markup first.
- Prefer small, focused changes that preserve the existing project structure.
- Do not change package versions or architecture unless the user asks or the change is required to satisfy the task.

## AWS Deployment

- Build with dotnet publish for Amplify.
- Use the provided amplify.yml for CI/CD, including build, test, and publish to wwwroot.
- Keep the published payload small by using individual Syncfusion NuGet packages where possible.

## Debugging And IDE Setup

- Preserve the Blazor debug proxy configuration in launchSettings.json by keeping inspectUri on each launch profile.
- Prefer a VS Code launch configuration for Blazor WebAssembly debugging when browser-level debugging is needed.
- Keep the C# Dev Kit and Blazor WebAssembly companion extension recommended in the workspace settings.

## Syncfusion MCP Server And Agentic UI Builder

- Use the Syncfusion MCP Server in workspace mode for this project.
- Configure the server as `sf-blazor-mcp` with `npx -y @syncfusion/blazor-assistant@latest`.
- Pass the MCP key from `SYNCFUSION_API_KEY`.
- Use `#sf_blazor_ui_builder` for end-to-end UI generation tasks.
- Use `#sf_blazor_layout` for responsive page structure, `#sf_blazor_component` for component API guidance, and `#sf_blazor_style` for theming and icon setup.
- If a natural-language prompt is used instead, include the word Syncfusion so the MCP server can route the request correctly.
- Keep the active MCP tool list minimal to avoid tool-selection ambiguity.

## Agent Search Tooling

- Treat ripgrep (`rg`) as the default search tool for content search and file discovery.
- Ripgrep (`rg`) is installed and approved for agent use in this repository.
- Prefer `rg` for content search and `rg --files` for fast file discovery.
- Use PowerShell and avoid `grep`/`findstr` unless `rg` is unavailable.

## Maintainability Gate

- No newly added runtime code may ship with a CRAP score greater than 5.
- Run `python .\Scripts\find_crap_code.py --threshold 5 --new-methods-only --fail-on-results --top 100` before handoff when a task adds or changes runtime code.
- Use the dedicated VS Code task for this gate when you want a one-click workspace check.

## Recommended Next Steps When Needed

- If the app is still using interactive server rendering, consider converting it to interactive WebAssembly so the Blazor WASM tooling is exercised fully.
- Add or update .vscode/launch.json when you need one-click browser debugging for the app.
- If the project layout changes, keep the Syncfusion guidance and debug settings aligned with the new startup project.
