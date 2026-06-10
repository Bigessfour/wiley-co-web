import puppeteer from "puppeteer-core";

const browser = await puppeteer.connect({
  browserURL: process.env.CHROME_DEBUG_URL ?? "http://127.0.0.1:9222",
});
const page = await browser.newPage();
const start = Date.now();

await page.goto("http://127.0.0.1:5230/wiley-workspace", {
  waitUntil: "domcontentloaded",
  timeout: 60000,
});

for (let i = 0; i < 90; i++) {
  const navToggle = await page.$("#app-shell-nav-toggle");
  if (navToggle) {
    console.log(`BOOT OK in ${((Date.now() - start) / 1000).toFixed(1)}s`);
    await page.close();
    await browser.disconnect();
    process.exit(0);
  }

  if (i % 3 === 0) {
    const label = await page
      .$eval("#wiley-static-boot-progress-label", (el) => el.textContent)
      .catch(() => "");
    console.log(`${((Date.now() - start) / 1000).toFixed(0)}s ${label}`);
  }

  await new Promise((r) => setTimeout(r, 2000));
}

console.log("TIMEOUT");
await page.close();
await browser.disconnect();
process.exit(1);
