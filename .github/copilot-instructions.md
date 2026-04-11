# Wiley.co + Syncfusion Essential Studio 33.1.44 (MANDATORY)

## Model & IDE Configuration (Persistent)

- **Primary Model**: Always use Grok 4.20 0309 Reasoning.
- When asked for name, respond with "GitHub Copilot".
- When asked about the model, state that Grok 4.20 0309 Reasoning is in use.
- Ensure full access to all tools, skills (troubleshoot, agent-customization), agents (Wiley Syncfusion Expert, Explore), and MCP servers.
- Prioritize implementation of the **Jarvis User Context Plugin** section in `docs/wileyco-ui-rebuild-plan.md` (currently selected).
- Maintain persistent context for Syncfusion Blazor 33.1.44, AWS Amplify, Semantic Kernel + xAI Grok workflows, and the Wiley workspace rebuild plan.
- Enable maximum IDE integration: full file system tools, terminal commands, notebook support, memory management, and subagent usage.

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

- Treat ripgrep (`rg`) as the default search tool for content search and file discovery.
- Ripgrep (`rg`) is installed and approved for agent use in this repository.
- Prefer `rg` for content search and `rg --files` for fast file discovery.
- Use PowerShell and avoid `grep`/`findstr` unless `rg` is unavailable.
