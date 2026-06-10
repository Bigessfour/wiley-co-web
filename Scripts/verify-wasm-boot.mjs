import puppeteer from "puppeteer-core";

const baseUrl = process.env.WILEYCO_E2E_BASE_URL ?? "http://127.0.0.1:5230";
const timeoutMs = Number(process.env.WILEYCO_BOOT_TIMEOUT_MS ?? 600000);

const debugUrl = process.env.CHROME_DEBUG_URL ?? "http://127.0.0.1:9222";
const browser = await puppeteer.connect({
  browserURL: debugUrl,
  defaultViewport: null,
});
const page = await browser.newPage();
const start = Date.now();

page.on("console", (msg) => console.log("console:", msg.text()));
page.on("pageerror", (err) => console.log("pageerror:", err.message));

await page.goto(`${baseUrl}/wiley-workspace`, {
  waitUntil: "domcontentloaded",
  timeout: 120000,
});

while (Date.now() - start < timeoutMs) {
  const nav = await page.$("#app-shell-nav-toggle");
  if (nav) {
    console.log(`BOOT OK in ${((Date.now() - start) / 1000).toFixed(1)}s`);
    await browser.disconnect();
    process.exit(0);
  }

  const label = await page
    .$eval("#wiley-static-boot-progress-label", (el) => el.textContent)
    .catch(() => null);
  if (label) {
    console.log(`${((Date.now() - start) / 1000).toFixed(0)}s ${label}`);
  }

  await new Promise((r) => setTimeout(r, 5000));
}

console.log(`TIMEOUT after ${timeoutMs / 1000}s`);
await browser.disconnect();
process.exit(1);
