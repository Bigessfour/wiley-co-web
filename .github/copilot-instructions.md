# Wiley.co + Syncfusion Essential Studio 33.1.44 (MANDATORY)

## Key Management
- Use environment variables for all Syncfusion keys.
- `SYNCUSION_LICENSE_KEY` is the runtime license key for `Program.cs`.
- `SYNCUSION_API_KEY` is the Syncfusion MCP server key.
- Never hard-code Syncfusion keys in source files or committed config.
- Prefer `#sf_blazor_ui_builder` and the Syncfusion MCP tools when generating UI code.

## Policy
- Follow the repository root [copilot-instructions.md](../copilot-instructions.md) as the canonical project policy.
- Use Syncfusion Essential Studio 33.1.44 for all UI components.
- Keep all UI suggestions in Syncfusion Blazor components, especially dashboards, charts, spreadsheets, and PDF/DOCX workflows.
- Prefer Syncfusion.Blazor.SfPdfViewer and Syncfusion.Blazor.WordProcessor for document workflows.