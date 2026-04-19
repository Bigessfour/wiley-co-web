# Wiley.co Canonical Copilot Instructions

This file is the repository-level canonical policy for AI agents working in this workspace. Supplemental instruction files in .github/, .vscode/, and .amazonq/ may add host-specific detail, but they must not weaken these rules.

## Core Stack

- This is a Wiley.co Blazor WebAssembly workspace.
- Syncfusion Essential Studio 33.1.44 is mandatory for all UI work.
- Keep recommendations aligned with the current AWS stack: Amplify, App Runner, Aurora PostgreSQL, AWS Secrets Manager, and the thin WileyCoWeb.Api host.
- Prioritize the Jarvis User Context Plugin section in docs/wileyco-ui-rebuild-plan.md when the task overlaps the rebuild plan.

## Key Management

- Use environment variables for all Syncfusion keys.
- SYNCFUSION_LICENSE_KEY is the runtime license key for Program.cs.
- SYNCFUSION_API_KEY is the Syncfusion MCP server key.
- Never hard-code Syncfusion keys in source files or committed config.

## AWS MCP Policy

- The AWS MCP servers configured in .vscode/mcp.json are mandatory for AWS-related work in this repository.
- Use aws-knowledge-mcp first for AWS processes, operational steps, architecture guidance, best practices, Amplify, App Runner, Aurora, IAM, networking, CDK, CloudFormation, and Well-Architected guidance.
- Use aws-documentation-mcp to search and read exact AWS documentation, API references, CLI behavior, parameters, quotas, prerequisites, and service-specific details.
- Do not answer AWS process or setup questions from memory alone when an AWS MCP server is available.
- If an AWS MCP server is unavailable, say so explicitly and then fall back to official AWS documentation plus checked-in repo docs.

## AWS Amplify CLI Reference

- amplify init initializes a new Amplify project and must be run from an empty directory.
- amplify add hosting adds hosting resources to an Amplify backend project.
- amplify configure hosting configures hosting resources such as S3, CloudFront, and publish ignore rules.
- amplify publish builds and publishes the backend and frontend.
- amplify pull <app-id> pulls an existing Amplify app into a local workspace.
- amplify push deploys backend changes from the local project.
- amplify status shows the local and cloud backend status.
- aws amplify create-app creates an Amplify hosting app from the AWS CLI.
- aws amplify create-branch creates the production or preview branch for a hosted app.
- aws amplify update-app updates app-level hosting settings and environment variables.
- aws amplify list-apps and aws amplify list-branches are useful for confirming hosted app state.

## Syncfusion Standards

- Use Syncfusion Blazor components for dashboards, grids, charts, spreadsheets, and document workflows.
- Prefer SfDashboardLayout for dashboard composition, SfDataGrid or SfSpreadsheet for finance tables, SfChart for visualizations, SfPdfViewer or SfSmartPdfViewer for PDF review, and WordProcessor for DOCX workflows.
- Do not replace core Syncfusion UI with plain HTML tables, MudBlazor, or non-Syncfusion charting libraries.

## Local Documentation

- Treat Blazor Documentation/ as the local authoritative reference for Blazor and Syncfusion questions.
- Prefer docs/blazor-documentation-index.md as the navigation summary for the local PDF reference set.

## Search Tooling

- Treat ripgrep (rg) as the default search tool for content search and file discovery.
- Prefer rg for content search and rg --files for fast file discovery.
- Use PowerShell and avoid grep/findstr unless rg is unavailable.

## Maintainability Gate

- No newly added runtime code may ship with a CRAP score greater than 5.
- Validate this rule with `python .\Scripts\find_crap_code.py --threshold 5 --new-methods-only --fail-on-results --top 100` before handoff when the task adds or changes runtime code.
- Treat the gate as applying to active runtime code only, using the scanner's built-in scope rules for `Components`, `Services`, `WileyCoWeb.Api`, `State`, and root-level C# files.
