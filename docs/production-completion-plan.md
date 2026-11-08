# Wiley Widget production completion plan

Ordered from first work to last. Do the phases in sequence.

Status date: 2026-11-08.

## Phase 0. Lock scope

Done. Rate math is decision support (revenue requirement). Issues #27 and #25 closed.

## Phase 1. Auth, before any new production image

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

Non-AWS polish in flight: home hub parity (#21), archived snapshot exports (#22), council-panel a11y (#23).

Keep Phase 2b egress and Phase 3 deploy out of these PRs.

## Phase 2b / 3+

AWS egress hardening, deploy, polish, and ECS remain after Phase 2. See `docs/post-production-operations-handbook.md`.
