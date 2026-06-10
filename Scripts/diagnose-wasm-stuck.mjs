import puppeteer from "puppeteer-core";

const DEBUG_URL = process.env.CHROME_DEBUG_URL ?? "http://127.0.0.1:9222";

const browser = await puppeteer.connect({
  browserURL: DEBUG_URL,
  defaultViewport: null,
});
const pages = await browser.pages();
const page =
  pages.find((p) => p.url().includes("5230")) ??
  pages.find((p) => p.url().includes("wiley")) ??
  pages[0];

console.log("Page URL:", page.url());

const bootState = await page.evaluate(() => ({
  headline: document.getElementById("wiley-static-boot-headline")?.textContent,
  detail: document.getElementById("wiley-static-boot-detail")?.textContent,
  progressLabel: document.getElementById("wiley-static-boot-progress-label")
    ?.textContent,
  progressWidth: document.getElementById("wiley-static-boot-progress")?.style
    .width,
  navToggle: !!document.getElementById("app-shell-nav-toggle"),
}));

console.log("Boot state:", JSON.stringify(bootState, null, 2));

const client = await page.createCDPSession();
await client.send("Network.enable");

const failed = [];
const pending = new Map();
const completed = [];

client.on("Network.requestWillBeSent", (e) => {
  const url = e.request.url;
  if (url.includes("_framework") || url.includes(".wasm")) {
    pending.set(e.requestId, { url, start: Date.now() });
  }
});

client.on("Network.loadingFinished", (e) => {
  const p = pending.get(e.requestId);
  if (p) {
    completed.push({ ...p, ms: Date.now() - p.start });
    pending.delete(e.requestId);
  }
});

client.on("Network.loadingFailed", (e) => {
  const p = pending.get(e.requestId);
  failed.push({
    url: p?.url ?? e.requestId,
    error: e.errorText,
    canceled: e.canceled,
  });
  pending.delete(e.requestId);
});

await new Promise((r) => setTimeout(r, 3000));

console.log("\nFailed requests:", failed.length);
failed
  .slice(0, 20)
  .forEach((f) => console.log(" FAIL", f.error, f.url?.slice(0, 120)));

console.log("\nStill pending:", pending.size);
[...pending.values()]
  .sort((a, b) => b.start - a.start)
  .slice(0, 15)
  .forEach((p) =>
    console.log(
      " PENDING",
      ((Date.now() - p.start) / 1000).toFixed(0) + "s",
      p.url?.slice(0, 120),
    ),
  );

console.log("\nRecently completed:", completed.length);
completed
  .sort((a, b) => b.ms - a.ms)
  .slice(0, 10)
  .forEach((c) =>
    console.log(" DONE", (c.ms / 1000).toFixed(1) + "s", c.url?.slice(0, 120)),
  );

const logs = await page.evaluate(() => window.__wileyBootLogs ?? null);
if (logs && logs.length > 0) {
  console.log("\nBoot failure logs:", JSON.stringify(logs, null, 2));
}

const consoleMessages = [];
page.on("console", (msg) => consoleMessages.push(msg.text()));

await page.evaluate(() => {
  if (!window.__wileyDiagDone) {
    window.__wileyDiagDone = true;
  }
});

await browser.disconnect();
