# Post-Production Operations Handbook

This handbook is the day-two operations document for Wiley Widget.

The live path is the office Mac plus Synology Postgres, defined in `docs/production-completion-plan.md` (2026-11-14). Amplify app `d2ellat1y3ljd9`, App Runner, Aurora, and Cognito pool `us-east-2_DmY7BCBIp` were removed. Sections and linked runbooks that still give commands for those hosts are historical.

Use this document for:

- starting and checking the clerk Mac path
- QuickBooks import checks
- what to record when the running commit changes

Use these for the live path:

- definition of ready: `docs/production-completion-plan.md`
- clerk and operator steps: `docs/clerk-mac-runbook.md`
- where the suite runs: `docs/aws-low-cost-architecture.md` (the NAS-first cutover sketch)
- Town Hub card URLs: `docs/town-hub.md`
- recurring clerk imports: `docs/quickbooks-desktop-import-guide.md`

These are historical records of the removed host. Do not follow them to deploy:

- `docs/aurora-postgresql-reset-runbook.md`
- `docs/aws-server-side-closure-plan.md`
- `docs/aws-aurora-private-layout.md`
- `docs/app-runner-successor-review.md`
- `docs/secrets-and-config-rotation-runbook.md` (App Runner secret rotation)
- `docs/backend-production-signoff-checklist.md`

`docs/incident-response-matrix.md` still uses App Runner examples. Until it is rewritten, treat severity names as the process and ignore the host names.

## 1. Production scope

The live path is:

- Blazor client on the office Mac at `http://127.0.0.1:5230`, started by `./Scripts/start-clerk-local.sh`
- API on the same Mac at `http://127.0.0.1:5231`
- Synology Community PostgreSQL 17 at `mr-storage:5433`, database `wileyco_dev`
- Town Hub links: Apartments `http://mr-storage:8082`, TIKR `http://mr-storage:8080`, Trash Truck on its existing Amplify app, Rural Water disabled
- Staff login off until a new Cognito pool exists on the Town account

Do not recreate App Runner, Aurora, or ECS for Widget.

## 2. Operating principles

- Follow this handbook and `docs/production-completion-plan.md`. Do not treat the historical AWS pages as the current deploy path.
- Do not put recurring import files in a container image. Clerks import through the QuickBooks panel.
- Do not point Production auth settings at Cognito pool `us-east-2_DmY7BCBIp`.
- Do not accept a Jarvis fallback as a normal council answer.
- Record the git commit when the Mac processes are restarted onto a new build.

## 3. Daily, weekly, and monthly cadence

### Daily

- Run `./Scripts/status-clerk-local.sh`.
- Confirm `http://127.0.0.1:5231/health` returns healthy and the Water Utility snapshot probe returns 200.
- If either check fails, restart with `./Scripts/start-clerk-local.sh`. That script leaves NAS Postgres running.

### Weekly

- Confirm Tailscale still reaches `mr-storage:5433`.
- Read `/tmp/wiley-clerk-local/api.log` (or the directory in `WILEY_CLERK_STATE_DIR`) for repeated Jarvis or database errors.

### Monthly

- Run the QuickBooks import through the upload panel.
- Keep the operator, enterprise, fiscal year, filenames, and row counts.
- Open one workspace snapshot and confirm the figures match the import.

## 4. How a new build reaches the clerk Mac

### Current release path

There is no hosted deploy.

1. Merge to `main` only through a pull request whose CI is green.
2. On the office Mac, pull that commit.
3. Run `./Scripts/stop-clerk-local.sh`, then `./Scripts/start-clerk-local.sh`.
4. Record the date, the operator, the commit SHA, `/health`, and the snapshot probe.

Rollback is the previous git commit, restarted with the same two scripts. Database schema changes still need a backup before migrate. The 2026-11-14 dump and the unfinished restore are in `docs/clerk-mac-runbook.md`.

### Historical: App Runner image release (removed 2026-11)

The old API release path was image-based:

- build the API image from `WileyCoWeb.Api/Dockerfile`
- push the image to `570912405222.dkr.ecr.us-east-2.amazonaws.com/wiley-widget-api`
- App Runner auto-deploys when a new image version is pushed to the watched ECR repository because `AutoDeploymentsEnabled=true`

AWS App Runner deployment note:

- Automatic and manual deployment produce the same service result.
- With automatic deployment enabled, a new image push is enough to start rollout.
- Manual deployment should only be used when the repository has a deployable version that should not wait on the normal trigger.

The subsections from here through the old rollback steps record the removed App Runner release. Do not run them.

### Historical: release evidence to capture

For every production API deployment, record:

- date and operator
- git commit SHA
- ECR image tag and image digest
- App Runner operation ID
- App Runner service status after rollout
- `/health` result
- smoke-check results for `/api/workspace/snapshot`, `/api/workspace/knowledge`, and `/api/ai/chat`
- alarm state before and after deploy
- rollback target

### Amplify / App Runner Cutover & Council Smoke Commands (ops only, no secrets)

**Cutover checklist (env-driven):**

- Set `WILEY_WORKSPACE_API_BASE_ADDRESS` in Amplify (points client to App Runner public URL or custom domain).
- Deploy Amplify with the env var present (amplify.yml + build already supports it for `appsettings.Workspace.local.json`).
- Town Hub suite cards: Production URLs point Apartments and TIKR at the Synology over Tailscale (`http://mr-storage:8082` and `http://mr-storage:8080`). Those cards say office network only and do not claim this sign-in carries over. Trash Truck uses the shared Cognito sign-in. Rural Water with an empty URL shows Coming soon. `Clerk` and `admin` see the clerk cards; `Driver` sees Trash Truck only. Amplify `TOWN_HUB_*_URL` overrides still replace those values when set. Detail: `docs/town-hub.md`.
- App Runner service: update runtime env/secrets via console or IaC (database-url, xai-api-key, syncfusion-license-key); trigger deploy from ECR image.
- Post-cutover validation (council smoke, run from ops workstation or CI):
  ```powershell
  dotnet build WileyCoWeb.csproj
  dotnet build WileyCoWeb.Api/WileyCoWeb.Api.csproj
  dotnet test tests/WileyCoWeb.ComponentTests --filter "Category=HighRisk"
  dotnet test tests/WileyCoWeb.IntegrationTests --filter "Category=HighRisk"
  dotnet test tests/WileyWidget.Tests --filter "Category=HighRisk"
  npm run playwright:test:ci:highrisk
  curl -s https://<app-runner-host>/api/ai/health | jq '.latestUsedFallback, .status'
  curl -s https://<app-runner-host>/api/jarvis/health | jq '.latestUsedFallback, .status'
  ```
- Secret/SSM rotation: use `docs/secrets-and-config-rotation-runbook.md` (never commit values; xAI primary now SSM Parameter Store with code fallback or direct App Runner env bind; Secrets Manager for other secrets; always trigger App Runner deploy after change).
- Rollback target: prior ECR image tag + prior Amplify version (record in release evidence template).

All steps are ops-only; no secrets in source or this handbook.

Use `docs/release-record-template.md` to keep this consistent across releases.

### Recommended best practice improvement

The current service watches `latest`, but production support should not rely on `latest` alone.

Best-practice release policy:

- continue publishing `latest` if needed for the current service contract
- also publish an immutable version tag for every release
- record the immutable image digest in the release record
- use the immutable tag or digest as the rollback target

## 5. Post-restart validation

After `./Scripts/start-clerk-local.sh`, the script already probes `/health` and the Water Utility snapshot. Confirm both succeeded, then open `http://127.0.0.1:5230` and check Town Hub.

### Historical: App Runner post-deploy validation

After every production deployment, the old order was:

1. App Runner service status and latest deployment operation.
2. Public `/health` endpoint.
3. `/api/workspace/snapshot` for expected enterprise and fiscal-year data.
4. `/api/workspace/knowledge` for live server-backed knowledge output.
5. One non-onboarding `/api/ai/chat` turn for the same enterprise and fiscal year.
6. Hosted browser shell smoke if the release affected client behavior.

Minimum success criteria:

- `/health` is healthy
- no critical runtime alarms are active
- snapshot and knowledge endpoints return current expected data
- Jarvis returns a non-onboarding response without timeout
- Jarvis evidence shows `UsedFallback=false` for at least one normal validation turn when the release is intended to improve live chat quality

## 6. Rollback

On the clerk Mac, check out the previous commit and run `./Scripts/stop-clerk-local.sh` then `./Scripts/start-clerk-local.sh`. If that commit included a database migration, restore the NAS backup taken before the migrate. That backup procedure is not written yet.

### Historical: App Runner rollback

The old rollback path was:

- identify the previous known-good image digest or immutable version tag
- redeploy that image to the App Runner service
- confirm `/health`, snapshot, knowledge, and chat validation again
- record the rollback operation ID and reason

If the current release still uses `latest` as the only visible tag, the operator should first recover the previous known-good digest from ECR or release records before initiating rollback.

Rollback triggers include:

- sustained App Runner 5xx errors after release
- failed health checks or unhealthy instances
- broken snapshot or knowledge responses
- Jarvis regression beyond accepted release scope
- import or database regression tied to the release

## 7. Monitoring And Logs

### CloudWatch and App Runner logging model

AWS App Runner streams logs to CloudWatch Logs in two groups per service:

- service log group for lifecycle, event, and deployment activity
- application log group for the running application output

App Runner service logs are the first place to inspect:

- deployment failures
- unhealthy instance events
- configuration problems

Application logs are the first place to inspect:

- Jarvis request outcomes
- import and API exceptions
- runtime configuration issues surfaced by the app

### Wiley alarm set

Current alarms documented for the production runtime include:

- `WileyCo-AppRunner-5xxRatePercent`
- `WileyCo-AppRunner-RequestLatencyHigh`
- `WileyCo-Aurora-CPUHigh`
- `WileyCo-Aurora-ConnectionsHigh`
- `WileyCo-Grok-AuthOrKeyFailures`
- `WileyCo-Grok-EndpointFailures`
- `WileyCo-Grok-KeyLoadFailures`
- `WileyCo-Grok-NetworkExceptions`
- `WileyCo-Jarvis-ChatFailures`
- `WileyCo-Jarvis-FallbackUsed`
- `WileyCo-Jarvis-Legacy403Forbidden`
- `WileyCo-Jarvis-TlsRevocationFailure`

Operational note:

- `WileyCo-Jarvis-FallbackUsed` and `WileyCo-Jarvis-Legacy403Forbidden` are currently diagnostic-only unless re-promoted to notification actions.

### Jarvis health endpoint

`GET /api/ai/health` (alias: `GET /api/jarvis/health`) returns the latest Jarvis turn metadata:

- `semanticKernelAvailable`
- `latestAnswerSource`
- `latestUsedFallback`
- `latestFailureCode`
- `lastTurnAtUtc`

Use this endpoint during release validation alongside CloudWatch `AnswerSource` queries. A council-ready release should show at least one non-onboarding turn with `latestAnswerSource=semantic_kernel` and `latestUsedFallback=false`.

### Jarvis completion log

Every Jarvis request now emits a completion log with:

- `ConversationId`
- `AnswerSource`
- `UsedFallback`
- `IsFirstConversation`
- `TurnCount`
- `FailureCode`
- `ElapsedMs`

This event is the primary production signal for chat behavior drift.

Expected `AnswerSource` values include:

- `onboarding`
- `semantic_kernel`
- `semantic_kernel_direct_retry`
- `rate_limit_fallback`
- `legacy_xai_fallback`
- `deterministic_fallback`

Healthy target pattern:

- onboarding turns may use onboarding flow by design
- normal production turns should trend toward `semantic_kernel`
- `legacy_xai_fallback` and `deterministic_fallback` should be treated as investigation signals, not success indicators, for council-facing live chat

### Example CloudWatch Logs Insights query

Use this query pattern against the App Runner application log group to review Jarvis completion behavior:

```sql
fields @timestamp, @message
| filter @message like "Workspace AI request completed"
| parse @message /AnswerSource=(?<AnswerSource>[^,]+), UsedFallback=(?<UsedFallback>[^,]+), IsFirstConversation=(?<IsFirstConversation>[^,]+), TurnCount=(?<TurnCount>[^,]+), FailureCode=(?<FailureCode>[^,]+), ElapsedMs=(?<ElapsedMs>[^)]+)\)/
| stats count() as RequestCount, avg(ElapsedMs) as AvgElapsedMs, max(ElapsedMs) as MaxElapsedMs by AnswerSource, FailureCode, UsedFallback
| sort RequestCount desc
```

Use this query pattern when a release touched Jarvis transport, retries, or endpoint configuration.

## 8. Incident First Response

### App Runner health or deployment issue

First checks:

- App Runner service status
- latest App Runner operation and deployment logs
- `/health`
- CloudWatch alarm state

If the issue began immediately after release:

- freeze further production changes
- evaluate rollback using the previous known-good image
- retain deployment and alarm evidence in the release record

Use `docs/incident-response-matrix.md` to classify severity and keep the response consistent.

### Jarvis fallback or timeout issue

First checks:

- recent `Workspace AI request completed` events
- `AnswerSource`, `FailureCode`, and `ElapsedMs`
- xAI-related alarms
- App Runner application logs around the same `ConversationId`
- whether the service is using the intended proxy endpoint and retry settings

If normal turns are using `deterministic_fallback` or `legacy_xai_fallback`:

- treat the release as degraded for live chat quality
- confirm whether the issue is transport, auth, timeout, or data grounding related
- decide on rollback if the release explicitly targeted live Jarvis reliability and materially regressed it

### Import issue

First checks:

- file type and report family
- preview correctness before commit
- duplicate file protection outcome
- enterprise and fiscal-year selection
- post-import snapshot and knowledge validation

For recurring monthly loads, use the QuickBooks panel workflow. Do not move routine imports into ad hoc server-side file placement.

### Database issue

First checks:

- Aurora CPU and connection alarms
- App Runner application logs for database exceptions
- snapshot and knowledge endpoint behavior
- recent import or migration activity

Use `docs/aurora-postgresql-reset-runbook.md` for destructive or migration-alignment recovery. Do not improvise schema reset steps from old SQL artifacts.

## 9. Secrets And Configuration Changes

App Runner environment variables and secret references are managed through the service configuration.

Operational rules:

- sensitive values stay in Secrets Manager or SSM references, not plain text repo files
- least-privilege IAM must remain in place for runtime secret access
- after any runtime secret rotation or environment change, perform an App Runner deployment or service update so the running service picks up the new value
- after rotation, validate `/health`, snapshot, knowledge, and Jarvis before closing the change

Use `docs/secrets-and-config-rotation-runbook.md` for the approved step-by-step procedure.

Examples that require this process:

- database connection secret rotation
- xAI API key rotation
- xAI endpoint or retry setting changes
- Syncfusion runtime secret changes for the API host

## 10. Data Operations

### Recurring monthly imports

Use the QuickBooks Import panel and API commit flow.

Operator checks:

- correct enterprise selected
- correct fiscal year selected
- preview reviewed before commit
- duplicate state understood before retrying uploads

### Bootstrap or admin imports

Bootstrap/reference-data imports are separate from monthly clerk imports.

Rules:

- production requires explicit import sources
- the App Runner image is not the bootstrap file store
- keep one canonical workbook or ledger file per dataset before admin bootstrap
- after bootstrap, validate snapshot, knowledge, utility-customer data, and one non-onboarding Jarvis turn

## 11. Backup, Recovery, And Restore Readiness

Runbook: [backup-and-restore.md](backup-and-restore.md). File-level check: `./Scripts/backup-restore-drill.sh`. Health and the Town Hall contact: [health-and-alerting.md](health-and-alerting.md) and `/support`. Clerk page: [clerk-quick-start.md](clerk-quick-start.md).

Aurora PostgreSQL is the system of record.

Standing rules:

- take and retain appropriate recovery points before destructive schema or data operations
- treat the encrypted Aurora cluster as the only live target
- use the existing Aurora runbook for reset or migration alignment
- keep one tested non-destructive migration path available when direct TCP access is unavailable

Best-practice cadence:

- verify restore readiness quarterly
- retain evidence of the last successful restore or recovery exercise
- update the runbook when the recovery path changes

## 12. Security And Access

- Keep runtime secrets out of source control and client build artifacts unless intentionally browser-exposed.
- Use IAM roles for runtime secret access rather than embedded credentials.
- Keep operator access limited to named deployment and support owners.
- Review alarm actions and notification recipients after team or ownership changes.
- Keep production domain, API host, and data-store changes out of the same release window unless there is a documented rollback plan for the combined change.
- **API JWT scaffolding:** `Authentication:Jwt:Enabled` gates mutating routes and read paths via `RequireWorkspaceReadAuth` when it is true. `/api/ai/health` stays anonymous. The clerk path keeps JWT off. Production files must not name the removed pool `us-east-2_DmY7BCBIp`. When a live Town-account pool exists, turn client `Auth:Enabled` and API `Authentication:Jwt:Enabled` on together and use Hosted UI (`Auth:SignInFlow=HostedUi`, OAuth2 code + PKCE). See `docs/sso-clerksuite.md`. While JWT is off, the API accepts callers with no token, so it stays on `127.0.0.1` on the office Mac.
  - Integration tests prove 401 on protected read endpoints when JWT enabled in test factory (no token).
- **Historical Cognito smoke tests:** these commands targeted the removed pool `us-east-2_DmY7BCBIp` and App Runner. Do not run them. The old notes said IAM user `copilot` had managed policy `WileyWidgetCopilotCognitoSmokeTest`. With `copilot` AWS credentials configured:
  - `pwsh Scripts/get-cognito-smoke-token.ps1` — returns a Bearer access token
  - `pwsh Scripts/smoke-test-jarvis-production.ps1` — exercises `/api/ai/health` and `/api/ai/chat` on App Runner
  - Requires the current API image (Jarvis routes + JWT). Rotate the smoke-test Cognito password via Secrets Manager after use.

## 13. Standing Follow-Up Work

These items are not blockers for day-two support, but they must remain tracked:

- OpenTelemetry successor plan for current observability path
- keep the App Runner successor review current, with ECS Fargate as the default migration direction
- keep release records current for every production deployment
- keep governance and review-cadence ownership current as roles and operating practice change

## 14. Definition Of Post-Production Done

Post-production operations are in good shape when all of the following are true:

- operators can deploy and validate without tribal knowledge
- rollback targets are recorded for every release
- support can locate App Runner, application, and Jarvis evidence quickly
- monthly imports have a stable clerical path
- database recovery has an approved runbook and tested access path
- Jarvis regressions are measurable through logs, alarms, and release validation
- the App Runner successor review stays active until a longer-lived hosting decision is made
