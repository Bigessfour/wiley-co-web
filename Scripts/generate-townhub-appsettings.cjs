#!/usr/bin/env node
/**
 * Emit wwwroot/appsettings.TownHub.local.json from TOWN_HUB_*_URL env vars.
 * Merged at WASM startup (same pattern as Auth / Workspace local settings).
 *
 * Merges env overrides onto any existing root or wwwroot TownHub.local file so
 * a partial env set does not drop previously configured keys.
 *
 * With --clear (or no env and no root file): writes {} so stale wwwroot copies
 * cannot leak into a publish.
 *
 * Usage (any subset):
 *   TOWN_HUB_APARTMENTS_URL=https://apartments.example.com \
 *   TOWN_HUB_TIKR_URL=https://tikr.example.com \
 *     node Scripts/generate-townhub-appsettings.js
 *
 * Local example:
 *   TOWN_HUB_WIDGET_URL=/wiley-workspace \
 *   TOWN_HUB_APARTMENTS_URL=http://localhost:8082 \
 *   TOWN_HUB_RURAL_WATER_URL=http://localhost:8083 \
 *   TOWN_HUB_TRASH_TRUCK_URL=http://localhost:8084 \
 *   TOWN_HUB_TIKR_URL=http://localhost:5080 \
 *     node Scripts/generate-townhub-appsettings.js
 *
 * AWS / Amplify example:
 *   TOWN_HUB_APARTMENTS_URL=https://xxxxx.us-east-2.awsapprunner.com \
 *   TOWN_HUB_RURAL_WATER_URL=https://yyyyy.us-east-2.awsapprunner.com \
 *   TOWN_HUB_TRASH_TRUCK_URL=https://zzzzz.us-east-2.awsapprunner.com \
 *   TOWN_HUB_TIKR_URL=https://ttttt.us-east-2.awsapprunner.com \
 *     node Scripts/generate-townhub-appsettings.js
 */
"use strict";

const fs = require("fs");
const path = require("path");

const root = path.resolve(__dirname, "..");
const outPath = path.join(root, "wwwroot", "appsettings.TownHub.local.json");
const rootLocalPath = path.join(root, "appsettings.TownHub.local.json");
const clearOnly = process.argv.includes("--clear");

const ENV_KEYS = {
  WidgetUrl: "TOWN_HUB_WIDGET_URL",
  ApartmentsUrl: "TOWN_HUB_APARTMENTS_URL",
  RuralWaterUrl: "TOWN_HUB_RURAL_WATER_URL",
  TrashTruckUrl: "TOWN_HUB_TRASH_TRUCK_URL",
  TikrUrl: "TOWN_HUB_TIKR_URL",
};

function pick(envKey) {
  const value = (process.env[envKey] || "").trim();
  return value.length > 0 ? value : undefined;
}

function readTownHub(filePath) {
  try {
    if (!fs.existsSync(filePath)) {
      return {};
    }
    const parsed = JSON.parse(fs.readFileSync(filePath, "utf8"));
    const section = parsed && typeof parsed === "object" ? parsed.TownHub : null;
    return section && typeof section === "object" && !Array.isArray(section) ? { ...section } : {};
  } catch {
    return {};
  }
}

const envOverrides = {};
for (const [prop, envKey] of Object.entries(ENV_KEYS)) {
  const value = pick(envKey);
  if (value) {
    envOverrides[prop] = value;
  }
}

const hasEnv = Object.keys(envOverrides).length > 0;
const hasRootLocal = fs.existsSync(rootLocalPath);

if (clearOnly || (!hasEnv && !hasRootLocal)) {
  fs.mkdirSync(path.dirname(outPath), { recursive: true });
  fs.writeFileSync(outPath, "{}\n");
  console.log(`Wrote ${outPath} (cleared / empty)`);
  process.exit(0);
}

if (!hasEnv && hasRootLocal) {
  fs.mkdirSync(path.dirname(outPath), { recursive: true });
  fs.copyFileSync(rootLocalPath, outPath);
  console.log(`Copied ${rootLocalPath} -> ${outPath}`);
  process.exit(0);
}

// Env present: merge root local (if any) then wwwroot local (if any) then env.
const merged = {
  ...readTownHub(rootLocalPath),
  ...readTownHub(outPath),
  ...envOverrides,
};

fs.mkdirSync(path.dirname(outPath), { recursive: true });
fs.writeFileSync(outPath, JSON.stringify({ TownHub: merged }, null, 2) + "\n");
console.log(`Wrote ${outPath}`);
