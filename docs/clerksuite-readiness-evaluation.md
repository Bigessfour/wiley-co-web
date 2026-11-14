# ClerkSuite readiness evaluation (SSO slice)

Evaluation of **shared Cognito SSO** between ClerkSuite and Trash Truck. Related PR: Hosted UI migration in `wiley-co-web`; Trash Truck side in [Trash_Truck#46](https://github.com/Bigessfour/Trash_Truck/pull/46).

## Readiness

| Area | Status | Notes |
|------|--------|--------|
| ClerkSuite client — Hosted UI + PKCE | **Code ready** | `SignInFlow=HostedUi`, `RemoteAuthenticatorView`, global logout URL |
| ClerkSuite client — legacy password | **Retained behind flag** | `UserPassword` at `/authentication/legacy-login`; remove when AWS cutover complete |
| API JWT validation | **Ready** | Audience + `client_id` fallback; access token expected |
| Town Hub → Trash Truck card | **Ready** | No role dialog; `from=town-hub`; path from `cognito:groups` |
| Trash Truck OAuth + silent launch | **Ready in Trash_Truck#46** | Not merged at evaluation time |
| AWS Hosted UI domain + callbacks | **Operator action** | Placeholders in Production appsettings |
| E2E / Playwright | **Unchanged** | `Auth:Enabled=false` in Development / smoke configs |
| Live SSO smoke | **Not verified** | Needs deployed pool domain + both apps |

## Blockers before clerks rely on one-click Trash Truck

1. Create/configure Cognito Hosted UI domain on pool `us-east-2_DmY7BCBIp`.
2. Update app client `2m6vp91m9938jpbg2efivr2p8k` (or document a new SPA client): auth code grant, PKCE, no secret, callback + sign-out URLs for production Amplify, branch previews, and `http://localhost:5230`.
3. Set `Auth:Cognito:OAuthDomain` on ClerkSuite Amplify to the real domain URL.
4. Deploy Trash Truck SSO build and align pool/client with shared pool.

## Rollout order (recommended)

See PR description AWS checklist. Summary:

1. **AWS** — Hosted UI domain, app client OAuth settings, Cognito groups unchanged.
2. **Trash_Truck #46** — deploy when callbacks include Trash Truck URLs (safe before ClerkSuite cutover; old ClerkSuite password login still works but SSO cookie absent).
3. **wiley-co-web** — deploy Hosted UI ClerkSuite; clerks re-sign once via Hosted UI.
4. Verify Town Hub → Trash Truck without second login.

**Rollback:** Set `Auth:SignInFlow=UserPassword` and redeploy ClerkSuite; restore password-only app client flows in Cognito if OAuth callbacks cause issues. Trash Truck can stay on OAuth independently.
