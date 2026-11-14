# ClerkSuite ↔ Trash Truck single sign-on

Town staff use **one Cognito user pool** (`us-east-2_DmY7BCBIp`) for ClerkSuite (Wiley Widget / Town Hub) and Trash Truck. Single sign-on depends on a **Cognito Hosted UI / managed login** browser session cookie on the pool domain — not on passing tokens in URLs.

## Contract

| Item | Value |
|------|--------|
| User pool | `us-east-2_DmY7BCBIp` |
| ClerkSuite app client | `2m6vp91m9938jpbg2efivr2p8k` |
| ClerkSuite sign-in | `Auth:SignInFlow=HostedUi` — OAuth2 authorization code + PKCE via Hosted UI |
| ClerkSuite legacy (removable) | `Auth:SignInFlow=UserPassword` — direct `USER_PASSWORD_AUTH` at `/authentication/legacy-login` |
| API JWT | Access token on `Authorization: Bearer`; audience / `client_id` = app client id |
| Trash Truck launch | Town Hub card opens `{TrashTruckBaseUrl}/{clerk\|driver}?from=town-hub` (no `role` query) |
| Groups | `Clerk` sees every card; `Driver` sees Trash Truck only; `admin` sees every card |
| Global sign-out | Cognito `/logout` with `logout_uri` (see `Auth:Cognito:OAuthDomain`) |

## Clerk experience

1. Open ClerkSuite (Amplify) → **Sign in** → redirect to Cognito Hosted UI → back to `/authentication/login-callback`.
2. Town Hub loads signed in; workspace API calls include the Cognito **access token**.
3. Click **Trash Truck** → new tab `…/clerk?from=town-hub` or `…/driver?from=town-hub` from `cognito:groups`.
4. Trash Truck (`Trash_Truck` PR #46) runs silent Hosted UI (`prompt=none`) when the domain cookie exists.
5. **Sign out** in ClerkSuite calls Hosted UI `/logout` so Trash Truck session ends too.

## Configuration (client)

`wwwroot/appsettings.Production.json`:

- `Auth:Enabled=true`
- `Auth:SignInFlow=HostedUi`
- `Auth:Cognito:*` pool, client, authority
- `Auth:Cognito:OAuthDomain` — **required for global logout** (placeholder until AWS prefix is chosen)

Local override: `Scripts/generate-auth-appsettings.sh` with `COGNITO_OAUTH_DOMAIN` and optional `COGNITO_SIGN_IN_FLOW`.

Redirect/sign-out URIs default from the WASM base address:

- `{origin}/authentication/login-callback`
- `{origin}/` post logout

Override with `Auth:Cognito:RedirectUri` / `PostLogoutRedirectUri` when needed.

## Trash Truck companion

Implementation and operator checklist: [Bigessfour/Trash_Truck#46](https://github.com/Bigessfour/Trash_Truck/pull/46).

Do **not** pass access tokens or roles in query strings.

## Follow-through

### Hosted UI domain

The committed production value is still the placeholder `REPLACE_WITH_COGNITO_HOSTED_UI_PREFIX`. Do not replace that string with a guessed prefix.

After the Cognito domain exists in pool `us-east-2_DmY7BCBIp`:

1. Set Amplify `COGNITO_OAUTH_DOMAIN` to `https://<prefix>.auth.us-east-2.amazoncognito.com` (no trailing slash).
2. The Amplify build runs `Scripts/apply-cognito-oauth-domain.cjs`, which writes gitignored `wwwroot/appsettings.Auth.local.json`. That file overrides the placeholder at startup.
3. Local check: `COGNITO_OAUTH_DOMAIN=https://<prefix>.auth.us-east-2.amazoncognito.com node Scripts/apply-cognito-oauth-domain.cjs`

Sign-out ignores the placeholder and uses local OIDC logout until a real `*.amazoncognito.com` domain is configured. Global logout (ClerkSuite and Trash Truck) starts only after that domain is present.

The `steve` AWS profile session was expired when this change was written, so the live domain was not read or created from this repo.

### Legacy password sign-in stays until production verification

`USER_PASSWORD_AUTH` at `/authentication/legacy-login` stays behind `Auth:SignInFlow=UserPassword`. Remove that path only after a clerk has signed in through Hosted UI on the production Amplify host, opened Trash Truck already signed in, and signed out of both. Until that check, the legacy path is the rollback.

### Cross-app end-to-end test

`tests/playwright/sso-cross-app.spec.ts` is the browser test for: sign in once on ClerkSuite, open the Trash Truck card already signed in, sign out, and both apps require sign-in again.

It does not run in smoke or high-risk CI. After both apps are deployed and Hosted UI works:

```bash
WILEYCO_SSO_E2E=1 \
WILEYCO_SSO_BASE_URL=https://wileywidget.townofwiley.gov \
TRASH_TRUCK_E2E_BASE_URL=https://main.d2dke4s7zyafra.amplifyapp.com \
COGNITO_E2E_USERNAME='<clerk test user>' \
COGNITO_E2E_PASSWORD='<not committed>' \
npx playwright test tests/playwright/sso-cross-app.spec.ts --project=chromium
```

## Unverified in repo

- Exact Cognito Hosted UI domain prefix in AWS console
- Whether existing app client `2m6vp91m9938jpbg2efivr2p8k` already allows authorization code + PKCE callback URLs for every Amplify hostname
- Live run of `sso-cross-app.spec.ts` (needs the domain, both deploys, and a clerk test user)
