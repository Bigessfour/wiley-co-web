# Wiley Widget production completion plan

Ordered from first work to last. Do the phases in sequence.

Status date: 2026-11-08.

## Phase 0. Lock scope

Done. Rate math is decision support (revenue requirement). Issues #27 and #25 closed.

## Phase 1. Auth, before any new production image

[#15](https://github.com/Bigessfour/wiley-co-web/issues/15)

**Done on disk (2026-11-08): staff Cognito login.**

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

Ops before next hosted publish: confirm Cognito pool/client allow `USER_PASSWORD_AUTH`; create staff users; prove anonymous 401 and signed-in workspace load (`Scripts/get-cognito-smoke-token.ps1` when present).

Run `/code-review` on the auth pull request before merge. Keep auth separate from Jarvis.

## Later phases

Phase 2+ (Jarvis model prove, AWS egress, deploy, polish, ECS) remain after Phase 1 merges. See `docs/post-production-operations-handbook.md` for day-two ops.
