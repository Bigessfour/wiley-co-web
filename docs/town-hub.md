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
  "Url": "http://localhost:8082",
  "OpenInNewTab": true,
  "ReferenceEnterpriseId": "tow-apartments",
  "ReferencePublisherSystem": "wiley-apartments"
}
```

Optional status match fields: `ReferenceEnterpriseId`, `ReferencePublisherSystem`, `ReferenceSources[]`. Omit them for apps with no reference-data yet (card shows “No data yet”).

### Local defaults (dev)

| App          | Default `Url`                    |
| ------------ | -------------------------------- |
| Wiley Widget | `/wiley-workspace` (same origin) |
| Apartments   | `http://localhost:8082`          |
| Rural Water  | `http://localhost:8083`          |
| Trash Truck  | `http://localhost:8084`          |
| TIKR         | `http://localhost:5080`          |

Adjust ports to match your docker-compose / `launchSettings` if they differ. Widget shell itself is `http://localhost:5230`; API is `http://localhost:5231`.

Local env override example:

```bash
TOWN_HUB_WIDGET_URL=/wiley-workspace \
TOWN_HUB_APARTMENTS_URL=http://localhost:8082 \
TOWN_HUB_RURAL_WATER_URL=http://localhost:8083 \
TOWN_HUB_TRASH_TRUCK_URL=http://localhost:8084 \
TOWN_HUB_TIKR_URL=http://localhost:5080 \
  ./Scripts/generate-townhub-appsettings.sh
```

### Production / App Runner

Replace the `REPLACE-*.awsapprunner.com` placeholders in `appsettings.Production.json` with the real service URLs after deploy, **or** set Amplify branch environment variables and let the build emit `appsettings.TownHub.local.json`.

Relative paths (Widget) stay same-origin; absolute HTTPS URLs open in a new tab when `OpenInNewTab` is true.

AWS / Amplify example:

```bash
TOWN_HUB_APARTMENTS_URL=https://xxxxx.us-east-2.awsapprunner.com
TOWN_HUB_RURAL_WATER_URL=https://yyyyy.us-east-2.awsapprunner.com
TOWN_HUB_TRASH_TRUCK_URL=https://zzzzz.us-east-2.awsapprunner.com
TOWN_HUB_TIKR_URL=https://ttttt.us-east-2.awsapprunner.com
```

If Amplify hosts the WASM shell and API separately, keep Widget as `/wiley-workspace` (or `TOWN_HUB_WIDGET_URL=/wiley-workspace`) and only swap sibling-app absolute URLs.

## Auth

- **Local:** `Auth:Enabled` = `false` → no Cognito gate (mock/open for local testing). Disabled-auth login page links to Town Hub (`/`).
- **Production:** `Auth:Enabled` = `true` → single Cognito user pool (existing Wiley Widget config). Successful sign-in defaults to `/` (Town Hub) unless a safe `returnUrl` is present. Sibling apps may still use their own auth; hub only links out.

## Related docs

- `docs/shared-contracts-reference-data-api.md` — ingest API
- `docs/aws-low-cost-architecture.md` — hosting sketch
- `docs/shared-contracts-agent-suggestions.md` — prompt series
