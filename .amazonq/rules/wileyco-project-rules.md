# Wiley.co Project Rules for Amazon Q (April 11, 2026)

## Core Policy (from copilot-instructions.md and rebuild-plan.md)

- **Mandatory**: Syncfusion Essential Studio 33.1.44 for ALL UI. Use SfBlazor components (SfAIAssistView, SfChart, SfDataGrid, SfSpreadsheet, SfPdfViewer, SfWordProcessor, SfDashboardLayout, SfSplitter, SfToast, SfStepper, SfUploader, SfDialog, SfProgressBar). Prefer #sf_blazor_ui_builder and Syncfusion MCP tools for UI generation.
- **AI Centerpiece**: Jarvis (Grok 4.20 0309 Reasoning via xAI proxy gl43sm/rd3j25/w544vrvb3i, Semantic Kernel 1.74.0). Enhanced UserContextPlugin with financial 'why', operational actions, rate rationale functions. Use AIContextStore, WorkspaceAiAssistantService with enriched prompt for rural utilities/city councils/auditor-impressing transparent financial fluency. XAI_API_KEY from AWS Secrets "Grok" (no hard-coding; use ConfigureXaiSecretAsync). IsSecureJarvisEnabled = true in production.
- **Testing**: >=80% line coverage on all in-process projects (Component, Integration, Widget); E2E by pass rate. Use bUnit for components, coverlet.runsettings, ci.yml gates. Extend ComponentPageTests.cs for 100% on components (WileyWorkspaceBase, JarvisChatPanel, QuickBooksImportPanel, MainLayout, NavMenu, Error). Fix any Trunk lint, use rg for searches.
- **AWS/Amplify**: Amplify app d2ellat1y3ljd9 (us-east-2), main branch, amplify.yml with .NET 9, publish_output. Secrets: SYNCFUSION_LICENSE_KEY, Grok/XAI. Aurora private cluster wiley-co-aurora-db in wiley-co-aurora-vpc (no public access). Thin API host WileyCoWeb.Api for /api/workspace/snapshot. Restrict CORS to Amplify domains. Use env vars only for keys. Cognito for auth where configured.
- **No Hard-Coding**: Keys, URLs, secrets — use env, Secrets Manager, Parameter Store fallback. Local Blazor PDF docs in Blazor Documentation/ preferred over web (see blazor-documentation-index.md).
- **Search/Tooling**: Prefer rg (`rg --files`, content search). PowerShell for terminal. MCP servers for Syncfusion, Microsoft Docs, NuGet, Blazor UI, MSSQL, GitHub. Use Wiley Syncfusion Expert and Explore subagents.
- **Plan Adherence**: Follow docs/wileyco-ui-rebuild-plan.md (100% complete per review, AI centerpiece, no deferred, Phase 6 polish applied). Mark [x] when complete; update Completeness Review with Q findings resolution. Promote shared models/contracts to WileyWidget.Models, thin API, avoid archive src/ for new code.
- **Code Quality**: Typed contracts (WorkspaceBootstrapData, records in Contracts/), structured logging, specific exception handling (no bare catch), caching for FilteredCustomers/projections, realistic projection model (linear + volume growth from historical), server-side reset for chat threads, auditable history. Restrict CORS, remove Console.WriteLine in prod paths.
- **Amazon Q Behavior**: Use Grok 4.20 0309 Reasoning when possible. Prioritize Jarvis User Context Plugin and Syncfusion Blazor. Validate against rebuild-plan before changes. Use MCP tools for Blazor/UI. Run tests/coverage after edits. Create no new files unless necessary; prefer multi_replace for edits. Keep answers short, use backticks for files/symbols, KaTeX for math if needed. Follow Microsoft content policies, no harmful content.

## Validation Rules for Changes

- All UI must use Syncfusion 33.1.44 components and themes (tailwind3/bootstrap5.3/fluent2/material3 per mcp_sf-blazor-mcp_sf_blazor_style).
- AI responses must include numeric results + plain-language rationale per UserContextPlugin.
- Coverage must stay >=80% (run coverlet after edits).
- No hard-coded keys or stale OpenAI references (use XAI_API_KEY/"Grok" secret).
- Prefer local docs/Blazor Documentation and rg for discovery.
- Update plan.md Completeness Review after fixes; mark all valid Q findings as resolved.
- For MCP: Use stdio transport for custom Wiley servers if adding; integrate with existing (Syncfusion, MSSQL, GitHub, Microsoft Docs, Blazor layout).

This file consolidates copilot-instructions.md, wileyco-ui-rebuild-plan.md, AI-BRIEF.md, agents/wiley-syncfusion-expert.agent.md, prompts, docs/\*, and repository memories for Amazon Q to maintain consistency with the Copilot environment.
