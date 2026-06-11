# Wiley.co Canonical Copilot Instructions

This file is the repository-level canonical policy for AI agents working in this workspace. Supplemental instruction files in .github/, .vscode/, and .amazonq/ may add host-specific detail, but they must not weaken these rules.

## Core Stack

- This is a Wiley.co Blazor WebAssembly workspace (pure local .NET only).
- Syncfusion Essential Studio 33.2.3 is mandatory for all UI work.
- Local dev only: start-local.sh + docker-compose.dev.yml (postgres) + direct xAI for Jarvis. No Amplify, App Runner, Aurora, or AWS deployment in this repository.
- Prioritize the core import → categorize/allocate overhead → analyze (net contribution / vampire) → viz (4-enterprise DataDashboard + BreakEven + Rates) → Jarvis loop. Non-core panels (affordability, debt, capital, scenario, reserve, trends, budget, customers viewer) and all AWS remnants are being surgically removed.

## Key Management

- Use environment variables for all Syncfusion keys.
- SYNCFUSION_LICENSE_KEY is the runtime license key for Program.cs.
- SYNCFUSION_API_KEY is the Syncfusion MCP server key.
- For local MCP on macOS, Scripts/mcp-sf-blazor-assistant.sh reads the MCP API key from Keychain (service com.wileyco.syncfusion.blazor-mcp); see that script and .cursor/mcp.json.
- Never hard-code Syncfusion keys in source files or committed config.

## MCP Tooling (allowed)

- MCP servers (github, chrome-devtools, @syncfusion/blazor-assistant via mcp-sf-blazor-assistant.sh, playwright-mcp, mermaid, filesystem) are explicitly allowed and recommended for audits, UI control inspection (Syncfusion params), verification, diagram generation, and repo queries during development and cull work.
- Use them to supplement (never replace) direct source reads (read_file), searches (grep), builds, and full Playwright E2E runs.
- AWS-specific MCP servers and policies have been removed as part of the local-only architecture cull. All recommendations must target the pure local stack (no cloud references).

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

## RAG for Agents (mandatory first step)

Before any planning, editing, or review that touches the core loop or established patterns, run the local RAG query tool and cite the results:

```bash
python -m Scripts.rag.query "AGENTS.md + the area you are changing (e.g. overhead, import duplicate guard, Jarvis grounding, workspace shell)"
# or: Scripts/rag/rag.sh query "..."
```

See `docs/rag-for-agents.md` and the updated AGENTS.md section on RAG. This is how we keep the pure local import→categorize→allocate→analyze→viz→Jarvis codebase drift-free. Combine RAG retrieval with direct file reads, grep, builds, and HighRisk/Playwright verification.
