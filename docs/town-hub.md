# Town Hub

Clerk starting point at `/` and `/town-hub` inside Wiley Widget (Blazor WASM).

## What it is

- Cognito login when `Auth:Enabled` is true (same as the rest of wiley-co-web); local default keeps auth off for mock/local testing.
- After sign-in (or when auth is disabled), clerks land on Town Hub — not a nested workspace panel.
- Five cards: Wiley Widget, Apartments, Rural Water, Trash Truck, TIKR — each opens the **existing** app URL (no embedded UIs).
- Compact summary tiles that call `GET /api/reference-data/actuals` and `GET /api/reference-data/events` via the existing `SharedContractReferenceDataApiService`.
  - **Latest actuals** — most recent ActualsSummary (enterprise, period, income vs expense). Empty API → muted “No actuals yet” with a link to Reference Data.
  - App cards show **Updated …** / **No data yet** from matching `ReferenceEnterpriseId`, `ReferencePublisherSystem`, or `ReferenceSources` (no extra AWS calls).
- Full grids stay on `/wiley-workspace/reference-data` (`SharedContractReferenceDataPanel`) — Town Hub does **not** duplicate that panel.
- Other Widget panels (Break-Even, Rates, QuickBooks import, etc.) remain reachable from the left nav once the shell is open.

## Pointing cards at local vs App Runner

Town Hub cards bind from configuration section **`TownHub:Apps`** (`Home.razor` → `Configuration.GetSection("TownHub")`).

| File                                     | Typical use                                              |
| ---------------------------------------- | -------------------------------------------------------- |
| `wwwroot/appsettings.Development.json`   | Local ports (committed defaults)                         |
| `wwwroot/appsettings.Production.json`    | App Runner (or Amplify) HTTPS URL placeholders           |
| `wwwroot/appsettings.TownHub.local.json` | Build/env overrides (gitignored; merged at WASM startup) |

Optional flat overrides (same section) — applied onto matching app Ids after `Apps` bind:

| Property                | Card Id | Env var (build / script)   |
| ----------------------- | ------- | -------------------------- |
| `TownHub:WidgetUrl`     | `01`    | `TOWN_HUB_WIDGET_URL`      |
| `TownHub:ApartmentsUrl` | `02`    | `TOWN_HUB_APARTMENTS_URL`  |
| `TownHub:RuralWaterUrl` | `03`    | `TOWN_HUB_RURAL_WATER_URL` |
| `TownHub:TrashTruckUrl` | `04`    | `TOWN_HUB_TRASH_TRUCK_URL` |
| `TownHub:TikrUrl`       | `05`    | `TOWN_HUB_TIKR_URL`        |

Blazor WASM cannot read process environment variables in the browser. Overrides are injected at **build time** into `wwwroot/appsettings.TownHub.local.json` (same pattern as `WILEY_WORKSPACE_API_BASE_ADDRESS` → `appsettings.Workspace.local.json`), then merged in `ClientStartup` at startup.

Generate locally or in CI (Node; works on macOS/Linux/Windows):

```bash
node Scripts/generate-townhub-appsettings.cjs
# or: ./Scripts/generate-townhub-appsettings.sh
```

A partial env set merges onto any existing root or wwwroot `appsettings.TownHub.local.json` so other override keys are kept. With no env and no root local file (or with `--clear`), the script writes `{}` so a stale wwwroot copy cannot publish.

MSBuild target `GenerateTownHubAppSettings` and Amplify `amplify.yml` call the Node script (or `--clear` when no overrides are set).

Each app entry:

```json
{
  "Id": "02",
  "Title": "Apartments",
  "Description": "ClerkSuite apartments operations.",
  "Url": "http://mr-storage:8082",
  "OpenInNewTab": true,
  "ReferenceEnterpriseId": "tow-apartments",
  "ReferencePublisherSystem": "wiley-apartments"
}
```

Optional status match fields: `ReferenceEnterpriseId`, `ReferencePublisherSystem`, `ReferenceSources[]`. Omit them for apps with no reference-data yet (card shows “No data yet”).

### Local defaults (dev)

Dev defaults match the NAS-first clerk path (Tailscale). Override with `appsettings.TownHub.local.json` only when you need pure-localhost sibling apps.

| App          | Default `Url`                                |
| ------------ | -------------------------------------------- |
| Wiley Widget | `/wiley-workspace` (same origin)             |
| Apartments   | `http://mr-storage:8082`                     |
| Rural Water  | empty (card disabled)                        |
| Trash Truck  | `https://main.d2dke4s7zyafra.amplifyapp.com` |
| TIKR         | `http://mr-storage:8080`                     |

Widget shell itself is `http://localhost:5230`; API is `http://localhost:5231`. Use `./Scripts/start-clerk-local.sh` for the Mac path (API + client against NAS native Postgres on `mr-storage:5433`).

Local env override example (NAS-first):

```bash
TOWN_HUB_WIDGET_URL=/wiley-workspace \
TOWN_HUB_APARTMENTS_URL=http://mr-storage:8082 \
TOWN_HUB_TRASH_TRUCK_URL=https://main.d2dke4s7zyafra.amplifyapp.com \
TOWN_HUB_TIKR_URL=http://mr-storage:8080 \
  ./Scripts/generate-townhub-appsettings.sh
```

### Production (NAS-first)

Clerk databases stay on the Synology (`mr-storage`) over Tailscale. Production card URLs:

| Card         | URL                                          | What the clerk sees                       |
| ------------ | -------------------------------------------- | ---------------------------------------- |
| Wiley Widget | `/wiley-workspace`                           | This app, when the shell is open         |
| Apartments   | `http://mr-storage:8082`                     | NAS, Postgres `clerksuite` on `:5433`. Sign in again there. |
| TIKR         | `http://mr-storage:8080`                     | NAS, SQLite. Sign in again there.                            |
| Trash Truck  | `https://main.d2dke4s7zyafra.amplifyapp.com` | Existing Amplify app. Same Town Hub sign-in.                 |
| Rural Water  | empty (card disabled)                        | No site yet. The card stays closed.                          |

`wwwroot/appsettings.Development.json` keeps `localhost` ports for Mac development. Tailscale names resolve only on the tailnet, which is the clerk path. Do not replace them with App Runner placeholders.

Amplify `TOWN_HUB_*_URL` overrides still work when a card must move. Leave them unset to keep the Production file above.

## Auth and who sees which card

- **Local:** `Auth:Enabled` = `false` → no Cognito gate, and every card is shown so local testing does not depend on groups. The disabled-auth login page links to Town Hub (`/`).
- **Production (NAS-first, 2026-11):** `Auth:Enabled` = `false` until a live Cognito pool exists on the Town account. The prior pool `us-east-2_DmY7BCBIp` was removed. Do not publish that pool id. When auth returns, set `Auth:Enabled=true` and regenerate via `Scripts/generate-auth-appsettings.sh`. Hosted UI is the sign-in flow (`Auth:SignInFlow=HostedUi`).

When auth is on, cards follow `cognito:groups` (`AllowedGroups` on each app, or the built-in default):

| Group | Cards |
| ----- | ----- |
| `Clerk` | Wiley Widget, Apartments, Rural Water, Trash Truck, TIKR |
| `Driver` | Trash Truck only |
| `admin` | All cards |
| Signed in, no groups claim | Same as `Clerk`, so a token that has not been stamped yet does not blank the hub |

Trash Truck opens `{url}/clerk` or `{url}/driver` from those groups and says it uses this sign-in. Apartments and TIKR on `http://` hosts other than localhost say **office network only** and tell the clerk to sign in again. They do not share the Town Hub login. Rural Water with an empty URL is a closed **Coming soon** card.

## Related docs

- `docs/shared-contracts-reference-data-api.md` — ingest API
- `docs/aws-low-cost-architecture.md` — hosting sketch
- `docs/shared-contracts-agent-suggestions.md` — prompt series
