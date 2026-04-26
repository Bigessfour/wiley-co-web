# wiley-co-web

Wiley Widget for AWS

## Amplify Hosting

This app is hosted on AWS Amplify in `us-east-2`.

- App name: `wiley-co-web`
- App id: `d2ellat1y3ljd9`
- Production branch: `main`
- Default domain: `d2ellat1y3ljd9.amplifyapp.com`

### Syncfusion License In Amplify

AWS Amplify Gen 1 stores encrypted hosting secrets in AWS Systems Manager Parameter Store, not AWS Secrets Manager. The AWS docs specify that Amplify environment secrets are exposed to the build as `process.env.secrets`.

For this app, production Amplify builds should resolve the key from the Amplify Gen 1 environment-secret path `/amplify/d2ellat1y3ljd9/main/SYNCFUSION_LICENSE_KEY` before `dotnet publish` runs.

The build also supports these non-production or fallback sources:

1. AWS Secrets Manager, using the secret name `SYNCFUSION_LICENSE_KEY`.
2. A local ignored file named `appsettings.Syncfusion.local.json` in the repository root.

Do not store `SYNCFUSION_LICENSE_KEY` in Amplify app-level or branch environment variables.

Amplify production builds now fail fast if `SYNCFUSION_LICENSE_KEY` is missing after those lookup steps. This prevents deploying a client bundle that would show the Syncfusion license popup at runtime.

Amplify builds now also validate Cognito secret consistency. If any of `COGNITO_USER_POOL_ID`, `COGNITO_APP_CLIENT_ID`, or `COGNITO_REGION` is set, all three must be present or the build fails.

If you are using AWS Secrets Manager:

1. Store the secret either as a raw string or as JSON containing `SYNCFUSION_LICENSE_KEY` or `SyncfusionLicenseKey`.
2. Ensure the Amplify build role can call `secretsmanager:GetSecretValue` for the secret named `SYNCFUSION_LICENSE_KEY`.
3. Redeploy the Amplify branch.

If you are using Amplify Gen 1 environment secrets instead:

1. In Systems Manager Parameter Store, create a `SecureString` parameter named `/amplify/d2ellat1y3ljd9/main/SYNCFUSION_LICENSE_KEY`.
2. Use the default AWS KMS key for the account so Amplify can decrypt it.
3. Redeploy the Amplify branch.

If you are enabling hosted auth:

1. Add `COGNITO_USER_POOL_ID`, `COGNITO_APP_CLIENT_ID`, and `COGNITO_REGION` as Amplify environment secrets.
2. Keep them in sync per branch; partial values now fail the build to prevent inconsistent auth configuration in production.

If you are working locally on macOS and user secrets are not being surfaced reliably, create an ignored file named `appsettings.Syncfusion.local.json` in the repository root with this shape:

```json
{ "SyncfusionLicenseKey": "<your-license-key>" }
```

The build copies or generates that file into `wwwroot/appsettings.Syncfusion.local.json`, and that generated file is already ignored by git.

If you want the browser client to point at a separately running local API host without emitting startup 404s, create an ignored file named `appsettings.Workspace.local.json` in the repository root with this shape:

```json
{ "WorkspaceApiBaseAddress": "http://localhost:5231" }
```

The build copies or generates that file into `wwwroot/appsettings.Workspace.local.json`. If neither the root file nor `WILEY_WORKSPACE_API_BASE_ADDRESS` is present, the build now emits an empty JSON object so the client no longer probes a missing file on startup.

Important: this app is a static Blazor WebAssembly site. That means the Syncfusion license is injected at build time and then included in the published client assets so `Program.cs` can read it from configuration at startup. This is not a private server-side runtime secret path.

### Grok And xAI Secrets

The xAI Grok API key is a backend runtime secret, not an Amplify frontend build secret.

The active browser client in [Program.cs](Program.cs) only talks to the thin API host. Grok participation happens server-side in the API and service layer, so keep the xAI key out of Amplify static hosting unless you intentionally redesign the app to call xAI directly from the browser.

For AWS deployment:

1. Store the Grok secret in AWS Secrets Manager as `Grok` or set `XAI:SecretName` / `XAI_SECRET_NAME` to the secret name you choose.
2. The secret value can be either a raw API key string or JSON containing `XAI_API_KEY`, `ApiKey`, `XaiApiKey`, or `GrokApiKey`.
3. Give the API host IAM role `secretsmanager:GetSecretValue` permission for that secret.
4. Set the runtime region with `WILEY_AWS_REGION`, `AWS_REGION`, or `AWS_DEFAULT_REGION`. The API defaults to `us-east-2`.

The API host now attempts to load the Grok secret from AWS Secrets Manager at startup only when `XAI_API_KEY` is not already present in configuration or the environment. Local `.NET user secrets` remain a development-only path.

Local PowerShell testing shells now treat the machine-scoped `XAI_API_KEY` as canonical. The active profiles under `Documents/PowerShell` repair stale process and user copies from machine scope on startup, so a fresh `pwsh` session reflects the current workstation testing key instead of inheriting an older user-level value.

Production App Runner now injects `XAI_API_KEY` from the runtime secret `wiley-widget/api/xai-api-key` and routes Jarvis traffic through the API Gateway xAI proxy `https://w544vrvb3i.execute-api.us-east-2.amazonaws.com/prod/v1`, with direct xAI retry disabled for the VPC-egress runtime. AWS App Runner only refreshes runtime secret and environment-variable values when the service is deployed or updated, so any future xAI secret rotation or endpoint change requires an App Runner redeploy.

Jarvis now follows a split transport model that matches the current xAI and Microsoft guidance:

1. Semantic Kernel stays on the OpenAI-compatible chat connector and must be configured with the service root ending in `/v1`, not `/v1/chat/completions`.
2. The direct xAI fallback path uses the Responses API at `/v1/responses`, which is xAI's recommended primary API surface.
3. The fallback request defaults `XAI:StoreResponses` to `false` because the app already persists conversation history locally and should not depend on xAI server-side storage unless you opt in deliberately.
4. Direct retry from the proxy to `api.x.ai` is now opt-in through `XAI:AllowDirectRetry=true`; the safe default is disabled so App Runner does not stack unreachable internet retries behind the VPC-egress proxy path.
5. All Jarvis/xAI traffic uses the named `IHttpClientFactory` client `WorkspaceAiTransport`, which applies pooled connections, a bounded connect timeout, response decompression, the App Runner TLS revocation workaround, and the standard .NET resilience handler.

## Aurora Database

The private Aurora PostgreSQL database is now provisioned in the dedicated `wiley-co-aurora-vpc` network and uses the canonical EF Core PostgreSQL migration under [src/WileyWidget.Data/Migrations](src/WileyWidget.Data/Migrations).

- Layout note: [docs/aws-aurora-private-layout.md](docs/aws-aurora-private-layout.md)
- Reset/apply runbook: [docs/aurora-postgresql-reset-runbook.md](docs/aurora-postgresql-reset-runbook.md)
- Cluster: `wiley-co-aurora-db`
- Writer instance: `wiley-co-aurora-db-1`
- Database name: `wileyco`

### Local API Database Secrets

The API host now supports `.NET user secrets` for local database overrides. That gives local debug a non-committed path for a real PostgreSQL target instead of relying on the checked-in localhost development placeholder.

To sync the current Aurora connection string into the API project's user secrets:

```powershell
./Scripts/sync-local-api-database-url.ps1
```

That script reads the `wiley-widget/api/database-url` secret from AWS Secrets Manager, writes it to `WileyCoWeb.Api/appsettings.Development.local.json`, and also stores it in API user secrets. The explicit local JSON file is ignored by git, is loaded ahead of the checked-in development placeholder, and disables degraded startup so a real-target local session does not silently fall back to seeded data.

If you use a port-forwarded or otherwise local-only PostgreSQL endpoint instead, set that value directly in `WileyCoWeb.Api/appsettings.Development.local.json` with this shape:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "<connection-string>"
  },
  "Database": {
    "AllowDegradedStartup": false,
    "SeedDevelopmentData": false
  }
}
```

Note: this workstation still needs TCP reachability to whatever PostgreSQL endpoint you store. If the target is the private Aurora writer, you must use a VPC-attached host, SSM tunnel, or another reachable endpoint; otherwise the API will still activate degraded mode.

## UI Rebuild Roadmap

The restored Wiley Widget rebuild plan is documented in [docs/wileyco-ui-rebuild-plan.md](docs/wileyco-ui-rebuild-plan.md).
The AWS server-side closure sequence for connecting the thin API to the widget is documented in [docs/aws-server-side-closure-plan.md](docs/aws-server-side-closure-plan.md).

- Focus: Syncfusion panel-first rate study workspace
- Backend: shared models, Aurora persistence, thin API, AI recommendations
- First slice: enterprise selector + break-even panel + scenario save/load

## Local Snapshot Host

The workspace client prefers a live snapshot endpoint at `api/workspace/snapshot` when it is available. Set `WILEY_WORKSPACE_API_BASE_ADDRESS` to point the Blazor client at the thin API host during local development, for example when running `WileyCoWeb.Api` separately. Amplify now writes that value into `wwwroot/appsettings.Workspace.local.json` during build; if no value is provided, the client falls back to same-origin API routing instead of the Grok gateway.

## QuickBooks Desktop Import

The current clerk-facing QuickBooks import path is the QuickBooks Import panel in the Wiley Widget workspace.

- Supported report families: `Transaction List by Date` and `General Ledger`
- Supported upload types: `.csv`, `.xlsx`, `.xls`
- Target table: `ledger_entries` through the thin API import endpoints

Use [docs/quickbooks-desktop-import-guide.md](docs/quickbooks-desktop-import-guide.md) for the operating procedure, required columns, export rules, and the guardrails the town clerk needs to follow before importing into Aurora through Wiley Widget.

Current data-path note:

- QuickBooks Desktop imports persist into `import_batches`, `source_files`, and `ledger_entries`.
- Reserve and historical-ledger analytics read from `ledger_entries`.
- Workspace baseline, top-variance, and scenario composition still depend on `Enterprises`, `BudgetEntries`, `MunicipalAccounts`, and `BudgetSnapshots`.
- Rebuilding Aurora with only import-pipeline tables is not sufficient for the full workspace analysis surface.

Reference-data note:

- `Import Data/` is the repo-local bootstrap sample set used for developer and admin seeding. It contains QuickBooks-style `.csv` and `.xlsx` source files, not XAML assets.
- Production App Runner does not bundle that folder by default. `WileyCoWeb.Api/appsettings.json` requires an explicit reference-data path in production.
- Monthly analysis imports should use the QuickBooks Import panel and API commit flow, not the repo-local `Import Data/` folder.

## Workspace Knowledge Layer

The server-side knowledge layer is the shared calculation surface for Decision Support and Jarvis.

- `IWorkspaceKnowledgeService` and `WorkspaceKnowledgeService` build live financial guidance from the selected enterprise, fiscal year, current rate, costs, projected volume, scenario pressure, reserve analytics, and top variances.
- `WileyCoWeb.Api` exposes that analysis at `/api/workspace/knowledge` using the shared contracts in [Contracts/WorkspaceKnowledgeContracts.cs](Contracts/WorkspaceKnowledgeContracts.cs).
- The browser client consumes that endpoint through [Services/WorkspaceKnowledgeApiService.cs](Services/WorkspaceKnowledgeApiService.cs).
- The Decision Support rail in [Components/JarvisChatPanel.razor.cs](Components/JarvisChatPanel.razor.cs) and the Semantic Kernel plugin in [src/WileyWidget.Services/Plugins/UserContextPlugin.cs](src/WileyWidget.Services/Plugins/UserContextPlugin.cs) now use the same server-backed knowledge so UI guidance and Jarvis recommendations stay grounded in the same facts.

Maintainer rule: do not reintroduce client-only financial heuristics or canned AI rationale where live knowledge is expected. New recommendation, scenario, and council-facing explanation work should extend the knowledge service and its thin API contract rather than bypassing it.

## AWS Backend Validation

AWS CLI validation on April 15, 2026 confirms the current production support picture:

- Amplify app `d2ellat1y3ljd9` remains the static `WEB` deployment for the Blazor WebAssembly client.
- Aurora PostgreSQL cluster `wiley-co-aurora-db` remains the system of record in private VPC `vpc-0b4e1d7362da22c17`.
- Secrets Manager contains the existing `Grok` secret for server-side xAI access and now also contains App Runner runtime secrets for the API database connection string, xAI key, and Syncfusion license.
- API Gateway `WileyJarvisApi` (`w544vrvb3i`) still serves only as the xAI proxy and is not the workspace API host.
- The xAI proxy still follows the AWS HTTP proxy pattern for greedy resources, and its resource policy allows requests from the Wiley Aurora VPC. Public workstation calls still return `403 AccessDeniedException`, but the App Runner service uses the proxy from that VPC path so Jarvis does not depend on direct outbound internet reachability to `api.x.ai`.
- Jarvis uses the proxy root `/prod/v1` for Semantic Kernel and normalizes any configured `.../chat/completions` suffix back to the `/v1` base URL before building the OpenAI-compatible client.
- Direct xAI fallback remains available through `/prod/v1/responses`, with direct retry to `https://api.x.ai/v1/responses` disabled unless `XAI:AllowDirectRetry` is explicitly enabled.
- AWS CLI provisioning created the thin API runtime path:
  - ECR repository `wiley-widget-api`
  - App Runner service `wiley-widget-api` at `https://mr7zeizxxd.us-east-2.awsapprunner.com`
  - App Runner VPC connector `wiley-widget-api-vpc-connector`
  - API security group `sg-050b6a6ae154820d5` with Aurora ingress on `5432`
  - Interface VPC endpoints for `execute-api` and `xray`
- Amplify app-level and `main` branch environment variables now include `WILEY_WORKSPACE_API_BASE_ADDRESS=https://mr7zeizxxd.us-east-2.awsapprunner.com`, and the Amplify app build spec was resynced from [amplify.yml](amplify.yml).

Production implication: the missing compute host is now provisioned, but final cutover is not complete until the App Runner service finishes healthy startup validation and Amplify performs a release using the staged API base address. Until that release happens, the public client can still reflect the previous routing behavior.

Runtime sizing used for the first App Runner deployment: `0.5 vCPU / 1 GB` with public ingress and VPC egress to Aurora. For the May 11 City Council working session, `1 vCPU / 2 GB`, minimum one warm instance, remains the safer baseline if load or export activity increases.

## Post-Production Operations

The repository now separates day-two operations guidance from release-status notes.

- Use [docs/post-production-operations-handbook.md](docs/post-production-operations-handbook.md) as the primary day-two production handbook for releases, rollback planning, monitoring, incident first response, data operations, and release evidence.
- Use [docs/post-production-documentation-review.md](docs/post-production-documentation-review.md) for the documentation inventory, identified gaps, and the recommended follow-on doc set for release records, incident response, and secret rotation.
- Use [docs/release-record-template.md](docs/release-record-template.md) for every production release, even small ones.
- Use [docs/secrets-and-config-rotation-runbook.md](docs/secrets-and-config-rotation-runbook.md) when rotating runtime secrets or changing App Runner configuration.
- Use [docs/incident-response-matrix.md](docs/incident-response-matrix.md) to classify production incidents and keep first response disciplined.
- Keep [docs/aws-server-side-closure-plan.md](docs/aws-server-side-closure-plan.md), [docs/aurora-postgresql-reset-runbook.md](docs/aurora-postgresql-reset-runbook.md), and [docs/quickbooks-desktop-import-guide.md](docs/quickbooks-desktop-import-guide.md) as the authoritative specialist documents linked from the handbook.

## Town Email Alias Routing

The `townofwiley.gov` email alias path now has a verified live success case for future reference.

- Live validation on April 16, 2026: a test email sent to `steve.mckitrick@townofwiley.gov` was forwarded to `bigessfour@gmail.com`.
- Inbound receipt uses Amazon SES in `us-east-1` because the public MX for `townofwiley.gov` points to `inbound-smtp.us-east-1.amazonaws.com`.
- Outbound forwarding uses Amazon SES in `us-east-2`; the SES dashboard SMTP endpoint `email-smtp.us-east-2.amazonaws.com` is the send path, not the inbound receiving path.
- The CMS/AppSync write path now lowercases `aliasAddress` on `createEmailAlias` and `updateEmailAlias` before DynamoDB writes.
- The `EmailAlias-j7b2x3sh7rcezekekkxxiak7hi-main` table now has the `byAliasAddress` global secondary index for normalized alias lookups.
- `TownOfWileyEmailAliasRouter` in `us-east-1` queries `byAliasAddress` and forwards only aliases where `active=true`.

If a future alias is created in CMS and does not route, check AppSync mutation writes, the `byAliasAddress` index status, and the CloudWatch log group for `TownOfWileyEmailAliasRouter` before changing SES or MX configuration.

## Browser E2E Tests

The Playwright-based browser suites in [tests/WileyCoWeb.E2ETests](tests/WileyCoWeb.E2ETests) run directly against the hosted site.

- `WILEYCO_E2E_BASE_URL` points the tests at the deployed app.
- The suites use ordinary Playwright assertions and do not require a visual-regression service.

Example PowerShell invocation:

```powershell
$env:WILEYCO_E2E_BASE_URL = 'https://main.d2ellat1y3ljd9.amplifyapp.com'
dotnet test tests/WileyCoWeb.E2ETests/WileyCoWeb.E2ETests.csproj --filter "FullyQualifiedName~Visual_WorkspaceOverview_MatchesBaseline"
```

## VS Code Playwright Setup

This workspace now carries a separate Node-based Playwright setup for the VS Code Playwright extension and agent workflows.

- `playwright.config.ts` defines Chromium and WebKit projects, HTML plus JSON reporters, retry-time tracing, and a managed local `dotnet run` web server whenever the effective base URL is the local default `http://localhost:5230`.
- `npm run playwright:test:hosted` runs the Node Playwright suite against the three verified live hosts: `https://main.d2ellat1y3ljd9.amplifyapp.com`, `https://wileywidget.townofwiley.gov`, and `https://www.wileywidget.townofwiley.gov`.
- `npm run playwright:test:ci` runs the full local Node Playwright suite with `--project=chromium --project=webkit` so CI covers both engines.
- `npm run playwright:test:docker-proof` builds the Ubuntu 24.04 Playwright container and runs the same CI path inside Docker Desktop before you push.
- `.vscode/tasks.json` exposes the same workflow as the `playwright: test hosted` task and writes per-host JSON artifacts under `TestResults/playwright-hosted`.
- `tests/playwright/workspace-smoke.spec.ts` gives the official Playwright extension and the Playwright Test Runner extension a concrete spec to discover, run, debug, record against, and inspect.
- `tests/playwright/support/workspace.ts` centralizes shell waits, panel navigation, numeric editing, and visual-snapshot prep so panel specs share the same lifecycle rules.
- `.vscode/settings.json` configures both installed Playwright extensions to use `playwright.config.ts`, default to the local workspace URL, and surface traces and locator copying in the IDE.
- `.vscode/mcp.json` registers the `playwright` MCP server with the standard `npx @playwright/mcp@latest` wiring so Copilot and other MCP-capable clients can drive a browser through Playwright tools.
- `package.json` adds `playwright:*` scripts for browser install, test runs, UI mode, codegen, reports, and agent CLI helpers.

Use the Node-based setup for VS Code testing UI and agent automation, but use the Docker proof as the pre-push validation path. The container mirrors the Ubuntu 24.04 GitHub Actions environment, publishes the app, serves `publish_output/wwwroot`, and runs the Chromium + WebKit Playwright pass in one shot.

The managed local `dotnet run` path in `playwright.config.ts` remains available for interactive debugging in the editor, but it is no longer the recommended proof before pushing.

If you want the Node Playwright setup to target a non-local site instead, set `WILEYCO_E2E_BASE_URL` to that host before running Playwright. The config skips the managed `dotnet run` server whenever the effective base URL is not the local default.

Do not use the bare Amplify default app domain `https://d2ellat1y3ljd9.amplifyapp.com` as a hosted regression target. The current Amplify app serves the production branch from `https://main.d2ellat1y3ljd9.amplifyapp.com`, while the bare default domain returns `404` even at `/`. If you want the bare default domain to land on production, that redirect needs to be configured in Amplify hosting or by making the custom domain canonical there; it is not controlled by the repo build spec.

## Observability (AWS X-Ray + CloudWatch Logs)

The thin API host uses AWS-native observability tools.

**Distributed Tracing — AWS X-Ray**
All incoming requests are traced via `AWSXRayRecorder.Handlers.AspNetCore`. Credentials are resolved automatically from the IAM execution role (Amplify/ECS task role) — no connection string required. Traces are visible in the [AWS X-Ray console](https://console.aws.amazon.com/xray/home).

**Startup Events — Amazon CloudWatch Logs**
Structured startup events (key resolution sources, secret presence) are emitted via `ILogger`. Amplify ships all stdout to CloudWatch Logs automatically. Query them in CloudWatch Logs Insights with:

```
fields @timestamp, @message
| filter @message like "WileyWidget.Startup.KeyResolution"
| sort @timestamp desc
```

## Local Secrets On macOS

This project now has a `.NET User Secrets` identity for local development.

- Initialize or inspect secrets from the repo root with `dotnet user-secrets list --project WileyCoWeb.csproj`.
- Store non-committed development secrets with `dotnet user-secrets set <key> <value> --project WileyCoWeb.csproj`.

Examples:

```bash
dotnet user-secrets set "OpenAI:ApiKey" "<your-key>" --project WileyCoWeb.csproj
dotnet user-secrets set "QuickBooks:ClientSecret" "<your-secret>" --project WileyCoWeb.csproj
```

Syncfusion keys remain environment-variable based by policy:

```bash
launchctl setenv SYNCFUSION_LICENSE_KEY "<your-runtime-license-key>"
launchctl setenv SYNCFUSION_API_KEY_PATH "$HOME/.config/syncfusion/documentsdk.key"
```

Restart VS Code after changing `launchctl` values so GUI-launched tools pick them up.

The workspace MCP config points `Syncfusion_API_Key_Path` at `/Users/stephenmckitrick/.config/syncfusion/documentsdk.key`, so keep the API key in that file as a single line of text.

## Amplify CLI Reference

### Local Amplify CLI

- `amplify init` initializes a new Amplify project and must be run from an empty directory.
- `amplify add hosting` adds hosting resources to an Amplify backend project.
- `amplify configure hosting` configures hosting resources such as S3, CloudFront, and publish ignore rules.
- `amplify publish` builds and publishes the backend and frontend.
- `amplify pull <app-id>` pulls an existing Amplify app into a local workspace.
- `amplify push` deploys backend changes from the local project.
- `amplify status` shows the local and cloud backend status.

### AWS CLI Hosting Commands

- `aws amplify create-app` creates an Amplify hosting app from the AWS CLI.
- `aws amplify create-branch` creates the production or preview branch for a hosted app.
- `aws amplify update-app` updates app-level hosting settings and environment variables.
- `aws amplify list-apps` and `aws amplify list-branches` are useful for confirming hosted app state.
