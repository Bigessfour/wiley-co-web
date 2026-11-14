# Wiley Widget production completion plan

Ordered from first work to last. Do the phases in sequence.

Status date: 2026-11-14.

## Definition of production ready

This is the decision. A Widget page that names Amplify app `d2ellat1y3ljd9`, App Runner `wiley-widget-api`, Aurora `wiley-co-aurora-db`, or Cognito pool `us-east-2_DmY7BCBIp` as the live system is a historical record from before November 2026.

Production ready means all of the following:

1. A clerk starts the app on the office Mac with `./Scripts/start-clerk-local.sh`. The client listens on `http://127.0.0.1:5230` and the API on `http://127.0.0.1:5231`.
2. The database is Synology Community PostgreSQL 17 on `mr-storage:5433`, database `wileyco_dev`, user `wiley_app`.
3. Town Hub opens Apartments and TIKR on that NAS, Trash Truck on its existing Amplify app, and Rural Water stays disabled until it has a URL.
4. Staff login stays off on the clerk Mac. The Town account already has pool `Townofwiley-staff` (`us-east-2_bHk9UcenK`), used by the town website admin login, with MFA off and a `Staff` group. Do not create a second pool, and do not point Widget at that pool until the sign-in flow and groups are chosen. Production config must not name the removed pool `us-east-2_DmY7BCBIp`.
5. Widget is not put back on App Runner, Aurora, or ECS.
6. Town AWS spend stays the small footprint already described in `docs/aws-low-cost-architecture.md`: `townofwiley.gov`, Trash Truck Amplify, and the Terraform budgets, contracts bucket, and empty ECR repos. Planning figure about $1–3 per month.

Work still required to meet that definition is the numbered list in `docs/production-readiness-gap-report.md`.

## Phase 0. Lock scope

Done. Rate math is decision support (revenue requirement). Issues #27 and #25 closed.

## Phase 1. Auth, before any new production image

Historical. The pool and client in this phase were removed with the hosted stack. See the definition above and the clerk-path note that production auth stays off until a new Town-account pool exists.

[#15](https://github.com/Bigessfour/wiley-co-web/issues/15) — staff Cognito login. **Merged** via PR #28.

Decision: staff Cognito only. No public-read exception. Pool `us-east-2_DmY7BCBIp`, app client `2m6vp91m9938jpbg2efivr2p8k` (no client secret).

| Piece                    | Status                                                                                                                      |
| ------------------------ | --------------------------------------------------------------------------------------------------------------------------- |
| API Production JWT       | `Authentication:Jwt:Enabled=true`                                                                                           |
| Read auth gaps           | Customers GET, scenarios list/get, capital-gap/debt-coverage POST, `/api/ai/recommendations` use `RequireWorkspaceReadAuth` |
| Health anonymous         | `/api/ai/health` and `/api/jarvis/health` stay open                                                                         |
| Blazor staff login       | `/authentication/login` via Cognito `USER_PASSWORD_AUTH`                                                                    |
| Bearer on API calls      | `CognitoAuthorizationMessageHandler` when `Auth:Enabled`                                                                    |
| Route gate               | `AuthorizeRouteView` + FallbackPolicy when auth enabled                                                                     |
| Production client config | `wwwroot/appsettings.Production.json` → `Auth:Enabled=true`                                                                 |
| Local override           | `Scripts/generate-auth-appsettings.sh` → `wwwroot/appsettings.Auth.local.json`                                              |
| Dev                      | `Auth:Enabled=false` keeps workspace open for local/tests                                                                   |

Ops before next hosted publish: confirm Cognito pool/client allow `USER_PASSWORD_AUTH`; create staff users; prove anonymous 401 and signed-in workspace load.

## Phase 2. Prove `grok-4.7`, then delete the dead stack

[#16](https://github.com/Bigessfour/wiley-co-web/issues/16), then [#24](https://github.com/Bigessfour/wiley-co-web/issues/24)

**Decision:** Jarvis, QuickBooks assistant, and the model default are `grok-4.7`. Blank or retired ids (`grok-4-1-fast-reasoning`, `grok-4.1`, `grok-4.20-0309-reasoning`) rewrite to that default via `WorkspaceAiKernelFactory.NormalizeModelId`. Newer explicit ids (for example `grok-4.10`) are kept. Do not edit historical EF migration seed strings.

PR #14 (`grok-4.3` + launcher) is superseded; do not merge it red.

1. PR #29: default model `grok-4.7`, launcher `#workspace-jarvis-launcher` — **merged**. Local/hosted health prove (`semantic_kernel`, no fallback) when key is available (#17).
2. PR #30: delete leftover AI types and strip `IAIService` / `AICacheWarmingService` DI — **merged** (#24).

#21, #22, and #23 are closed. Town Hub replaced the old home-panel grid. Archived snapshot export buttons are in Document Center. Council panels announce status and return dialog focus.

Keep Phase 2b egress and Phase 3 deploy out of these PRs.

## Phase 2b / 3+

#17, #18, #19, #20, and #26 are closed. Wiley Widget is not running on the personal account or on `tow`. The old App Runner, Amplify app, and Cognito pool are gone, and `wileywidget.townofwiley.gov` does not resolve. Do not recreate App Runner, Aurora, or ECS to reopen them. Eight orphan `WileyCo-*` alarms remain on account `570912405222` and were not deleted.

## Clerk Mac path (NAS-first local)

Active operator path while Widget has no hosted compute:

| Piece                 | How                                                                                                                                                                                                                                                                                                               |
| --------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Postgres              | Native Synology Community PostgreSQL 17 on `mr-storage:5433` (`wileyco_dev` / `wiley_app`). Not Docker. Password via `appsettings.Development.local.json` (wins over API user-secrets). NAS source of truth: `/root/wiley-postgres/wiley_app.pass`. FY2026 pack gitignored under `Import Data/tow-october-2026/`. |
| Start                 | `./Scripts/start-clerk-local.sh` — Tailscale PG probe → EF migrate → API `5231` + client `5230` via launchctl                                                                                                                                                                                                     |
| Status / stop         | `./Scripts/status-clerk-local.sh` / `./Scripts/stop-clerk-local.sh` (stop leaves NAS Postgres running)                                                                                                                                                                                                            |
| Reload FY2026 ledgers | `./Scripts/reload-clerk-imports.sh`                                                                                                                                                                                                                                                                               |
| Degraded mode         | **Off** in Development (`AllowDegradedStartup=false`). API must not silently fall back to in-memory.                                                                                                                                                                                                              |
| Production auth       | `Auth:Enabled=false` until a new Cognito pool exists.                                                                                                                                                                                                                                                             |
| Town Hub cards        | Apartments + TIKR on `mr-storage`; Trash on Amplify; Rural Water disabled.                                                                                                                                                                                                                                        |
| Prod env overlay      | `deploy/wileytown.prod.env.example` (NAS Office/Postgres). Dev: `deploy/wileytown.dev.env.example`. Secrets stay out of git.                                                                                                                                                                                      |
