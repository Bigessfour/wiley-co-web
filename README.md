# Wiley.co Web

Wiley.co Web is a production-style municipal utility rate-study workspace built with Blazor WebAssembly, an ASP.NET Core API, Syncfusion UI, and AWS hosting.

- Live app: https://main.d2ellat1y3ljd9.amplifyapp.com
- Focus: break-even analysis, scenario planning, QuickBooks imports, customer review, document exports, and AI-assisted decision support

## What this project does

This workspace helps a utility team move rate-study work out of scattered spreadsheets and into a single browser-based workflow.

Core capabilities:

- break-even rate analysis for utility enterprises
- scenario planning with saved snapshots
- QuickBooks Desktop import and validation flows
- customer review and export-ready filtering
- Excel and PDF document generation
- server-backed decision support and Jarvis guidance

## Why I built it

I built Wiley.co Web to modernize a real municipal-finance workflow and to practice shipping a full-stack application with real deployment, persistence, testing, and reporting requirements.

This repo is also my strongest example of working across product design, frontend UI, backend APIs, AWS infrastructure, document workflows, and automated test coverage in one project.

## Tech stack

- **Frontend:** Blazor WebAssembly, C#, Tailwind-style utility classes, Syncfusion Blazor 33.1.44
- **Backend:** ASP.NET Core API, shared contracts and services
- **Data:** PostgreSQL / Aurora, reference-data import flows, scenario persistence
- **Cloud:** AWS Amplify, AWS App Runner, AWS Secrets Manager
- **Testing:** xUnit, bUnit, Playwright

## Local setup

### Prerequisites

- .NET SDK `9.0.313` (pinned in `global.json`)
- Node.js 18+
- A valid `SYNCFUSION_LICENSE_KEY`

### Frontend

```bash
dotnet run --project WileyCoWeb.csproj --launch-profile http --urls http://localhost:5230
```

### API

```bash
dotnet run --project WileyCoWeb.Api/WileyCoWeb.Api.csproj --launch-profile WileyCoWeb.Api --urls http://127.0.0.1:5231
```

### Optional local config

Create an ignored file at `appsettings.Syncfusion.local.json`:

```json
{ "SyncfusionLicenseKey": "<your-license-key>" }
```

Create an ignored file at `appsettings.Workspace.local.json` if you want the client to point at a separate local API host:

```json
{ "WorkspaceApiBaseAddress": "http://localhost:5231" }
```

## Testing

```bash
dotnet test tests/WileyWidget.Tests/WileyWidget.Tests.csproj
dotnet test tests/WileyCoWeb.ComponentTests/WileyCoWeb.ComponentTests.csproj
npm install
npm run playwright:test:progress
```

When `WILEYCO_E2E_BASE_URL` is not set, Playwright uses the local default app at `http://localhost:5230` and API at `http://127.0.0.1:5231`.

## Current status

This project is active and already demonstrates:

- deployed Blazor + API architecture
- municipal finance workflows beyond a demo CRUD app
- browser automation and component-level test coverage
- export and document workflows
- AI-assisted analysis connected to the server-side knowledge layer

## Next steps

- deepen the Jarvis user-context workflow
- continue polishing council-ready reporting and exports
- expand browser proof coverage for critical workspace routes

## Project docs

- UI rebuild roadmap: [docs/wileyco-ui-rebuild-plan.md](docs/wileyco-ui-rebuild-plan.md)
- AWS server-side closure plan: [docs/aws-server-side-closure-plan.md](docs/aws-server-side-closure-plan.md)
- QuickBooks import guide: [docs/quickbooks-desktop-import-guide.md](docs/quickbooks-desktop-import-guide.md)
- Aurora layout: [docs/aws-aurora-private-layout.md](docs/aws-aurora-private-layout.md)
- Aurora reset runbook: [docs/aurora-postgresql-reset-runbook.md](docs/aurora-postgresql-reset-runbook.md)

## Notes

- Keep Syncfusion keys in environment variables or ignored local config, never in committed source.
- The browser client talks to the thin API host; xAI/Grok access stays server-side.
- If you are curating pinned repositories on GitHub, this repo is best presented as a flagship full-stack project rather than a generic experiment.
