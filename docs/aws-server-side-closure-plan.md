# AWS Server-Side Closure Plan

This plan closes the remaining AWS and server-side gaps between the current static Wiley Widget deployment and the production-ready system described in [docs/wileyco-ui-rebuild-plan.md](docs/wileyco-ui-rebuild-plan.md).

## Objective

Connect the thin API to the live Wiley Widget client so the product can achieve the stated goals:

- QuickBooks Desktop actuals remain canonical.
- Enterprise-by-enterprise financial analysis stays separated and auditable.
- Council-facing views show current position, subsidization, and 1, 5, and 10-year trend outcomes from live data.
- AI recommendations and Jarvis explanations come from the same server-side calculations used by the UI.
- Scenario, baseline, and export workflows stay reproducible for the May 11, 2026 Utility Rate Working meeting.

## Current Validated State

Validated on April 15, 2026 using the AWS CLI and the current repository:

- Amplify app `d2ellat1y3ljd9` is a static `WEB` deployment for the Blazor WebAssembly client.
- Aurora PostgreSQL cluster `wiley-co-aurora-db` is available in `us-east-2`.
- The Aurora footprint is private and has no public ingress path, per [docs/aws-aurora-private-layout.md](docs/aws-aurora-private-layout.md).
- Secrets Manager contains the `Grok` secret for server-side xAI access.
- API Gateway `w544vrvb3i` (`WileyJarvisApi`) only exposes `/{proxy+}` as an `HTTP_PROXY` integration to `https://api.x.ai/v1`.
- App Runner service `wiley-widget-api` now exists at `https://mr7zeizxxd.us-east-2.awsapprunner.com` and is backed by ECR repository `wiley-widget-api`.
- App Runner private egress is provisioned through VPC connector `wiley-widget-api-vpc-connector` and API security group `sg-050b6a6ae154820d5`.
- Interface VPC endpoints now exist for `com.amazonaws.us-east-2.execute-api` and `com.amazonaws.us-east-2.xray`.
- App Runner runtime secrets now exist in Secrets Manager:
	- `wiley-widget/api/database-url`
	- `wiley-widget/api/xai-api-key`
	- `wiley-widget/api/syncfusion-license-key`
- Amplify app `d2ellat1y3ljd9` and branch `main` now carry `WILEY_WORKSPACE_API_BASE_ADDRESS=https://mr7zeizxxd.us-east-2.awsapprunner.com` without storing the Syncfusion license in Amplify environment variables.
- The Syncfusion build-time license is now stored in the Amplify Gen 1 environment-secret path `/amplify/d2ellat1y3ljd9/main/SYNCFUSION_LICENSE_KEY`.
- App Runner `/health` currently returns `200 Healthy`.
- App Runner `/api/workspace/snapshot` currently returns `200` with populated enterprise options and live workspace data.
- App Runner `/api/workspace/knowledge` currently returns `200` against the current snapshot payload.
- App Runner `/api/ai/chat` currently returns `200` with a fallback onboarding response.
- App Runner `/api/workspace/reference-data/import` currently returns `400` with `Import data folder '/app/Import Data' was not found.`, which confirms the route is deployed and that production does not currently bundle the repo-local bootstrap folder.
- Aurora PostgreSQL now runs from encrypted cluster `wiley-co-aurora-db-encrypted` (`StorageEncrypted=true`); the original unencrypted cluster `wiley-co-aurora-db` is retained temporarily for rollback.
- Amplify release job `32` failed because the build used a floating `.NET 9` SDK while the cloned repo required the exact `global.json` version.
- The workspace now contains the corrected [amplify.yml](../amplify.yml) that installs the pinned SDK version, but release job `33` confirmed that Amplify is still building from the tracked GitHub branch copy of `amplify.yml`. The branch used for production releases must carry the same file change before the public release can succeed.

Production conclusion: the browser shell is deployed, the thin API runtime infrastructure exists, and the current App Runner revision is serving the main workspace routes from the encrypted Aurora target. The remaining blockers are final Amplify cutover verification, a durable production reference-data source policy, and the longer-term API hosting roadmap. The only Aurora follow-up is retirement of the rollback cluster after the observation window.

## Provisioned Execution Checklist

- [x] Create ECR repository `wiley-widget-api`.
- [x] Create App Runner IAM roles `WileyWidgetAppRunnerEcrAccessRole` and `WileyWidgetAppRunnerInstanceRole`.
- [x] Create API security group `sg-050b6a6ae154820d5` and allow Aurora ingress from it on `5432`.
- [x] Create interface VPC endpoints for `execute-api` and `xray` in the Aurora VPC.
- [x] Create App Runner VPC connector `wiley-widget-api-vpc-connector`.
- [x] Create Secrets Manager entries for the App Runner database connection string, xAI API key, and Syncfusion runtime license.
- [x] Build and push the API container image to ECR.
- [x] Create App Runner service `wiley-widget-api` with VPC egress and `/health` health checks.
- [x] Update Amplify app/branch configuration so the next release targets the App Runner URL instead of the Grok proxy.
- [x] Move the Syncfusion build-time license out of Amplify environment variables and into the Amplify Gen 1 environment-secret path.
- [x] Validate `/health` from the public App Runner hostname.
- [x] Validate `/api/ai/chat` from the public App Runner hostname.
- [x] Validate `/api/workspace/snapshot` with live enterprise bootstrap data instead of the current empty-enterprise response.
- [x] Validate `/api/workspace/knowledge` without the current `500` failure.
- [x] Validate that the deployed App Runner revision includes `/api/workspace/reference-data/import` and the other current repo routes.
- [ ] Start and verify the Amplify release that publishes the staged `WILEY_WORKSPACE_API_BASE_ADDRESS` with the corrected pinned-SDK build spec.
- [ ] Replace the raw App Runner hostname with final public API DNS if `api.wileywidget.townofwiley.gov` is required before go-live.

## Required AWS Additions

### 1. Thin API Compute Host

Deploy [WileyCoWeb.Api/Program.cs](../WileyCoWeb.Api/Program.cs) to a small always-available compute service.

Recommended target: AWS App Runner.

- Why: smallest operational lift for the current ASP.NET Core thin API.
- Runtime baseline for general testing: `0.5 vCPU / 1 GB`.
- Runtime baseline for the May 11 Council session: `1 vCPU / 2 GB`, minimum one warm instance.
- Autoscaling: keep at least one warm instance and allow short burst headroom during demos and imports.

Fallback options:

- ECS Fargate if tighter VPC/network control is required.
- Lambda only if the API is intentionally refactored for function-style hosting and cold-start behavior.

### 2. Network Path To Aurora

The API compute host must be able to reach the private Aurora writer endpoint.

- Create or reuse an App Runner VPC connector for the Aurora VPC `vpc-0b4e1d7362da22c17`.
- Use the private subnets documented in [docs/aws-aurora-private-layout.md](docs/aws-aurora-private-layout.md).
- Create an API security group such as `wiley-widget-api-sg`.
- Allow inbound `5432` from the API security group to `sg-0cacdba1850b420f7` (`wiley-co-aurora-db-sg`).
- Keep Aurora private. Do not add public access, public subnets, or broad CIDR rules.

### 3. Runtime Secrets And Environment Configuration

The API host needs production runtime configuration that Amplify cannot provide to the browser shell.

Required runtime values:

- `DATABASE_URL` or `ConnectionStrings__DefaultConnection`
- `WILEY_AWS_REGION=us-east-2`
- `XAI_SECRET_NAME=Grok`
- `ASPNETCORE_ENVIRONMENT=Production`
- `Database__AllowDegradedStartup=false`
- `Database__SeedDevelopmentData=false`
- `Database__ApplyMigrations=false` unless a controlled migration step is being run
- `SYNCFUSION_LICENSE_KEY` only if server-side document generation is required in the API host at runtime

Recommended secret sources:

- Store xAI in Secrets Manager under `Grok`.
- Store the database connection string in Secrets Manager or inject it from the App Runner service configuration.
- Store the Amplify Syncfusion build-time license in Systems Manager Parameter Store at `/amplify/d2ellat1y3ljd9/main/SYNCFUSION_LICENSE_KEY` for the `main` build.
- Avoid pushing any API runtime secret into Amplify static hosting unless it must be exposed to the browser, which is not the case here.

### 4. IAM For The API Runtime

Create or attach an instance role for the API host with the minimum permissions needed.

- `secretsmanager:GetSecretValue` for `Grok` and any database connection-string secret used by the API
- `xray:PutTraceSegments` and `xray:PutTelemetryRecords` if X-Ray tracing remains enabled
- CloudWatch logging permissions if the chosen compute platform does not grant them automatically

### 5. Public API Addressing

The client must know where the thin API lives.

Preferred option:

- Create `api.wileywidget.townofwiley.gov` and point it at the App Runner service.
- Set Amplify `WILEY_WORKSPACE_API_BASE_ADDRESS` to that URL.
- Redeploy Amplify so [amplify.yml](../amplify.yml) emits the correct `wwwroot/appsettings.Workspace.local.json`.

Alternative option:

- Use the raw App Runner URL temporarily for staging validation.

Do not point `WILEY_WORKSPACE_API_BASE_ADDRESS` at `w544vrvb3i.execute-api.us-east-2.amazonaws.com/prod`. That gateway is only an xAI proxy today, not the workspace API.

### 6. Observability And Operations

Make the thin API operable before the Council session.

- Keep `/health` exposed and monitored.
- Send structured application logs to CloudWatch.
- Keep AWS X-Ray enabled if traces are part of the support workflow.
- As of 2026-04-16, startup wiring and IAM permissions for X-Ray are present, but the live App Runner service still reports `ObservabilityConfiguration=null` and a same-window `aws xray get-service-graph` check in `us-east-2` returned no Wiley API service nodes even after fresh `/health` traffic. AWS App Runner tracing is disabled by default unless observability configuration enables it, so treat trace emission as not yet proven and likely disabled at the service layer.
- Capture a post-release observability follow-up to either restore verified live X-Ray emission or migrate the service instrumentation to OpenTelemetry before expanding the production support posture.
- Add alarms for `5xx` rate, latency, and unhealthy instance count.
- Retain a deployment log or release note for each pre-meeting push.

## Code-Complete Closures

These original plan items are now closed in the repository and should not be treated as open implementation work anymore.

- CORS is now configuration-driven in [WileyCoWeb.Api/Program.cs](../WileyCoWeb.Api/Program.cs) and [WileyCoWeb.Api/appsettings.json](../WileyCoWeb.Api/appsettings.json), including the town domains.
- The thin API exposes `/api/workspace/snapshot`, `/api/workspace/knowledge`, `/api/ai/chat`, `/api/workspace/reference-data/import`, scenario endpoints, and utility-customer CRUD in the current repo.
- The workspace knowledge layer is implemented in code and is shared by the API, the Decision Support rail, and Jarvis.
- The production migration and Aurora recovery path is documented in [docs/aurora-postgresql-reset-runbook.md](aurora-postgresql-reset-runbook.md) with supporting scripts under [Scripts](../Scripts).
- API Gateway `w544vrvb3i` is now clearly documented as an xAI proxy only, not the workspace API backend.

## Remaining Server-Side Gaps To Close In Deployment And Data

These are the concrete gaps still blocking the stated goals.

### Gap 1. Amplify Public Cutover Is Not Yet Re-verified

Current state:

- App Runner is serving `/health`, `/api/workspace/snapshot`, `/api/workspace/knowledge`, and `/api/workspace/reference-data/import` as expected.
- The Syncfusion license has been moved out of Amplify environment variables and into the Amplify Gen 1 secret path.
- Amplify app `d2ellat1y3ljd9` is Git-connected to `https://github.com/Bigessfour/wiley-co-web`, and production job `40` successfully deployed commit `017c3e5013a446ecb5de087c240777b051d29120` from `main`.
- The hosted browser client is now on the updated release: the public `/_framework/blazor.boot.json` hash is `sha256-RwMqxCQdkIaKM6AosQxBz5hOOkNbTtXFw9HcRvD7UF8=` with `WileyCoWeb.u6y6jaqdar.wasm`.
- A post-deploy 2026-04-16 hosted Playwright run against `https://main.d2ellat1y3ljd9.amplifyapp.com/wiley-workspace` no longer failed on missing UI, but it did expose the deployed QuickBooks uploader path as the remaining browser blocker.
- The current working tree now replaces that QuickBooks file-selection path with a Blazor `InputFile` plus an explicit Analyze action, and local QuickBooks component tests pass on the refactored panel.
- The remaining unresolved cutover item is therefore a new public release of the latest QuickBooks client fix, not a question about whether Amplify can publish from Git.

Closure action:

- Re-run validation from the public site against `/api/workspace/snapshot`, `/api/workspace/knowledge`, and the workspace shell.
- Publish the latest QuickBooks client change through the Git-connected Amplify branch so the public site actually contains the `InputFile`-based uploader refactor.
- After that release, re-run the hosted QuickBooks assistant browser flow against `https://main.d2ellat1y3ljd9.amplifyapp.com/wiley-workspace`.
- If the post-release hosted run still fails, treat the remaining issue as production-only rather than deployment drift.

### Gap 2. Production Reference Data Must Stay External To The App Runner Image

Current state:

- The current repository and production API are now aligned on policy: production requires an explicit reference-data path and does not assume a bundled `Import Data` folder.
- `WileyCoWeb.Api/appsettings.json` sets `WorkspaceReferenceData:RequireExplicitImportDataPath=true`.
- The live App Runner route returns `400` with the missing-folder message, which confirms the container does not currently ship the repo-local sample set.
- Monthly clerk imports are already handled through the QuickBooks Import panel and the API commit flow into Aurora.

Closure action:

- Keep recurring monthly imports on the QuickBooks panel and API commit path.
- Treat `Import Data/` as a developer or admin bootstrap set only.
- If production needs repeatable reference-data bootstrap, provide it through an explicit path or managed source such as S3 plus an admin-only import job, rather than baking files into the App Runner image.

### Gap 3. Aurora Encryption Cutover Is Complete

Current state:

- App Runner runtime secret `wiley-widget/api/database-url` now points at encrypted cluster `wiley-co-aurora-db-encrypted`.
- Snapshot `wiley-co-aurora-db-preenc-20260416-2340` was restored into encrypted Aurora PostgreSQL cluster `wiley-co-aurora-db-encrypted` in the same private subnet and security-group posture, with `StorageEncrypted=true` and `HttpEndpointEnabled=true`.
- Post-cutover validation on 2026-04-16: App Runner deployment `106a3bc87c7747da933511eb22e29eba` succeeded, `/health` returned `200 Healthy`, `/api/workspace/snapshot` returned populated enterprise options and projections, and live session checks showed traffic on the encrypted cluster while the original cluster remained idle.

Closure action:

- Keep `wiley-co-aurora-db` available as a rollback target during the short post-cutover observation window.
- After acceptance, capture the final rollback snapshot and decommission the unencrypted cluster plus any rollback-only artifacts.

## Operational Decisions Made On April 15, 2026

### Reference-Data Source Policy

- Do not bundle the repo-local `Import Data/` folder into the App Runner image.
- Keep `Import Data/` as a bootstrap/admin dataset for local seeding, diagnostics, and one-time environment initialization.
- Use the QuickBooks Import panel and API commit flow for recurring monthly analysis files.
- If production requires centralized bootstrap data, move the curated seed files to an explicit managed source such as S3 and invoke the reference-data import with an explicit path or background job.

### Aurora Encryption Cutover Record

1. Captured manual snapshot `wiley-co-aurora-db-preenc-20260416-2340` from source cluster `wiley-co-aurora-db`.
2. Restored encrypted target `wiley-co-aurora-db-encrypted` in `us-east-2` with the same private subnet group `wiley-co-aurora-subnets`, security group `sg-0cacdba1850b420f7`, and Aurora PostgreSQL `14.17` engine version.
3. Created writer instance `wiley-co-aurora-db-encrypted-1` and enabled the Aurora Data API with `aws rds enable-http-endpoint`.
4. Validated parity on key tables (`Enterprises`, `BudgetEntries`, `MunicipalAccounts`, `UtilityCustomers`, `budget_snapshots`, `budget_snapshot_artifacts`, `ledger_entries`) before cutover.
5. Rotated App Runner secret `wiley-widget/api/database-url` to the encrypted writer endpoint and started deployment `106a3bc87c7747da933511eb22e29eba`, which completed successfully.
6. Verified post-cutover runtime behavior with `200 Healthy` on `/health`, populated data from `/api/workspace/snapshot`, and active-session checks showing live traffic on the encrypted cluster while the original cluster stayed idle.

### Observability Follow-Up

- Keep the current X-Ray startup configuration in place for the present App Runner deployment.
- Treat live trace emission as an unresolved operational verification item until a Wiley service node appears in X-Ray after controlled traffic.
- Current investigation result: the API host initializes the .NET X-Ray SDK, but the repo contains no X-Ray daemon address or daemon-side configuration, and AWS states that the .NET SDK generates and sends trace data to the X-Ray daemon. This makes the current App Runner deployment a poor place to invest more time unless a daemon delivery path is added deliberately.
- Create a post-release work item to migrate service instrumentation from the legacy X-Ray SDK chain to OpenTelemetry rather than deepening dependency on the 2026 maintenance-mode path.

## Post-Release Infrastructure Track

These items are intentionally scheduled after the current widget proof closeout so they are owned as infrastructure follow-up work rather than left as implied technical debt.

- Aurora rollback retirement: after the observation window, snapshot and decommission the original unencrypted cluster plus any rollback-only resources.
- Observability migration: replace or supersede the current X-Ray SDK path with OpenTelemetry, and only keep X-Ray if a supported, proven delivery path is required during the transition.
- Public cutover hardening: rerun the hosted QuickBooks assistant browser flow and any other public-shell regression checks after the next successful Amplify release.
- Public cutover hardening: treat boot-manifest and `WileyCoWeb` fingerprint verification as part of release validation so future public E2E failures can be distinguished from stale-browser deployments immediately.

### App Runner Replacement Review

- Short term: keep App Runner for the current thin API because it is already live, VPC-attached, and validated.
- Trigger a formal replacement review before any broad production expansion because AWS App Runner is closed to new customers after April 30, 2026 even though existing customers can continue operating.
- Compare at least these targets: App Runner as-is, ECS Fargate service behind an ALB, and Lambda only if the API surface is intentionally reduced and cold-start behavior is acceptable.
- Evaluate each option on private Aurora connectivity, secrets handling, deployment workflow, cold-start and steady-state latency, CloudWatch/X-Ray support, and operational cost.
- Current direction: plan ECS Fargate as the likely successor if Wiley needs a long-lived supported path beyond the current App Runner footprint.

## Recommended Delivery Sequence

### Phase 1. Runtime Foundation

Target: complete the already-provisioned runtime path.

- Keep App Runner on the current repo revision.
- Keep VPC connectivity, secrets, and API security group access intact.
- Validate `/health`, `/api/workspace/snapshot`, `/api/workspace/knowledge`, `/api/ai/chat`, and `/api/workspace/reference-data/import` from the public hostname.

Exit criteria:

- API host responds successfully.
- API can connect to Aurora and expose the current repo routes.
- No degraded-mode startup in production.

### Phase 2. Widget-To-API Connection

Target: immediately after runtime validation passes.

- Keep Amplify pointed at the App Runner URL or final API DNS.
- Publish the corrected [amplify.yml](../amplify.yml) to the tracked GitHub branch, then run a successful Amplify release with the pinned-SDK build spec.
- Verify the generated workspace config file is present in the public build.

Exit criteria:

- Browser calls `/api/workspace/snapshot` successfully from the public site.
- Browser calls `/api/workspace/knowledge` successfully from the public site.
- Decision Support rail loads live knowledge, not fallback-only status.

### Phase 3. Data And AI Validation

Target: after widget-to-API connection works.

- Validate baseline save/load against Aurora.
- Validate scenario save/list/apply against Aurora.
- Validate QuickBooks import preview and commit against canonical tables.
- Validate `/api/ai/chat` and recommendation history.
- Validate server-side exports if Council packet generation is in scope.

Exit criteria:

- Water, Sewer, Trash, and Apartments each load live enterprise context.
- Scenario changes rehydrate the widget correctly.
- Jarvis and Decision Support return live, matching financial context.

### Phase 4. Go-Live Hardening

Target: complete before the Council rehearsal week.

- Add CloudWatch alarms.
- Record a release checklist and rollback path.
- Validate DNS and TLS for the final public URLs.
- Confirm no environment points the client back to the Grok gateway.
- Run a full rehearsal with council-style workflows and meeting data.

Exit criteria:

- Public client, API, Aurora, and secrets all work together end-to-end.
- Support team has logs, health checks, and rollback instructions.

## Acceptance Criteria Aligned To Product Goals

The AWS/server-side plan is complete only when all items below are true.

- QuickBooks imports write to canonical tables with duplicate protection intact.
- Each enterprise loads through the same widget flow but remains analytically separated.
- Current position, subsidization, and trend panels are driven by live API data.
- The Decision Support rail and Jarvis use the same server-side knowledge layer.
- Scenario, baseline, and recommendation history changes persist and reload from Aurora.
- Public users can open the widget and perform the rate-study workflow without a missing-backend failure.

## Recommended Owner Checklist

Platform / AWS owner:

- Keep App Runner on the current repo image and revision.
- Keep VPC and security-group access to Aurora healthy.
- Keep IAM role and secrets aligned with production settings.
- Create API DNS.
- Add monitoring and alarms.

Application owner:

- Confirm production env vars disable degraded mode.
- Keep the migration runbook current and use it as the release path.
- Validate API endpoints and live widget flows.
- Keep the town-clerk import procedure aligned with the live QuickBooks import path in [docs/quickbooks-desktop-import-guide.md](docs/quickbooks-desktop-import-guide.md).

Release owner:

- Redeploy Amplify after the tracked GitHub branch contains the corrected pinned-SDK [amplify.yml](../amplify.yml).
- Set `WILEY_WORKSPACE_API_BASE_ADDRESS` to the API URL.
- Run end-to-end smoke tests before the Council meeting.

## Immediate Next Action

The next server-side move should be to redeploy the current `WileyCoWeb.Api` revision to App Runner, then rerun the public validation sequence (`/health`, `/api/workspace/snapshot`, `/api/workspace/knowledge`, `/api/ai/chat`, `/api/workspace/reference-data/import`) before completing the Amplify cutover release.