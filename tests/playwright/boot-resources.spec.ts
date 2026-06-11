import { expect, test } from "@playwright/test";

/**
 * E2E gate: blazor.boot.json manifest must match files the host can serve.
 * Catches stale dotnet run / incomplete publish_output before users see a stuck boot screen.
 */
test("blazor.boot.json fingerprinted resources are all reachable", async ({
  request,
}) => {
  const bootResponse = await request.get("/_framework/blazor.boot.json");
  expect(bootResponse.ok()).toBeTruthy();

  const boot = await bootResponse.json();
  const fingerprinting = boot.resources?.fingerprinting ?? {};
  const entries = Object.entries(fingerprinting) as [string, string][];

  expect(entries.length).toBeGreaterThan(0);

  const failures: string[] = [];
  for (const [fileName] of entries) {
    const resourceResponse = await request.get(`/_framework/${fileName}`);
    if (!resourceResponse.ok()) {
      failures.push(`${fileName} -> HTTP ${resourceResponse.status()}`);
    }
  }

  const mainAssembly = boot.mainAssemblyName ?? "WileyCoWeb";
  const mainWasm = entries.find(
    ([name]) => name.startsWith(`${mainAssembly}.`) && name.endsWith(".wasm"),
  );
  expect(mainWasm, "main assembly wasm entry in boot manifest").toBeTruthy();

  if (mainWasm) {
    const mainResponse = await request.get(`/_framework/${mainWasm[0]}`);
    expect(mainResponse.ok(), `main wasm ${mainWasm[0]}`).toBeTruthy();
  }

  expect(failures, "missing boot resources").toEqual([]);
});

test("boot shell loads Blazor start script and error UI", async ({
  request,
}) => {
  const response = await request.get("/");
  expect(response.ok()).toBeTruthy();
  const body = await response.text();
  expect(body).toContain('id="wiley-static-boot-headline"');
  expect(body).toContain("js/wiley-wasm-boot.js");
  expect(body).toContain('autostart="false"');
  expect(body).toContain('id="blazor-error-ui"');
});
