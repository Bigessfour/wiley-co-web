# AWS Server-Side Closure Plan

This plan records the current AWS and server-side state for Wiley Widget, the remaining pre-meeting hardening items, and the post-release infrastructure follow-up described in [docs/wileyco-ui-rebuild-plan.md](docs/wileyco-ui-rebuild-plan.md).

## Objective

Connect the thin API to the live Wiley Widget client so the product can achieve the stated goals:

- QuickBooks Desktop actuals remain canonical.
- Enterprise-by-enterprise financial analysis stays separated and auditable.
- Council-facing views show current position, subsidization, and 1, 5, and 10-year trend outcomes from live data.
- AI recommendations and Jarvis explanations come from the same server-side calculations used by the UI.
- Scenario, baseline, and export workflows stay reproducible for the May 11, 2026 Utility Rate Working meeting.

## Current Validated State

Validated through April 21, 2026 using the AWS CLI, hosted browser evidence, public API checks, and the current repository:

- Amplify app `d2ellat1y3ljd9` is a static `WEB` deployment for the Blazor WebAssembly client.
- Aurora PostgreSQL now runs from encrypted cluster `wiley-co-aurora-db-encrypted` in `us-east-2`, and the original unencrypted cluster `wiley-co-aurora-db` is no longer present.
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
- App Runner `/api/workspace/snapshot` currently returns `200` with populated enterprise options and live workspace data for all four intended enterprises: `Apartments`, `Trash`, `Water Utility`, and `Wiley Sanitation District`.
- App Runner `/api/workspace/knowledge` currently returns `200` against the current snapshot payload.
- App Runner `/api/ai/chat` currently returns `200` with a fallback onboarding response.
- As of 2026-04-22, a second production `/api/ai/chat` turn for `Water Utility` / `FY2026` timed out upstream instead of returning a stable live answer, so Jarvis is not yet production-ready for council-facing use.
- App Runner `/api/utility-customers` now returns a live imported dataset, and the public snapshot exposes all four intended enterprises with non-zero current rate, total cost, projected volume, projection rows, and scenario items for `FY2026`.
- App Runner `/api/workspace/reference-data/import` still returns `400` when called without an explicit path or uploaded files, which confirms the route is deployed and that production does not bundle the repo-local bootstrap folder by default. A production multipart import was executed on 2026-04-22 using uploaded bootstrap files and returned `200`, updating two discovered enterprise sources plus ten utility-customer rows while skipping duplicate sample ledger inputs.
- Integration coverage exists for snapshot persistence, reference-data bootstrap import, knowledge generation, and Jarvis history persistence. The current repo regression suite for `WorkspaceSnapshotApiTests`, `WorkspaceReferenceDataApiTests`, `WorkspaceKnowledgeApiTests`, `WorkspaceAiApiTests`, and `WorkspaceKnowledgeServiceTests` passed on 2026-04-22 after stopping the local `WileyCoWeb.Api` process that otherwise locks the API build outputs during test rebuilds.
- CloudWatch alarms now exist for App Runner and Aurora, but monitoring sign-off is still open because the live set must be reviewed for correctness and operator readiness. Current alarm inventory includes `WileyCo-AppRunner-5xxRatePercent`, `WileyCo-AppRunner-ActiveInstancesLow`, `WileyCo-AppRunner-RequestLatencyHigh`, `WileyCo-Aurora-CPUHigh`, and `WileyCo-Aurora-ConnectionsHigh`, with `WileyCo-AppRunner-ActiveInstancesLow` currently in `ALARM` state.
- App Runner custom-domain attachment is still empty, so `api.wileywidget.townofwiley.gov` has not been provisioned on the service.
- Amplify production branch `main` is Git-connected to `https://github.com/Bigessfour/wiley-co-web`, and production job `43` successfully deployed commit `8620cf72f569058a5c72619e9e341e25fb0b34f1` to `https://main.d2ellat1y3ljd9.amplifyapp.com`.
- Hosted browser proof is complete on the public Amplify site: both QuickBooks preview and QuickBooks assistant flows passed against `https://main.d2ellat1y3ljd9.amplifyapp.com` on 2026-04-16, confirming the shipped client now includes the `InputFile`-based uploader refactor.

Production conclusion: the browser shell is deployed, the public Amplify cutover is verified, and the current App Runner revision is serving the main workspace routes from the encrypted Aurora target. Phase 2 infrastructure and business-data proof are now materially complete: alarms exist, the raw App Runner hostname remains the accepted API baseline, Aurora encryption and rollback-cluster retirement are confirmed, a production reference-data import has been exercised, the live snapshot exposes all four intended enterprises, and current break-even/projection calculations can be spot-checked from production payloads. Production-ready sign-off is still blocked by live chat reliability because Jarvis non-onboarding production chat is not yet proven and the checked second turn still timed out upstream. Keep observability review and the longer-term API hosting roadmap on the post-release track.

## Provisioned Execution Checklist

- [x] Create ECR repository `wiley-widget-api`.
- [x] Create App Runner IAM roles `WileyWidgetAppRunnerEcrAccessRole` and `WileyWidgetAppRunnerInstanceRole`.
- [x] Create API security group `sg-050b6a6ae154820d5` and allow Aurora ingress from it on `5432`.
- [x] Create interface VPC endpoints for `execute-api` and `xray` in the Aurora VPC.
- [x] Create App Runner VPC connector `wiley-widget-api-vpc-connector`.
- [x] Create Secrets Manager entries for the App Runner database connection string, xAI API key, and Syncfusion runtime license.
- [x] Build and push the API container image to ECR.
- [x] Create App Runner service `wiley-widget-api` with VPC egress and `/health` health checks.
- [x] Update Amplify app/branch configuration so releases target the App Runner URL instead of the Grok proxy.
- [x] Move the Syncfusion build-time license out of Amplify environment variables and into the Amplify Gen 1 environment-secret path.
- [x] Validate `/health` from the public App Runner hostname.
- [x] Validate `/api/ai/chat` from the public App Runner hostname.
- [x] Validate `/api/workspace/snapshot` with live enterprise bootstrap data.
- [x] Validate `/api/workspace/knowledge` successfully against the live snapshot payload.
- [x] Validate that the deployed App Runner revision includes `/api/workspace/reference-data/import` and the other current repo routes.
- [x] Start and verify the Amplify release that publishes `WILEY_WORKSPACE_API_BASE_ADDRESS` with the corrected pinned-SDK build spec.
- [ ] Curate the first-time reference-data seed folder, remove duplicate report variants, and run one admin bootstrap import to complete missing customer data.
- [x] Keep the raw App Runner hostname as the current production API baseline.
- [ ] Revisit `api.wileywidget.townofwiley.gov` only after the current raw-host release path, first-time seed import, and monitoring are stable.
- [x] Complete one production reference-data import that results in all intended enterprises appearing in `/api/workspace/snapshot`, then capture the response summary as release evidence.
- [x] Manually spot-check break-even, scenario impact, and projection outputs from `/api/workspace/snapshot` after the production import.
- [ ] Confirm Jarvis returns live snapshot-grounded responses without timeout or deterministic fallback on a non-onboarding production conversation.

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

Current release decision:

- Keep `WILEY_WORKSPACE_API_BASE_ADDRESS=https://mr7zeizxxd.us-east-2.awsapprunner.com` for the current hardening window.
- Keep Amplify generating `wwwroot/appsettings.Workspace.local.json` from the raw App Runner hostname until the first-time seed import and monitoring rollout are complete.
- Do not add Route 53, ACM validation, and App Runner custom-domain cutover risk in the same window as the remaining data-seed and alarm work.

Deferred public-DNS option:

- Create `api.wileywidget.townofwiley.gov` and point it at the App Runner service after the raw-host baseline is stable.
- Set Amplify `WILEY_WORKSPACE_API_BASE_ADDRESS` to that URL only as part of a dedicated cutover release.
- Redeploy Amplify so [amplify.yml](../amplify.yml) emits the updated `wwwroot/appsettings.Workspace.local.json`.
- Smoke-test `/health`, `/api/workspace/snapshot`, `/api/workspace/knowledge`, `/api/ai/chat`, `/api/workspace/reference-data/import`, and the hosted workspace shell after the cutover.

Do not point `WILEY_WORKSPACE_API_BASE_ADDRESS` at `w544vrvb3i.execute-api.us-east-2.amazonaws.com/prod`. That gateway is only an xAI proxy today, not the workspace API.

### 6. Observability And Operations

Make the thin API operable before the Council session.

- Keep `/health` exposed and monitored.
- Send structured application logs to CloudWatch.
- Keep the current X-Ray startup wiring in place for now, but do not treat verified live trace emission as a release prerequisite.
- As of 2026-04-16, startup wiring and IAM permissions for X-Ray are present, but the live App Runner service still reports `ObservabilityConfiguration=null` and a same-window `aws xray get-service-graph` check in `us-east-2` returned no Wiley API service nodes even after fresh `/health` traffic. AWS App Runner tracing is disabled by default unless observability configuration enables it, so treat trace emission as not yet proven and likely disabled at the service layer.
- Capture the observability follow-up on the post-release infrastructure track, with OpenTelemetry as the preferred successor instead of spending more release time on the current App Runner X-Ray chain.
- As of 2026-04-23, `aws cloudwatch describe-alarms --region us-east-2` shows the Wiley runtime alarm set is live and currently clear: App Runner (`WileyCo-AppRunner-5xxRatePercent`, `WileyCo-AppRunner-RequestLatencyHigh`), Aurora (`WileyCo-Aurora-CPUHigh`, `WileyCo-Aurora-ConnectionsHigh`), and Wiley-specific Grok/Jarvis diagnostics (`WileyCo-Grok-AuthOrKeyFailures`, `WileyCo-Grok-EndpointFailures`, `WileyCo-Grok-KeyLoadFailures`, `WileyCo-Grok-NetworkExceptions`, `WileyCo-Jarvis-ChatFailures`, `WileyCo-Jarvis-FallbackUsed`, `WileyCo-Jarvis-Legacy403Forbidden`, `WileyCo-Jarvis-TlsRevocationFailure`). No alarms were in `ALARM` at the time of review.
- Add App Runner alarms for `5xx` rate, latency, and unhealthy instance count:
  - `5xx` rate: alert when server errors are sustained across two consecutive 5-minute periods or exceed an agreed low-volume error threshold during active traffic; first response is to check App Runner events, CloudWatch logs, and the most recent deployment before deciding on rollback.
  - Latency: alert when request latency is materially above the current interactive baseline for two consecutive 5-minute periods; first response is to inspect import activity, database saturation, and recent deployments.
  - Unhealthy instances: alert immediately when unhealthy instances are reported; first response is to inspect service health, deployment events, and restart or roll back if the condition persists.
- Add Aurora alarms for CPU and connection pressure:
  - CPU: alert on sustained elevated `CPUUtilization` so import or analytics spikes are visible before they degrade user flows.
  - Connections: alert when `DatabaseConnections` rises toward the observed operating ceiling or deviates sharply from current baseline, then inspect connection pooling and long-running queries before rerunning imports.
- Attach all Wiley API runtime alarms to the same operator notification target and retain the alarm names, SNS target, and dashboard link in the release record.
- As of 2026-04-23, the `WileyCo-Grok-Alerts` email topic was tuned to reduce benign inbox traffic: `OKActions` were removed from the Wiley alarm set, and `WileyCo-Jarvis-FallbackUsed` plus `WileyCo-Jarvis-Legacy403Forbidden` were left visible in CloudWatch but had actions disabled so they remain diagnostic-only unless promoted again.
- Retain a deployment log or release note for each pre-meeting push.

### 7. Backend Business Logic Soundness

These items are required for production sign-off even though the endpoints and tests already exist.

- Run one full reference-data import against production using `POST /api/workspace/reference-data/import` with an explicit `ImportDataPath`, or complete the equivalent documented bootstrap through the QuickBooks/admin flow if that is the chosen operator path.
- After the import, verify every enterprise exposed by `/api/workspace/snapshot` has viable customer counts, current rate, total cost, projected volume, and projection rows that match the intended live baseline.
- Confirm break-even figures are realistic by comparing `CurrentRate`, `TotalCosts`, and `ProjectedVolume` from `/api/workspace/snapshot` against the expected calculation for at least Water Utility and Wiley Sanitation District.
- Confirm scenario impact is realistic by loading at least one saved scenario per enterprise and checking that scenario totals and resulting reserve/rate narratives remain plausible for council-facing review.
- Confirm projection outputs are realistic by spot-checking the returned `ProjectionRows` against the imported baseline and the known operating assumptions used by the existing widget calculations.
- Treat the current integration tests as regression coverage, not production evidence: `WorkspaceSnapshotApiTests`, `WorkspaceReferenceDataApiTests`, `WorkspaceKnowledgeApiTests`, `WorkspaceAiApiTests`, and `WorkspaceKnowledgeServiceTests` should stay green, but production still requires manual live-data confirmation.
- Confirm Jarvis knowledge and chat answers are grounded in the live snapshot after the import by checking both `/api/workspace/knowledge` and a non-onboarding `/api/ai/chat` turn for the same enterprise/fiscal-year context.
- Production is not ready while Jarvis is still returning deterministic or legacy fallback responses for normal chat turns. Release evidence should show at least one non-onboarding response with `UsedFallback=false` and content that reflects current snapshot values.

## Code-Complete Closures

These original plan items are now closed in the repository and should not be treated as open implementation work anymore.

- CORS is now configuration-driven in [WileyCoWeb.Api/Program.cs](../WileyCoWeb.Api/Program.cs) and [WileyCoWeb.Api/appsettings.json](../WileyCoWeb.Api/appsettings.json), including the town domains.
- The thin API exposes `/api/workspace/snapshot`, `/api/workspace/knowledge`, `/api/ai/chat`, `/api/workspace/reference-data/import`, scenario endpoints, and utility-customer CRUD in the current repo.
- Archived snapshot export creation is currently accepted through the API only, backed by `PostSnapshotExports_PersistsArtifacts_AndDownloadEndpointReturnsBinaryContent` in `WorkspaceSnapshotApiTests`. The shipped workspace shell has no archived-export trigger today; if product adds one later, treat it as separate user-facing scope and add browser proof then.
- The workspace knowledge layer is implemented in code and is shared by the API, the Decision Support rail, and Jarvis.
- The production migration and Aurora recovery path is documented in [docs/aurora-postgresql-reset-runbook.md](aurora-postgresql-reset-runbook.md) with supporting scripts under [Scripts](../Scripts).
- API Gateway `w544vrvb3i` is now clearly documented as an xAI proxy only, not the workspace API backend.

## Current Deployment Status And Remaining Open Items

These sections keep the deployed state current and separate true open items from items that are now closed.

### 1. Amplify Public Cutover Is Verified

Current state:

- App Runner is serving `/health`, `/api/workspace/snapshot`, `/api/workspace/knowledge`, and `/api/workspace/reference-data/import` as expected.
- The Syncfusion license has been moved out of Amplify environment variables and into the Amplify Gen 1 secret path.
- Amplify app `d2ellat1y3ljd9` is Git-connected to `https://github.com/Bigessfour/wiley-co-web`, and production job `43` successfully deployed commit `8620cf72f569058a5c72619e9e341e25fb0b34f1` from `main`.
- The public build now carries the current workspace configuration and the shipped QuickBooks uploader refactor.
- Hosted browser proof is complete against `https://main.d2ellat1y3ljd9.amplifyapp.com`: `Workspace_QuickBooksImportPanel_PreviewsUploadedFile` and `Workspace_QuickBooksImportPanel_AssistantAnswersQuestion_ForLoadedPreview` both passed on 2026-04-16.

Ongoing action:

- Keep public-site validation against `/api/workspace/snapshot`, `/api/workspace/knowledge`, and the workspace shell in release smoke coverage.
- Keep the hosted QuickBooks preview and assistant flow in future Amplify release validation so public regressions are caught immediately.

### 2. Production Reference Data Remains External To The App Runner Image

Current state:

- The current repository and production API are now aligned on policy: production requires an explicit reference-data path and does not assume a bundled `Import Data` folder.
- `WileyCoWeb.Api/appsettings.json` sets `WorkspaceReferenceData:RequireExplicitImportDataPath=true`.
- The live App Runner route returns `400` with the missing-folder message when called without an explicit path or uploaded files, which confirms the container does not currently ship the repo-local sample set.
- Monthly clerk imports are already handled through the QuickBooks Import panel and the API commit flow into Aurora.
- A production multipart import was executed on 2026-04-22 using uploaded bootstrap files (`Full_Customers.xlsx WSD.xlsx`, `Full_GeneralLedger_FY2026.xlsx Util.xlsx`, and `Full_GeneralLedger_FY2026xlsx WSD.xlsx`) with `includeSampleLedgerData=true` and `applyDefaultEnterpriseBaselines=true`. The response reported `updatedEnterpriseCount=2`, `importedUtilityCustomerCount=10`, and duplicate-ledger skipping, which proves the admin route works with uploaded files even though the current attached seed subset only carries two discovered enterprise sources.
- Public runtime checks now show that `/api/workspace/snapshot` exposes all four intended enterprises (`Apartments`, `Trash`, `Water Utility`, and `Wiley Sanitation District`) with current rate, total cost, projected volume, projection rows, and scenario items for `FY2026`.
- The current attached `Import Data/` folder contains duplicate report variants for at least three basenames (`Full_GeneralLedger_FY2026.xlsx Util`, `Full_GeneralLedger_FY2026xlsx WSD`, and `Full_TransactionList_ByDate_All`), so the folder needs a first-pass sort before the admin bootstrap runs.
- `WorkspaceReferenceDataImportService` already prefers `.xlsx` workbooks and groups sample-ledger files by basename before commit, but the operator should still curate one canonical workbook or report per dataset before the first production seed to avoid avoidable duplicate or stale inputs.

Ongoing action:

- Keep recurring monthly imports on the QuickBooks panel and API commit path.
- Treat `Import Data/` as a developer or admin bootstrap set only.
- Before the first production bootstrap, sort the import folder down to one canonical customer workbook and one canonical ledger file per dataset, preferring the `.xlsx` versions that match the current parser expectations.
- Retain the 2026-04-22 production import response summary as release evidence, then recheck `/api/workspace/snapshot`, `/api/workspace/knowledge`, `/api/utility-customers`, and the hosted workspace shell whenever the bootstrap set changes.
- After the import, record manual spot-checks for break-even, scenario impact, and projection realism so the live baseline numbers are explicitly signed off instead of inferred from automated tests.
- After the import, run one non-onboarding Jarvis chat turn in the same enterprise/fiscal-year scope and retain evidence that it returns promptly with `UsedFallback=false` before treating production chat as council-ready.
- If production needs repeatable reference-data bootstrap, provide it through an explicit path or managed source such as S3 plus an admin-only import job, rather than baking files into the App Runner image.

### 3. Aurora Encryption Cutover And Rollback Retirement Are Complete

Current state:

- App Runner runtime secret `wiley-widget/api/database-url` now points at encrypted cluster `wiley-co-aurora-db-encrypted`.
- Snapshot `wiley-co-aurora-db-preenc-20260416-2340` was restored into encrypted Aurora PostgreSQL cluster `wiley-co-aurora-db-encrypted` in the same private subnet and security-group posture, with `StorageEncrypted=true` and `HttpEndpointEnabled=true`.
- Post-cutover validation on 2026-04-16: App Runner deployment `106a3bc87c7747da933511eb22e29eba` succeeded, `/health` returned `200 Healthy`, `/api/workspace/snapshot` returned populated enterprise options and projections, and live session checks showed traffic on the encrypted cluster while the original cluster remained idle.
- As of 2026-04-17, `aws rds describe-db-clusters --db-cluster-identifier wiley-co-aurora-db` returns `DBClusterNotFoundFault`, and `aws rds describe-db-instances` shows no instances for `DBClusterIdentifier=='wiley-co-aurora-db'`.

Current handling:

- Treat `wiley-co-aurora-db-encrypted` as the only live Aurora target in future debugging, release validation, and runbooks.

## Operational Decisions And Records Through April 17, 2026

### Reference-Data Source Policy

- Do not bundle the repo-local `Import Data/` folder into the App Runner image.
- Keep `Import Data/` as a bootstrap/admin dataset for local seeding, diagnostics, and one-time environment initialization.
- Use the QuickBooks Import panel and API commit flow for recurring monthly analysis files.
- Complete one first-time admin bootstrap import from a curated `Import Data/` folder because production currently has enterprise data but incomplete customer seeding.
- Before that first bootstrap, remove duplicate CSV/XLSX report variants and keep one canonical workbook per dataset, preferring `.xlsx` where the same report exists in multiple formats.
- If production requires centralized bootstrap data, move the curated seed files to an explicit managed source such as S3 and invoke the reference-data import with an explicit path or background job.

### Aurora Encryption Cutover Record

1. Captured manual snapshot `wiley-co-aurora-db-preenc-20260416-2340` from source cluster `wiley-co-aurora-db`.
2. Restored encrypted target `wiley-co-aurora-db-encrypted` in `us-east-2` with the same private subnet group `wiley-co-aurora-subnets`, security group `sg-0cacdba1850b420f7`, and Aurora PostgreSQL `14.17` engine version.
3. Created writer instance `wiley-co-aurora-db-encrypted-1` and enabled the Aurora Data API with `aws rds enable-http-endpoint`.
4. Validated parity on key tables (`Enterprises`, `BudgetEntries`, `MunicipalAccounts`, `UtilityCustomers`, `budget_snapshots`, `budget_snapshot_artifacts`, `ledger_entries`) before cutover.
5. Rotated App Runner secret `wiley-widget/api/database-url` to the encrypted writer endpoint and started deployment `106a3bc87c7747da933511eb22e29eba`, which completed successfully.
6. Verified post-cutover runtime behavior with `200 Healthy` on `/health`, populated data from `/api/workspace/snapshot`, and active-session checks showing live traffic on the encrypted cluster while the original cluster stayed idle.
7. Confirmed on 2026-04-17 that the original unencrypted cluster `wiley-co-aurora-db` is no longer present, so rollback-cluster retirement is complete.

### Observability Follow-Up

- Keep the current X-Ray startup configuration in place for the present App Runner deployment, but treat it as legacy carry-forward rather than a release sign-off target.
- Treat live trace emission as a post-release infrastructure verification item, not a release blocker, until a Wiley service node appears in X-Ray after controlled traffic.
- Current investigation result: the API host initializes the .NET X-Ray SDK, but the repo contains no X-Ray daemon address or daemon-side configuration, and AWS states that the .NET SDK generates and sends trace data to the X-Ray daemon. This makes the current App Runner deployment a poor place to invest more pre-release time unless a deliberate supported delivery path is added.
- Create a post-release work item to migrate service instrumentation from the legacy X-Ray SDK chain to OpenTelemetry rather than deepening dependency on the 2026 maintenance-mode path.

## Post-Release Infrastructure Track

These items are intentionally scheduled after the current widget proof closeout so they are owned as infrastructure follow-up work rather than left as implied technical debt.

- Observability migration: replace or supersede the current X-Ray SDK path with OpenTelemetry, and only keep X-Ray if a supported, proven delivery path is required during the transition.
- Archived export UX follow-up: if product later adds a workspace-shell trigger for archived snapshot exports, treat it as new user-facing scope and add browser proof rather than reopening the current API-only release acceptance.
- Public cutover hardening: rerun the hosted QuickBooks assistant browser flow and other public-shell regression checks on future Amplify releases.
- Public cutover hardening: treat boot-manifest and `WileyCoWeb` fingerprint verification as part of release validation so future public E2E failures can be distinguished from stale-browser deployments immediately.

### App Runner Replacement Review

- Short term: keep App Runner for the current thin API because it is already live, VPC-attached, and validated.
- Trigger a formal replacement review before any broad production expansion because AWS App Runner is closed to new customers after April 30, 2026 even though existing customers can continue operating.
- Compare at least these targets: App Runner as-is, ECS Fargate service behind an ALB, and Lambda only if the API surface is intentionally reduced and cold-start behavior is acceptable.
- Evaluate each option on private Aurora connectivity, secrets handling, deployment workflow, cold-start and steady-state latency, CloudWatch/X-Ray support, and operational cost.
- Current direction: plan ECS Fargate as the likely successor if Wiley needs a long-lived supported path beyond the current App Runner footprint.

## Recommended Delivery Sequence

### Phase 1. Runtime Foundation

Target: completed for the current App Runner and encrypted Aurora runtime path.

- Keep App Runner on the current repo revision.
- Keep VPC connectivity, secrets, and API security group access intact.
- Validate `/health`, `/api/workspace/snapshot`, `/api/workspace/knowledge`, `/api/ai/chat`, and `/api/workspace/reference-data/import` from the public hostname.

Exit criteria:

- API host responds successfully.
- API can connect to Aurora and expose the current repo routes.
- No degraded-mode startup in production.

### Phase 2. Widget-To-API Connection

Target: completed for the current public Amplify deployment.

- Keep Amplify pointed at the App Runner URL or final API DNS.
- Keep future Amplify releases on the tracked GitHub branch copy of the corrected pinned-SDK [amplify.yml](../amplify.yml).
- Verify the generated workspace config file remains present in each public build.

Exit criteria:

- Browser calls `/api/workspace/snapshot` successfully from the public site.
- Browser calls `/api/workspace/knowledge` successfully from the public site.
- Decision Support rail loads live knowledge, not fallback-only status.

### Phase 3. Data And AI Validation

Target: core validation is complete; keep this as recurring release-proof coverage.

- Validate baseline save/load against Aurora.
- Validate scenario save/list/apply against Aurora.
- Validate QuickBooks import preview and commit against canonical tables.
- Validate `/api/ai/chat` and recommendation history.
- Validate the hosted QuickBooks preview and assistant browser flow on the public site.
- Run one first-time admin reference-data bootstrap from the curated import folder because public runtime checks still show incomplete customer seeding.
- Validate server-side exports if Council packet generation is in scope.

Exit criteria:

- Water, Sewer, Trash, and Apartments each load live enterprise context.
- Scenario changes rehydrate the widget correctly.
- Utility customer data is fully seeded and visible through `/api/utility-customers` and the workspace snapshot filters.
- Jarvis and Decision Support return live, matching financial context.

### Phase 4. Go-Live Hardening

Target: complete before the Council rehearsal week.

- Verify that no Wiley App Runner or Aurora alarms exist yet, then create the missing CloudWatch alarms for App Runner `5xx` rate, latency, unhealthy instances, and Aurora CPU/connections.
- Test the notification target for those alarms and retain the alarm names or dashboard link in the release notes.
- Record a release checklist and rollback path.
- Keep the raw App Runner hostname as the current API baseline; do not make custom-domain cutover a pre-meeting blocker.
- Treat `api.wileywidget.townofwiley.gov` as a separate follow-up release once the raw-host baseline, monitoring, and first-time seed import are stable.
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
- Keep the raw App Runner hostname as the active API URL for now and only schedule custom-domain cutover as a separate follow-up.
- Add monitoring and alarms for App Runner and Aurora.

Application owner:

- Confirm production env vars disable degraded mode.
- Keep the migration runbook current and use it as the release path.
- Validate API endpoints and live widget flows.
- Curate the `Import Data/` folder, remove duplicate report variants, and run one first-time admin bootstrap import to complete customer/reference seeding.
- Keep the town-clerk import procedure aligned with the live QuickBooks import path in [docs/quickbooks-desktop-import-guide.md](docs/quickbooks-desktop-import-guide.md).

Release owner:

- Keep Amplify releases flowing from the tracked GitHub branch copy of the corrected pinned-SDK [amplify.yml](../amplify.yml).
- Keep `WILEY_WORKSPACE_API_BASE_ADDRESS` pointed at `https://mr7zeizxxd.us-east-2.awsapprunner.com` until a separate custom-domain release is approved.
- Run end-to-end smoke tests on each release before the Council meeting.

## Immediate Next Action

The next server-side moves are now concrete: add the missing CloudWatch alarms for App Runner and Aurora, keep the release note and rollback path current, curate the attached `Import Data/` folder and run one first-time admin bootstrap import to complete customer/reference seeding, and preserve the hosted browser validation sequence (`/health`, `/api/workspace/snapshot`, `/api/workspace/knowledge`, `/api/ai/chat`, `/api/workspace/reference-data/import`, `/api/utility-customers`, plus the QuickBooks assistant/browser smoke) on future Amplify releases. Keep the raw App Runner hostname as the current API baseline. Observability migration and custom-domain cutover remain follow-up infrastructure work.
