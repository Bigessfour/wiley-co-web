# Town Hub

Minimal suite entry page at `/` and `/town-hub` inside Wiley Widget (Blazor WASM).

## What it is

- Cognito login when `Auth:Enabled` is true (same as the rest of wiley-co-web); local default keeps auth off for mock/local testing.
- Five cards: Wiley Widget, Apartments, Rural Water, Trash Truck, TIKR — each opens the **existing** app URL (no embedded UIs).
- Compact summary tiles that call `GET /api/reference-data/actuals` and `GET /api/reference-data/events` via the existing `SharedContractReferenceDataApiService`.
- Full grids stay on `/wiley-workspace/reference-data` (`SharedContractReferenceDataPanel`) — Town Hub does **not** duplicate that panel.

## Pointing cards at local vs App Runner

Town Hub cards bind from configuration section **`TownHub:Apps`** (`Home.razor` → `Configuration.GetSection("TownHub")`). Supported override path for the Blazor WASM client:

| File | Typical use |
| ---- | ----------- |
| `wwwroot/appsettings.json` | Local ports (defaults below) |
| `wwwroot/appsettings.Production.json` | App Runner (or Amplify) HTTPS URLs |

Process **environment variables are not used** for card URLs in the WASM client (unlike `WILEY_WORKSPACE_API_BASE_ADDRESS` for the API base). Change local vs production by editing the matching `appsettings*.json` (or shipping a different Production file at deploy time).

Each app entry:

```json
{
  "Id": "02",
  "Title": "Apartments",
  "Description": "ClerkSuite apartments operations.",
  "Url": "http://localhost:8082",
  "OpenInNewTab": true
}
```

### Local defaults (dev)

| App | Default `Url` |
| --- | ------------- |
| Wiley Widget | `/wiley-workspace` (same origin) |
| Apartments | `http://localhost:8082` |
| Rural Water | `http://localhost:8083` |
| Trash Truck | `http://localhost:8084` |
| TIKR | `http://localhost:5080` |

Adjust ports to match your docker-compose / `launchSettings` if they differ. Widget shell itself is `http://localhost:5230`; API is `http://localhost:5231`.

### Production / App Runner

Replace the `REPLACE-*.awsapprunner.com` placeholders in `appsettings.Production.json` with the real service URLs after deploy. Relative paths (Widget) stay same-origin; absolute HTTPS URLs open in a new tab when `OpenInNewTab` is true.

Example:

```json
"Url": "https://xxxxx.us-east-2.awsapprunner.com"
```

If Amplify hosts the WASM shell and API separately, keep Widget as `/wiley-workspace` and only swap sibling-app absolute URLs.

## Auth

- **Local:** `Auth:Enabled` = `false` → no Cognito gate (mock/open for local testing).
- **Production:** `Auth:Enabled` = `true` → single Cognito user pool (existing Wiley Widget config). Sibling apps may still use their own auth; hub only links out.

## Related docs

- `docs/shared-contracts-reference-data-api.md` — ingest API
- `docs/aws-low-cost-architecture.md` — hosting sketch
- `docs/shared-contracts-agent-suggestions.md` — prompt series
