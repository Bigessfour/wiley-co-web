/**
 * UI audit via Chrome remote debugging (chrome-devtools-mcp transport).
 * Start Chrome: Scripts/start-chrome-debug.sh
 */
import { writeFileSync, mkdirSync } from "node:fs";
import { join } from "node:path";
import puppeteer from "puppeteer-core";

const BASE = process.env.WILEYCO_CLIENT_URL ?? "http://127.0.0.1:5230";
const DEBUG_URL = process.env.CHROME_DEBUG_URL ?? "http://127.0.0.1:9222";
const OUT = join(process.cwd(), "TestResults", "chrome-devtools-ui-audit.json");

const PANELS = [
  {
    name: "overview",
    path: "/wiley-workspace",
    selector: "#workspace-overview-dashboard",
  },
  {
    name: "break-even",
    path: "/wiley-workspace/break-even",
    selector: "#break-even-panel",
  },
  { name: "rates", path: "/wiley-workspace/rates", selector: "#rates-panel" },
  {
    name: "decision-support",
    path: "/wiley-workspace/decision-support",
    selector: "#decision-support-panel",
  },
];

async function waitWorkspaceReady(page) {
  await page.waitForSelector("#app-shell-nav-toggle", { timeout: 120000 });
  await page
    .waitForFunction(
      () => !document.querySelector("#workspace-init-skeleton"),
      { timeout: 120000 },
    )
    .catch(() => undefined);
}

async function clickByText(page, selector, text) {
  const clicked = await page.evaluate(
    (sel, label) => {
      const root = document.querySelector(sel);
      if (!root) return false;
      const nodes = root.querySelectorAll("button, a");
      for (const node of nodes) {
        if ((node.textContent ?? "").trim() === label) {
          node.click();
          return true;
        }
      }
      return false;
    },
    selector,
    text,
  );
  if (!clicked)
    throw new Error(`Could not find clickable "${text}" in ${selector}`);
}

async function duplicateIds(page) {
  return await page.evaluate(() => {
    const ids = [...document.querySelectorAll("[id]")].map((el) => el.id);
    const seen = new Map();
    for (const id of ids) {
      seen.set(id, (seen.get(id) ?? 0) + 1);
    }
    return [...seen.entries()]
      .filter(([, n]) => n > 1)
      .map(([id, count]) => ({ id, count }));
  });
}

async function main() {
  const results = [];
  const browser = await puppeteer.connect({
    browserURL: DEBUG_URL,
    defaultViewport: null,
  });
  const pages = await browser.pages();
  let page = pages.find((p) => p.url().includes("/wiley-workspace"));
  if (!page) {
    page = await browser.newPage();
    await page.goto(`${BASE}/wiley-workspace`, {
      waitUntil: "domcontentloaded",
      timeout: 120000,
    });
  } else {
    await page.bringToFront();
  }
  if (!page.url().includes("/wiley-workspace")) {
    await page.goto(`${BASE}/wiley-workspace`, {
      waitUntil: "domcontentloaded",
      timeout: 120000,
    });
  }
  await waitWorkspaceReady(page);

  const dupes = await duplicateIds(page);
  results.push({
    check: "duplicate-ids",
    status: dupes.length === 0 ? "pass" : "fail",
    detail: dupes,
  });

  try {
    await clickByText(page, "#app-shell-navigation-list", "Rates");
    await page.waitForFunction(() => location.pathname.endsWith("/rates"), {
      timeout: 15000,
    });
    results.push({ check: "left-nav Rates", status: "pass" });
  } catch (err) {
    results.push({
      check: "left-nav Rates",
      status: "fail",
      detail: String(err),
    });
  }

  try {
    await page.goto(`${BASE}/wiley-workspace`, {
      waitUntil: "domcontentloaded",
      timeout: 120000,
    });
    await waitWorkspaceReady(page);
    await page.evaluate(() => {
      const btn = [...document.querySelectorAll("button")].find(
        (b) => b.textContent?.trim() === "Open Rates",
      );
      if (!btn) throw new Error("Open Rates button missing");
      btn.click();
    });
    await page.waitForFunction(() => location.pathname.endsWith("/rates"), {
      timeout: 15000,
    });
    results.push({ check: "overview Open Rates", status: "pass" });
  } catch (err) {
    results.push({
      check: "overview Open Rates",
      status: "fail",
      detail: String(err),
    });
  }

  try {
    await page.goto(`${BASE}/wiley-workspace`, {
      waitUntil: "domcontentloaded",
      timeout: 120000,
    });
    await waitWorkspaceReady(page);
    await clickByText(page, "#workspace-navigation-card", "Break-Even");
    await page.waitForFunction(
      () => location.pathname.endsWith("/break-even"),
      { timeout: 15000 },
    );
    results.push({ check: "context-rail Break-Even", status: "pass" });
  } catch (err) {
    results.push({
      check: "context-rail Break-Even",
      status: "fail",
      detail: String(err),
    });
  }

  for (const panel of PANELS) {
    try {
      await page.goto(`${BASE}${panel.path}`, {
        waitUntil: "domcontentloaded",
        timeout: 120000,
      });
      await waitWorkspaceReady(page);
      const el = await page.$(panel.selector);
      if (!el) {
        results.push({
          panel: panel.name,
          selector: panel.selector,
          status: "fail",
          detail: "not found",
        });
        continue;
      }
      const box = await el.boundingBox();
      const visible = await el.isIntersectingViewport();
      results.push({
        panel: panel.name,
        selector: panel.selector,
        status: visible ? "pass" : "fail",
        detail: visible ? "visible" : "hidden",
        box,
      });
    } catch (err) {
      results.push({ panel: panel.name, status: "fail", detail: String(err) });
    }
  }

  mkdirSync(join(process.cwd(), "TestResults"), { recursive: true });
  writeFileSync(
    OUT,
    JSON.stringify(
      { generatedAt: new Date().toISOString(), debugUrl: DEBUG_URL, results },
      null,
      2,
    ),
  );
  console.log(`Audit written to ${OUT}`);
  await browser.disconnect();
}

main().catch((err) => {
  console.error(err);
  process.exit(1);
});
