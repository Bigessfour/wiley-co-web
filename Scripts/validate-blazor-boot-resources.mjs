/**
 * Validates that every fingerprinted resource in blazor.boot.json is reachable from the host.
 * Run after dotnet run / publish to catch stale dev-server or incomplete publish_output.
 *
 * Usage:
 *   node Scripts/validate-blazor-boot-resources.mjs
 *   node Scripts/validate-blazor-boot-resources.mjs --base http://127.0.0.1:5230
 */

const baseUrl = (() => {
  const idx = process.argv.indexOf("--base");
  if (idx >= 0 && process.argv[idx + 1]) {
    return process.argv[idx + 1].replace(/\/$/, "");
  }
  return (process.env.WILEYCO_E2E_BASE_URL ?? "http://127.0.0.1:5230").replace(
    /\/$/,
    "",
  );
})();

const bootUrl = `${baseUrl}/_framework/blazor.boot.json`;
const bootResponse = await fetch(bootUrl, { cache: "no-store" });
if (!bootResponse.ok) {
  console.error(`FAIL boot.json ${bootResponse.status} ${bootUrl}`);
  process.exit(1);
}

const boot = await bootResponse.json();
const resources = boot?.resources ?? {};
const fingerprinting = resources.fingerprinting ?? {};
const jsResources = resources.js ?? {};
const wasmResources = resources.wasm ?? {};
const otherResources = resources.icu ?? {}; // add other categories if present (e.g. satellite assemblies, timezones)

// Collect *all* files the runtime + app will request under _framework/
const allEntries = new Set([
  ...Object.keys(fingerprinting),
  ...Object.keys(jsResources),
  ...Object.keys(wasmResources),
  ...Object.keys(otherResources),
]);

// Also include any top-level arrays Blazor uses in some manifests
if (Array.isArray(resources.assembly)) {
  for (const a of resources.assembly) allEntries.add(a);
}
if (Array.isArray(resources.runtime)) {
  for (const r of resources.runtime) allEntries.add(r);
}

const entries = [...allEntries];

if (entries.length === 0) {
  console.error(
    "FAIL no resource entries (fingerprinting/js/wasm) in blazor.boot.json",
  );
  process.exit(1);
}

const failures = [];
const mainAssembly = boot.mainAssemblyName ?? "WileyCoWeb";

async function checkResource(fileName) {
  // Some manifests list bare names; others are already under subpaths. Normalize.
  const url = `${baseUrl}/_framework/${fileName}`;
  try {
    const response = await fetch(url, { method: "HEAD", cache: "no-store" });
    if (!response.ok) {
      failures.push({ fileName, status: response.status, url });
    }
  } catch (err) {
    failures.push({ fileName, status: "error", url, detail: String(err) });
  }
}

for (const fileName of entries) {
  await checkResource(fileName);
}

const mainWasm = entries.find(
  (name) => name.startsWith(`${mainAssembly}.`) && name.endsWith(".wasm"),
);

if (mainWasm) {
  // already checked in the loop, but keep explicit for clear logging
  const url = `${baseUrl}/_framework/${mainWasm}`;
  // re-HEAD not strictly needed; the loop already did it
}

if (failures.length > 0) {
  console.error(
    `FAIL ${failures.length}/${entries.length} boot resources missing at ${baseUrl}`,
  );
  for (const f of failures.slice(0, 30)) {
    console.error(
      `  ${f.status} ${f.fileName} ${f.detail ? `(${f.detail})` : ""}`,
    );
  }
  if (failures.length > 30) {
    console.error(`  … and ${failures.length - 30} more`);
  }
  console.error(
    "Hint: the dev server may be serving a stale or incomplete manifest. Run ./stop-local.sh && ./start-local.sh (client uses dotnet watch). If a specific runtime file like dotnet.js or dotnet.runtime.* is missing, a full clean (rm -rf bin obj) + restart may be required.",
  );
  process.exit(1);
}

console.log(
  `OK ${entries.length} boot resources (fingerprinting + js + wasm + runtime) reachable at ${baseUrl}`,
);
console.log(`OK main assembly wasm: ${mainWasm ?? "not found"}`);
if (mainWasm) {
  console.log(`OK (verified in full set)`);
}
