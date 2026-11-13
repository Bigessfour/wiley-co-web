# ClerkSuite production readiness and single-login evaluation

**Audience:** Mayor Stephen McKitrick, Town of Wiley clerk staff, and operators maintaining the suite.  
**Scope:** `wiley-co-web` (Amplify Blazor WASM + App Runner API) as **Town Hub / ClerkSuite entry**, plus sibling projects reached from hub cards.  
**Evaluation date:** 2026-11-13.  
**Method:** Repository code and configuration review (`Components/Pages/Home.razor`, `Options/TownHubOptions.cs`, `Services/Auth/*`, `wwwroot/appsettings.*.json`, `WileyCoWeb.Api/*`, `docs/*`, `infra/terraform/*`, `.github/workflows/ci.yml`, Playwright specs). **No application code was changed.** Local `dotnet` / Playwright UI runs were **not** executed on the evaluation VM (SDK not installed); hosted UI was not exercised with production Cognito credentials (no clerk test accounts in this environment).

**Terminology:** Staff see **Town Hub** at `/`. Product copy also refers to **Wiley Widget** (utility rate study workspace) and **ClerkSuite** (especially the Apartments app on the NAS). This document uses **ClerkSuite** for the whole suite when referring to the owner’s single-login requirement.

---

## 1. Project card inventory

Home screen cards are **exactly five** entries, loaded from configuration section `TownHub:Apps` with optional flat URL overrides (`TownHub:WidgetUrl`, `ApartmentsUrl`, etc.). Implementation: `Components/Pages/Home.razor`, `Options/TownHubOptions.cs`, `wwwroot/appsettings.Development.json`, `wwwroot/appsettings.Production.json`, `docs/town-hub.md`.

There are **no additional hub cards** in code for other town projects (for example Town of Wiley public CMS, mail routing, or standalone Colorado Rural Water Lambda) — those appear in `docs/aws-low-cost-architecture.md` as suite context but are **not** on the Town Hub grid.

| Id | Card title | Where it lives | Production URL (committed config) | Data / hosting notes |
| --- | --- | --- | --- | --- |
| `01` | **Wiley Widget** | **In-repo** Blazor WASM panels under `/wiley-workspace/*` | `/wiley-workspace` (same origin) | Aurora PostgreSQL via App Runner API; primary financial / rate-study data |
| `02` | **Apartments** | **External repo** `Bigessfour/Wiley_Apartments` (ClerkSuite); **NAS** Docker | `http://mr-storage:8082` | SQLite on Synology volume; Tailscale MagicDNS |
| `03` | **Rural Water** | **External** Colorado Rural Water app (not in this repo) | **Empty** → card **disabled** in UI | Local dev default `http://localhost:8083`; no stable production URL recorded |
| `04` | **Trash Truck** | **External repo** `Bigessfour/Trash_Truck` | `https://main.d2dke4s7zyafra.amplifyapp.com` | Separate Amplify app; clerk/driver paths via hub dialog |
| `05` | **TIKR** | **External** TIKR (Blazor Server + API, own repo) | `http://mr-storage:8080` | SQLite on NAS; Tailscale |

**Related in-repo workspace panels (not separate home cards):** Opening card `01` exposes 15 left-nav destinations (`Components/Layout/NavMenu.razor`, `Components/Pages/WileyWorkspaceBase.cs` `PanelNavItems`): Overview, Break-Even, Apartment Config, Rates, QuickBooks Import, Scenario Planner, Customer Viewer, Affordability, Reserve Trajectory, Debt Coverage, Capital Gap, Trends, Decision Support (Jarvis), Data Dashboard, Reference Data.

### 1.1 Authentication behavior today (per card)

| Card | How auth works when staff use ClerkSuite | Second login when clicking the card? | Evidence |
| --- | --- | --- | --- |
| **Wiley Widget** | Production: `Auth:Enabled=true` in `wwwroot/appsettings.Production.json`. Staff sign in at `/authentication/login` (`Components/Pages/AuthenticationLogin.razor`) using Cognito `USER_PASSWORD_AUTH` (`Services/Auth/CognitoAuthService.cs`). Session stored in browser local storage key `wiley.auth.cognito.session.v1` (`Services/Auth/CognitoTokenStore.cs`). API calls attach `Authorization: Bearer` via `CognitoAuthorizationMessageHandler`. JWT validation on API when `Authentication:Jwt:Enabled` (`WileyCoWeb.Api/Configuration/WorkspaceEndpointAuthorizationExtensions.cs`). | **No** — same SPA; navigating to `/wiley-workspace` reuses the hub session. | `docs/town-hub.md`; `docs/production-completion-plan.md` Phase 1 (PR #28) |
| **Apartments** | Hub opens **new browser tab** to NAS HTTP URL (`OpenInNewTab: true`). No token, cookie, or OIDC handoff from Wiley Widget. Apartments app auth is **outside this repository** (historically per-app login). | **Yes (expected)** — unless Apartments was separately configured to trust the same Cognito pool without a second prompt (not implemented in `wiley-co-web`). | `docs/town-hub.md` § Auth: *“Sibling apps may still use their own auth; hub only links out.”* |
| **Rural Water** | Card disabled when `Url` is empty (`Home.razor` renders `is-disabled`). | **N/A** in production until a URL is configured. | `wwwroot/appsettings.Production.json` |
| **Trash Truck** | Hub shows role dialog (Clerk vs Driver), then opens `…/clerk` or `…/driver` with query `?from=town-hub&role=…` (`Home.razor` `BuildTrashRoleUrl`). **No JWT or SSO pass-through.** Trash Truck is a **different origin** (Amplify). | **Yes (expected)** — separate app session unless Trash Truck integrates Cognito as a relying party (not in this repo). UI copy *“Town Hub is already signed in”* describes intent, not technical SSO. | `Components/Pages/Home.razor` lines 78–95, 467–471 |
| **TIKR** | Same pattern as Apartments: new tab to `http://mr-storage:8080`. | **Yes (expected)** | Same as Apartments |

**Local development nuance:** `Auth:Enabled=false` in `wwwroot/appsettings.Development.json` causes `CognitoAuthenticationStateProvider` to synthesize user `local-dev` (`Services/Auth/CognitoAuthenticationStateProvider.cs`), so engineers see an “always signed in” hub **without** proving Cognito flows.

### 1.2 Network prerequisites for clerks

- **NAS cards (Apartments, TIKR):** Production URLs use **plain HTTP** and hostname **`mr-storage`**, reachable only on the **Tailscale tailnet** (`docs/town-hub.md`, `docs/aws-low-cost-architecture.md`). Clerks off-tailnet or without Tailscale on the PC will see broken links even after Cognito login.
- **Trash Truck:** Public HTTPS on a **different** Amplify app — works without Tailscale but not SSO-linked.
- **Wiley Widget:** Hosted at `https://main.d2ellat1y3ljd9.amplifyapp.com` / `https://wileywidget.townofwiley.gov` (`README.md`); API at App Runner (`https://mr7zeizxxd.us-east-2.awsapprunner.com`).

---

## 2. Single-login gap analysis and recommended design

### 2.1 Executive gap statement

**Met today:** One Cognito staff login gates **Town Hub and the entire Wiley Widget SPA + App Runner API** (when production flags are on).

**Not met:** The owner requirement that **every hub card** opens **without a second login**. External apps remain **deep links**; architecture docs explicitly allow separate auth (`docs/town-hub.md`). Trash Truck only receives **routing hints** in the query string, not identity.

**Strategic target (already documented):** `docs/aws-low-cost-architecture.md` calls for **one Cognito user pool** with **per-app clients and groups** for Widget, TIKR, Apartments, Rural Water, and Trash Truck. That diagram is **aspirational** — `wiley-co-web` does not implement federation or token relay to siblings.

### 2.2 Recommended approaches (per project)

| Card | Recommended approach | Effort | Risks / notes |
| --- | --- | --- | --- |
| **Wiley Widget** | **Keep current** — Cognito `USER_PASSWORD_AUTH` + refresh tokens + API JWT. Harden: MFA, groups, session UX. | **S** | Cognito challenges (MFA, `NEW_PASSWORD_REQUIRED`) currently throw generic errors (`CognitoAuthService.cs`). |
| **Apartments** | **OIDC authorization code flow** with shared Cognito pool + dedicated app client; redirect from hub via short-lived “launch” URL **or** embedded iframe (not recommended for Blazor Server). Alternative: **reverse proxy** on tailnet (`clerk.townofwiley.gov/apartments`) terminating Cognito at edge and forwarding trusted headers. | **L** (Apartments + NAS/proxy work) | SQLite on NAS; must not break local/Synology path per `docs/aws-low-cost-architecture.md`. HTTP→HTTPS and Tailscale-only exposure. |
| **TIKR** | Same as Apartments — Cognito app client + Blazor Server cookie sign-in from OIDC middleware. | **L** | Blazor Server sticky sessions; NAS hosting. |
| **Trash Truck** | Register Cognito app client; Amplify Hosted UI or custom OIDC middleware; accept hub-issued **one-time launch token** (signed JWT from Widget API) **or** full OIDC redirect chain starting at hub. | **M–L** | Public Amplify origin; may still use separate user store today. |
| **Rural Water** | Defer SSO until URL exists; when ready, **Lambda + API Gateway** pattern in arch doc — Cognito JWT authorizer on API, static site with same pool. | **M** | No production URL; card disabled. |

**Cross-cutting patterns (pick one suite-wide):**

1. **Cognito as single IdP (preferred):** One user pool (`us-east-2_DmY7BCBIp`), one staff username/password, **multiple app clients**, optional **groups** (`clerk`, `driver`, `finance`). Each sibling app validates the same JWT issuer. Hub becomes a **launchpad** that opens siblings with `?code=` OIDC redirect or silent refresh where same-site cookies allow (usually **not** across Amplify + NAS origins).

2. **Signed launch token (pragmatic bridge):** After hub login, Widget API mints a **60-second HS256/RS256 JWT** (`aud=apartments`, `sub=username`, Cognito `sub` claim) consumed by sibling app login endpoint. Requires trust store in each app and **short TTL**. Medium effort, avoids full OIDC in every app initially.

3. **Reverse proxy / Tailscale funnel:** Single HTTPS hostname; proxy injects `X-Forwarded-User` after validating Cognito session cookie at edge. Siblings trust proxy only. Good for NAS HTTP backends; **L** ops effort.

4. **Port apps in-repo:** Highest integration cost; only justified if NAS hosting is retired.

### 2.3 Role and permission model (current vs needed)

| Topic | Current state | Recommendation |
| --- | --- | --- |
| **Who sees which cards** | **No filtering** — all five cards render for every authenticated hub user (`Home.razor` has no role checks). | Cognito groups: `clerk` (Apartments, Widget, TIKR), `utility-clerk` (Rural Water when live), `sanitation-driver` (Trash Truck driver path only). Filter `_apps` by group claims once JWT claims are mapped in `CognitoAuthenticationStateProvider` (today only `ClaimTypes.Name` is set). |
| **Trash Truck clerk vs driver** | UI dialog only (`OpenTrashRoleDialog`); **not enforced** by Cognito. | Map Cognito groups to default path; hide Driver button for non-drivers. |
| **Session timeout** | Access token refresh when within 2 minutes of expiry (`CognitoAuthService.GetAccessTokenAsync`); refresh failure clears session. Pool token lifetimes are **Cognito console settings** (not in repo). | Document clerk-facing “you’ll need to sign in again after X hours”; align pool refresh token validity with town policy (typical 8–12h office day). |
| **Logout everywhere** | **Sign out** clears Widget local storage only (`LoginDisplay.razor`, `CognitoAuthService.SignOutAsync`). **Does not** log out NAS or Trash Truck tabs. | Global logout: Cognito **GlobalSignOut** + sibling apps register logout callbacks, or proxy session destruction. Until then, train clerks to close sibling tabs. |
| **Password reset / MFA** | Not implemented in UI; Cognito challenges return errors (“Complete the challenge in the AWS console”). | Enable Cognito **forgot password** flow or admin reset SOP; enable **optional MFA** (TOTP) for clerks handling financial PII; add hosted UI or challenge handlers in `CognitoAuthService`. |

### 2.4 Suggested implementation sequence (technical)

1. **Suite identity charter** — document pool, app clients, groups (Widget already has client `2m6vp91m9938jpbg2efivr2p8k`).
2. **Apartments + TIKR OIDC** on Tailscale HTTPS hostnames (certificates via reverse proxy).
3. **Trash Truck** Cognito client on Amplify.
4. **Hub launch service** — optional `GET /api/auth/launch?app=apartments` returning redirect URL with OIDC or signed token (new API surface; must be in JWT mutating policy).
5. **Playwright** staff journey: login → each card → assert no second password field (`tests/playwright` has hub smoke but **no Cognito E2E**).

---

## 3. Production-readiness checklist

Prioritized for **non-technical clerks** using production paths daily.

### Must-fix before clerks rely on ClerkSuite

| Area | Item | Rationale |
| --- | --- | --- |
| **Single login** | Implement SSO (or interim launch tokens) for **Apartments, TIKR, Trash Truck** | Owner requirement; docs admit second logins today |
| **Single login** | Remove misleading Trash Truck copy unless SSO works | Avoid false confidence (“already signed in”) |
| **Auth** | Create production Cognito users; verify `USER_PASSWORD_AUTH`; prove anonymous API **401** and signed-in **200** on `/api/workspace/snapshot` | `docs/production-completion-plan.md` Phase 1 ops |
| **Auth** | Password reset + MFA policy for clerk accounts | No self-service in UI today |
| **Auth** | Map Cognito groups to card visibility and Trash roles | Prevent drivers from finance panels |
| **Network** | Confirm every clerk PC has **Tailscale** and can resolve `mr-storage:8082` / `:8080` | Production card URLs depend on it |
| **Network** | **HTTPS** for NAS-backed apps or terminate TLS at proxy | Production config uses `http://` |
| **Security** | Ensure `Authentication:Jwt:Enabled=true` on public App Runner; never expose API with JWT off + public internet | Dev pattern is open endpoints |
| **Security** | CORS locked to known Amplify/custom domains (`update-config.json` `WorkspaceClientOrigins`) | Prevent token theft from rogue origins |
| **Security** | Reference-data ingest credentials for NAS publishers — POST requires mutating JWT when enabled | `ReferenceDataEndpoints.cs` |
| **Reliability** | Rural Water URL decision — enable card or remove from clerk training | Disabled card confuses staff |
| **Testing** | Add hosted E2E: Cognito login → Widget QuickBooks panel smoke → hub card link check (mock or staging siblings) | No automated clerk journey today |
| **Operations** | Named **on-call** / escalation for Sev 1 (mayor + contractor) | `docs/operations-governance-and-review-cadence.md` roles exist but no pager |
| **Data** | Document **NAS backup** for Apartments/TIKR SQLite and restore test | Data lives off AWS |
| **Data** | Aurora backup/restore tested per `docs/aurora-postgresql-reset-runbook.md` | Widget financial data |
| **Usability** | Clerk-facing **quick start** (one page, plain language) separate from engineer `README.md` | Hub copy mentions “shared-contract reference data” jargon |

### Should-fix soon

| Area | Item |
| --- | --- |
| **Reliability** | Health monitoring for NAS containers (Apartments/TIKR) — Widget cannot know if NAS is down beyond “No data yet” on cards |
| **Reliability** | Jarvis non-fallback production proof (`latestUsedFallback=false`) per `docs/backend-production-signoff-checklist.md` |
| **Security** | Logout propagates to siblings or explicit “you may still be signed in elsewhere” warning |
| **Security** | Secrets rotation drill (`docs/secrets-and-config-rotation-runbook.md`) |
| **Deployment** | Immutable ECR tags / rollback drills — handbook warns against relying on `latest` only |
| **Testing** | Raise Playwright effective pass rate; reduce skips (`docs/testing-gap-analysis-2026-04-22.md`) |
| **Hosting** | App Runner successor plan (service closed to new customers 2026-04-30) — `docs/post-production-operations-handbook.md` |
| **Accessibility** | Council-panel a11y follow-ups noted in `docs/production-completion-plan.md` |
| **Financial accuracy** | Confirm council panels use live data not synthetic fallback (`WorkspacePanels:Fallback`) in production |

### Nice-to-have

| Area | Item |
| --- | --- |
| **UX** | Rename “Town Hub” vs “Wiley Widget” vs “ClerkSuite” consistently in UI |
| **UX** | Hide raw URLs on cards (`town-hub-url` spans) from clerks — show friendly “Opens on town server” |
| **Mobile** | Tablet layout review — Syncfusion dense grids on Customer Viewer / QuickBooks |
| **Integration** | Move NAS apps to App Runner + Aurora when budget allows (`docs/aws-low-cost-architecture.md`) |
| **Cost** | Monthly AWS cost review ($1–3 target in arch doc) |
| **Observability** | OpenTelemetry / centralized logging beyond CloudWatch ad hoc queries |

---

## 4. Clerk usability review (walkthrough)

*Based on UI structure and copy in repo; not validated with live screenshots in this evaluation environment.*

### 4.1 First visit — sign in and home

1. Browser opens Wiley Widget URL → static boot shell (“Starting Wiley Widget” in `wwwroot/index.html`) then Blazor loads.
2. If production auth on, user redirect to **Staff sign-in** (`/authentication/login`). Labels say “Cognito staff account” and “Anonymous API calls return 401” — **too technical** for clerks.
3. After sign-in, land on **Town Hub** (`/`). Hero text: *“Single entry point for town apps. Cards open the existing UIs (local or App Runner). Summary tiles below read shared-contract reference data…”* — **jargon** (`shared-contract`, `App Runner`).
4. Five cards show **index numbers**, **status chips** (“No data yet” / “Updated …”), and **raw URLs** (including `http://mr-storage:8082`) — confusing for non-technical users.
5. Primary actions: **Open Wiley Widget workspace** and **Full reference data** — good shortcuts.

**Pain points:** Technical hero copy; exposed internal hostnames; disabled Rural Water card without explanation; no in-app help or “call Steve” link.

### 4.2 Wiley Widget card (daily finance / rates)

Typical clerk tasks: QuickBooks import, customer lookup, scenario for council, PDF rate packet.

| Task | Path | Usability notes |
| --- | --- | --- |
| Import QuickBooks export | `/wiley-workspace/quickbooks-import` | Panel subtitle is clerk-friendly; multi-step Syncfusion UI is **dense**; errors may be raw API messages |
| Find a customer | `/wiley-workspace/customers` | Grid + dialogs; Excel export via dedicated button (not toolbar) — train explicitly |
| Council scenario | `/wiley-workspace/scenario` | “Load New Hire Impact Template” helpful; terminology “break-even”, “enterprise” needs glossary |
| Ask Jarvis | `/wiley-workspace/decision-support` | AI failures show fallback text — clerks may not distinguish live vs fallback |
| Print PDF packet | Workspace export actions | Good for council; verify button label changes with scenario content (`docs/council-readiness-may-2026.md`) |

**Accessibility:** Nav uses tooltips and `aria-current`; workspace is desktop-oriented — **small laptop OK**; phone unlikely.

### 4.3 Apartments card

Click opens **new tab** to NAS. Clerk must understand Tailscale. Expect **second login** and different UI paradigm (ClerkSuite). No return path to hub except browser back / bookmark.

### 4.4 TIKR card

Same as Apartments. “TIKR” acronym unexplained on card — expand title/description for training.

### 4.5 Trash Truck card

Dialog: **Clerk** vs **Driver** — clear. Opens separate Amplify site. Driver role may be irrelevant for office clerks — consider hiding via Cognito group later.

### 4.6 Rural Water card

Disabled in production — clerks see grey card “(not configured)”. **Remove or hide** until go-live to avoid “is it broken?” questions.

### 4.7 Reference data snapshot (on hub)

Tiles show **Latest actuals** and **Recent OperationalEvents** with enterprise IDs like `tow-water-utility` — useful for power users, opaque for clerks. Link to **Reference Data** panel is appropriate for auditors, not daily clerks.

### 4.8 Screenshots

Evaluation VM could not run the Blazor client (no .NET SDK). Production hosted URLs require Cognito credentials. **Recommended:** Clerk UAT capture screenshots per §5.2 and store under `/opt/cursor/artifacts/` or town SharePoint.

---

## 5. Phased rollout and UAT

### Phase A — Pilot (1–2 clerks, staging or auth-enabled dev build)

**Goals:** Validate Cognito accounts, Tailscale reachability, Widget QuickBooks import, hub navigation.

**Exit criteria:** Pilot clerks complete UAT script below without developer assistance; SSO gap documented and accepted as known limitation **or** fixed for Widget-only go-live.

### Phase B — Widget go-live (finance/rates only)

**Goals:** Production hub + Wiley Widget card only; **temporarily hide or relabel** sibling cards that still require second login (config change in `TownHub:Apps` or training “do not use yet”).

**Exit criteria:** `npm run playwright:test:ci:highrisk` green; App Runner `/health` green; release record filed.

### Phase C — Sibling SSO waves

**Order suggested:** Trash Truck (HTTPS public) → Apartments → TIKR → Rural Water when URL exists.

**Exit criteria:** Per-app UAT with **no second password**; logout behavior documented.

### Phase D — Full ClerkSuite

All cards enabled; quarterly restore test NAS + Aurora; council demo path rehearsed.

### 5.1 Short UAT script (clerks)

Print this one-page script. Clerk checks **Pass / Fail** and notes.

1. **Sign in once:** Open the town Wiley Widget website. Enter username and password. You should see **Town Hub** with five cards. **Pass / Fail: ___**
2. **Open Wiley Widget:** Click the **Wiley Widget** card (or green **Open Wiley Widget workspace**). You should **not** be asked for password again. **Pass / Fail: ___**
3. **QuickBooks:** In the left menu, open **QuickBooks Import**. Page loads without error. **Pass / Fail: ___**
4. **Customers:** Open **Customer Viewer**. You see a list or a clear “no customers” message (not a blank white screen). **Pass / Fail: ___**
5. **Return home:** Click **Town Hub** in the left menu. Hub loads. **Pass / Fail: ___**
6. **Apartments:** Click **Apartments** card. Note whether a **second login** appears. **Pass / Fail / Second login: ___**
7. **TIKR:** Same as step 6. **Pass / Fail / Second login: ___**
8. **Trash Truck:** Click card, choose **Clerk**, note second login. **Pass / Fail / Second login: ___**
9. **Sign out:** Click **Sign out** top right. Hub should ask you to sign in again. **Pass / Fail: ___**
10. **Help:** If any step failed, write what you saw and call _______________.

### 5.2 Screenshot checklist (for training deck)

- Staff sign-in page  
- Town Hub with all cards  
- Wiley Widget overview  
- QuickBooks Import panel  
- Trash Truck role dialog  
- Example error state (API down) if safe to reproduce in staging  

---

## 6. Per-card single-login summary table

| Card | In-repo? | Production host | Auth for card app | Second login today? | Recommended SSO approach | Effort |
| --- | --- | --- | --- | --- | --- | --- |
| Wiley Widget | Yes | Same Amplify app | Cognito + API JWT | **No** | Maintain; add groups/MFA | S |
| Apartments | No (`Wiley_Apartments`) | NAS `mr-storage:8082` | Separate (local/NAS app) | **Yes (expected)** | Cognito OIDC + HTTPS proxy | L |
| Rural Water | No | *Not configured* | Unknown | N/A (disabled) | Cognito + API Gateway when live | M |
| Trash Truck | No (`Trash_Truck`) | Amplify `d2dke4s7zyafra` | Separate Amplify app | **Yes (expected)** | Cognito OIDC or launch JWT | M–L |
| TIKR | No | NAS `mr-storage:8080` | Separate Blazor Server | **Yes (expected)** | Cognito OIDC + HTTPS proxy | L |

---

## 7. Key file reference index

| Concern | Path |
| --- | --- |
| Hub cards UI | `Components/Pages/Home.razor` |
| Hub config model | `Options/TownHubOptions.cs` |
| Production card URLs | `wwwroot/appsettings.Production.json` |
| Cognito client auth | `Services/Auth/CognitoAuthService.cs`, `Components/Pages/AuthenticationLogin.razor` |
| Route authorization | `Components/Routes.razor`, `ClientStartup.cs` |
| API JWT gating | `WileyCoWeb.Api/Configuration/WorkspaceEndpointAuthorizationExtensions.cs` |
| Town Hub operator doc | `docs/town-hub.md` |
| Auth phase status | `docs/production-completion-plan.md` |
| Suite hosting / Cognito vision | `docs/aws-low-cost-architecture.md` |
| Day-2 ops | `docs/post-production-operations-handbook.md` |
| Hub Playwright smoke | `tests/playwright/workspace-smoke.spec.ts` |
| NAS URL IaC pointers | `infra/terraform/variables.tf`, `infra/terraform/README.md` |

---

## 8. Document history

| Date | Change |
| --- | --- |
| 2026-11-13 | Initial evaluation (docs-only PR). |
