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
- Amplify app `d2ellat1y3ljd9` and branch `main` now carry `WILEY_WORKSPACE_API_BASE_ADDRESS=https://mr7zeizxxd.us-east-2.awsapprunner.com`.
- App Runner `/health` currently returns `200 Healthy`.
- App Runner `/api/workspace/snapshot` currently returns `200`, but with empty `enterpriseOptions`, empty `selectedEnterprise`, and zeroed financials.
- App Runner `/api/workspace/knowledge` currently returns `500`.
- App Runner `/api/ai/chat` currently returns `200` with a fallback onboarding response.
- App Runner `/api/workspace/reference-data/import` currently returns `404`, even though that route exists in the repository, which indicates the deployed App Runner revision is behind the current repo code.
- Amplify release job `32` failed because the build used a floating `.NET 9` SDK while the cloned repo required the exact `global.json` version.
- The workspace now contains the corrected [amplify.yml](../amplify.yml) that installs the pinned SDK version, but release job `33` confirmed that Amplify is still building from the tracked GitHub branch copy of `amplify.yml`. The branch used for production releases must carry the same file change before the public release can succeed.

Production conclusion: the browser shell is deployed, the thin API runtime infrastructure exists, and the major server-side seams are complete in the repo. The remaining blockers are live deployment drift, missing production bootstrap data, and final Amplify/App Runner cutover verification.

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
- [x] Validate `/health` from the public App Runner hostname.
- [x] Validate `/api/ai/chat` from the public App Runner hostname.
- [ ] Validate `/api/workspace/snapshot` with live enterprise bootstrap data instead of the current empty-enterprise response.
- [ ] Validate `/api/workspace/knowledge` without the current `500` failure.
- [ ] Validate that the deployed App Runner revision includes `/api/workspace/reference-data/import` and the other current repo routes.
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

### Gap 1. App Runner Runtime Drift

Current state:

- App Runner exists and `/health` succeeds.
- The deployed service appears to be behind the current repository revision because `/api/workspace/reference-data/import` returns `404` even though the route exists locally.
- `/api/workspace/knowledge` still fails with `500` in the live runtime.

Closure action:

- Redeploy the current API image/revision to App Runner.
- Re-run validation for `/health`, `/api/workspace/snapshot`, `/api/workspace/knowledge`, `/api/ai/chat`, and `/api/workspace/reference-data/import` from the public hostname.

### Gap 2. Amplify Public Cutover Is Not Yet Verified

Current state:

- The branch environment points at the App Runner URL.
- The workspace copy of [amplify.yml](../amplify.yml) has been corrected so Amplify installs the SDK pinned in `global.json` instead of a floating channel version.
- Public cutover is still incomplete because the tracked GitHub branch used by Amplify has not yet published that same `amplify.yml` change, so release job `33` still executed the old floating-SDK prebuild commands.

Closure action:

- Publish the corrected [amplify.yml](../amplify.yml) into the GitHub branch that Amplify builds from, then complete a successful Amplify release.
- Verify the public site publishes `wwwroot/appsettings.Workspace.local.json` and routes the browser client to App Runner instead of the Grok gateway.

### Gap 3. Production Bootstrap Data Is Still Missing

Current state:

- `/api/workspace/snapshot` returns successfully from App Runner, but the payload has no enterprise options and no selected enterprise.
- That means the public analysis experience still lacks the enterprise baseline data required to power the workspace, even when the API is reachable.

Closure action:

- Seed or import the required enterprise baseline and fiscal-year budget data after schema alignment.
- Re-run `/api/workspace/snapshot` and `/api/workspace/knowledge` validation against a non-empty enterprise scope.

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