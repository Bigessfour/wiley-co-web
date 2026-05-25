# Backend production sign-off checklist

Use this checklist after deploying API changes to App Runner. Pair with [`docs/release-record-template.md`](release-record-template.md).

## Pre-deploy

- [ ] HighRisk dotnet gate green (Component + Integration + Widget)
- [ ] `npm run playwright:test:ci:highrisk` green locally or in CI
- [ ] `dotnet build WileyCoWeb.csproj` and `dotnet build WileyCoWeb.Api/WileyCoWeb.Api.csproj`
- [ ] Optional: `RUN_POSTGRES_TESTS=true dotnet test tests/WileyCoWeb.IntegrationTests --filter Category=Postgres` (validates EF migrations on PostgreSQL 16)

## Aurora migration (when schema changes ship)

- [ ] Review migration `20260525204607_SchemaAlignmentProductionReadiness` diff
- [ ] Confirm no orphan `Charges` rows (migration aborts if `BillId`/`UtilityBillId` both missing)
- [ ] Apply from VPC-attached host or `./Scripts/apply-aurora-migration-data-api.ps1 -NoBuild`
- [ ] Post-apply schema checks per [`aurora-postgresql-reset-runbook.md`](aurora-postgresql-reset-runbook.md) § Post-Reset Validation

## Post-deploy (App Runner)

Run [`Scripts/verify-apprunner-workspace-api.ps1`](../Scripts/verify-apprunner-workspace-api.ps1) and record:

- [ ] `GET /health` → 200
- [ ] `GET /api/workspace/snapshot` → 200
- [ ] `POST /api/workspace/knowledge` → 200 or explicit 503 when data insufficient
- [ ] Production config: `WorkspacePanels:Fallback` synthetic flags **false** (capital gap / debt coverage return 503 when budget missing)

## Jarvis validation

- [ ] `GET /api/ai/health` → `latestUsedFallback=false` after a live chat turn
- [ ] `POST /api/ai/chat` for real enterprise + FY returns `latestAnswerSource=semantic_kernel`
- [ ] CloudWatch query from [`post-production-operations-handbook.md`](post-production-operations-handbook.md) shows no repeated fallback storms

## Release record

- [ ] Release record completed with commit SHA, ECR image digest, App Runner operation ID
- [ ] Jarvis field documented as **`latestUsedFallback`** (not `UsedFallback`)

## Grok CLI re-run (optional)

To resume backend hardening locally:

```powershell
.\Scripts\run-grok-backend-plan.ps1 -Continue
```

Prompt: [`.grok/prompts/backend-production-readiness.md`](../.grok/prompts/backend-production-readiness.md)
