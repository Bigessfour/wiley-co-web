# Post-Production Operations Handbook

This handbook is the primary day-two operations document for Wiley Widget after production deployment.

Use this document for:

- normal production releases
- post-release validation
- monitoring and alert triage
- Jarvis and data-import operational checks
- release evidence and rollback planning

Use the linked specialist runbooks for narrow tasks:

- database recovery: `docs/aurora-postgresql-reset-runbook.md`
- recurring clerk imports: `docs/quickbooks-desktop-import-guide.md`
- current AWS deployment state and infrastructure decisions: `docs/aws-server-side-closure-plan.md`
- release evidence: `docs/release-record-template.md`
- secret and runtime configuration changes: `docs/secrets-and-config-rotation-runbook.md`
- incident severity and escalation guidance: `docs/incident-response-matrix.md`
- hosting successor decision: `docs/app-runner-successor-review.md`
- ownership and review cadence: `docs/operations-governance-and-review-cadence.md`

## 1. Production Scope

The live production path currently depends on these services:

- Amplify hosts the static Blazor WebAssembly client.
- App Runner hosts the thin ASP.NET Core API.
- Aurora PostgreSQL is the system of record.
- Secrets Manager holds runtime secrets for database access, xAI, and Syncfusion.
- API Gateway `w544vrvb3i` is the xAI proxy path used by Jarvis.
- CloudWatch Logs and CloudWatch Alarms are the current operational monitoring surfaces.

Current platform note:

- App Runner remains the active production host.
- AWS states App Runner is closed to new customers after April 30, 2026, while existing customers can continue operating normally.
- Treat App Runner as the current production platform, but keep the successor review active as a standing infrastructure follow-up.

## 2. Operating Principles

- Do not treat repository history as the operating handbook. Use this document first.
- Do not treat the App Runner image as storage for recurring import files.
- Do not rotate secrets or change runtime endpoints without a corresponding App Runner deployment or service update.
- Do not accept deterministic or legacy Jarvis fallback as normal production success for council-facing use.
- Do not make production changes without capturing release evidence and a rollback target.

## 3. Daily, Weekly, And Monthly Cadence

### Daily

- Confirm `/health` returns healthy from the public App Runner hostname.
- Review active CloudWatch alarms for App Runner, Aurora, and Jarvis/xAI diagnostics.
- Review recent application logs for `Workspace AI request completed` events with unusual failure codes or elevated elapsed time.

### Weekly

- Review App Runner request latency and 5xx trends.
- Review Aurora CPU and connection trends.
- Review Jarvis completion outcomes by `AnswerSource`, `UsedFallback`, and `FailureCode`.
- Confirm the current release record is complete for the latest production deployment.

### Monthly

- Execute the normal QuickBooks import flow through the upload panel.
- Retain import evidence: operator, enterprise, fiscal year, filenames, counts, and follow-up validation results.
- Reconfirm that workspace snapshot, workspace knowledge, and one non-onboarding Jarvis turn still reflect current live data.
- Review runtime sizing and alarm thresholds against the last month of traffic and import activity.

## 4. Normal Release Workflow

### Current release path

The current API release path is image-based:

- build the API image from `WileyCoWeb.Api/Dockerfile`
- push the image to `570912405222.dkr.ecr.us-east-2.amazonaws.com/wiley-widget-api`
- App Runner auto-deploys when a new image version is pushed to the watched ECR repository because `AutoDeploymentsEnabled=true`

AWS App Runner deployment note:

- Automatic and manual deployment produce the same service result.
- With automatic deployment enabled, a new image push is enough to start rollout.
- Manual deployment should only be used when the repository has a deployable version that should not wait on the normal trigger.

### Release evidence to capture

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

Use `docs/release-record-template.md` to keep this consistent across releases.

### Recommended best practice improvement

The current service watches `latest`, but production support should not rely on `latest` alone.

Best-practice release policy:

- continue publishing `latest` if needed for the current service contract
- also publish an immutable version tag for every release
- record the immutable image digest in the release record
- use the immutable tag or digest as the rollback target

## 5. Post-Deploy Validation

After every production deployment, validate in this order:

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

## 6. Rollback Guidance

Rollback must be prepared before release, not invented during incident response.

Current best-practice rollback path:

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
