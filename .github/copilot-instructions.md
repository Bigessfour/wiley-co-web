# Wiley.co + Syncfusion Essential Studio 33.1.44 (MANDATORY)

## Key Management

- Use environment variables for all Syncfusion keys.
- `SYNCUSION_LICENSE_KEY` is the runtime license key for `Program.cs`.
- `SYNCUSION_API_KEY` is the Syncfusion MCP server key.
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

## Policy

- Follow the repository root [copilot-instructions.md](../copilot-instructions.md) as the canonical project policy.
- Use Syncfusion Essential Studio 33.1.44 for all UI components.
- Keep all UI suggestions in Syncfusion Blazor components, especially dashboards, charts, spreadsheets, and PDF/DOCX workflows.
- Prefer Syncfusion.Blazor.SfPdfViewer and Syncfusion.Blazor.WordProcessor for document workflows.

## Local Blazor Documentation

- Treat [Blazor Documentation](../Blazor%20Documentation) as the local authoritative reference for Blazor and Syncfusion questions.
- Prefer the local PDF set before web sources when answering questions about the Blazor UI stack.
- Use [docs/blazor-documentation-index.md](../docs/blazor-documentation-index.md) as the navigation summary for the PDF reference set.
- Keep the PDF folder out of source control; it is a local reference library for Copilot and maintainers.

## Agent Search Tooling

- Ripgrep (`rg`) is installed and approved for agent use in this repository.
- Prefer `rg` for content search and `rg --files` for fast file discovery.
- Use PowerShell and avoid `grep`/`findstr` unless `rg` is unavailable.
