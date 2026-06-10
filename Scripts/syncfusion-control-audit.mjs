/**
 * Full Syncfusion control audit via Chrome remote debugging (CDP / chrome-devtools-mcp transport).
 * Usage:
 *   ./Scripts/start-chrome-debug.sh http://127.0.0.1:5230/wiley-workspace
 *   node Scripts/syncfusion-control-audit.mjs
 */
import { writeFileSync, mkdirSync } from "node:fs";
import { join } from "node:path";
import { chromium } from "@playwright/test";

const BASE = (
  process.env.WILEYCO_CLIENT_URL ?? "http://127.0.0.1:5230"
).replace(/\/$/, "");
const DEBUG_URL = process.env.CHROME_DEBUG_URL ?? "http://127.0.0.1:9222";
const OUT_MD = join(
  process.cwd(),
  "TestResults",
  "syncfusion-control-audit.md",
);
const OUT_JSON = join(
  process.cwd(),
  "TestResults",
  "syncfusion-control-audit.json",
);

/** Public-facing routes and primary panel anchors */
const PAGES = [
  { name: "Home", path: "/", panelSelector: ".home-hub", expectsSf: false },
  {
    name: "Budget Dashboard",
    path: "/budget-dashboard",
    panelSelector: "#workspace-overview-dashboard",
    expectsSf: true,
  },
  {
    name: "Workspace Overview",
    path: "/wiley-workspace",
    panelSelector: "#workspace-overview-dashboard",
    expectsSf: true,
  },
  {
    name: "Break-Even",
    path: "/wiley-workspace/break-even",
    panelSelector: "#break-even-panel",
    expectsSf: true,
  },
  {
    name: "Apartment Config",
    path: "/wiley-workspace/apartment-config",
    panelSelector: "#apartment-config-panel",
    expectsSf: true,
  },
  {
    name: "Rates",
    path: "/wiley-workspace/rates",
    panelSelector: "#rates-panel",
    expectsSf: true,
  },
  {
    name: "QuickBooks Import",
    path: "/wiley-workspace/quickbooks-import",
    panelSelector: "#quickbooks-import-panel",
    expectsSf: true,
  },
  {
    name: "Scenario Planner",
    path: "/wiley-workspace/scenario",
    panelSelector: "#scenario-panel",
    expectsSf: true,
  },
  {
    name: "Customer Viewer",
    path: "/wiley-workspace/customers",
    panelSelector: "#customer-viewer-panel",
    expectsSf: true,
  },
  {
    name: "Affordability",
    path: "/wiley-workspace/affordability",
    panelSelector: "#affordability-dashboard-panel",
    expectsSf: true,
  },
  {
    name: "Debt Coverage",
    path: "/wiley-workspace/debt-coverage",
    panelSelector: "#debt-coverage-panel",
    expectsSf: true,
  },
  {
    name: "Capital Gap",
    path: "/wiley-workspace/capital-gap",
    panelSelector: "#capital-gap-panel",
    expectsSf: true,
  },
  {
    name: "Reserve Trajectory",
    path: "/wiley-workspace/reserve-trajectory",
    panelSelector: "#reserve-trajectory-panel",
    expectsSf: true,
  },
  {
    name: "Trends",
    path: "/wiley-workspace/trends",
    panelSelector: "#trends-panel",
    expectsSf: true,
  },
  {
    name: "Decision Support",
    path: "/wiley-workspace/decision-support",
    panelSelector: "#decision-support-panel",
    expectsSf: true,
  },
  {
    name: "Data Dashboard",
    path: "/wiley-workspace/data-dashboard",
    panelSelector: "#data-dashboard-panel",
    expectsSf: true,
  },
];

/** Known high-value controls to exercise explicitly (per page path suffix) */
const EXPECTED_CONTROLS = {
  "/wiley-workspace": [
    {
      id: "export-customers-excel-button",
      type: "SfButton",
      action: "click-expand-details",
      detailsId: "workspace-document-center",
    },
    {
      id: "export-scenario-excel-button",
      type: "SfButton",
      action: "click-expand-details",
      detailsId: "workspace-document-center",
    },
    {
      id: "export-workspace-pdf-button",
      type: "SfButton",
      action: "click-expand-details",
      detailsId: "workspace-document-center",
    },
    { id: "enterprise-select", type: "SfDropDownList", action: "open-ddl" },
    { id: "fiscal-year-select", type: "SfDropDownList", action: "open-ddl" },
    { id: "scenario-name-input", type: "SfTextBox", action: "focus" },
    {
      id: "saved-scenarios-dropdown",
      type: "SfDropDownList",
      action: "open-ddl",
    },
    { id: "app-shell-nav-toggle", type: "SfButton", action: "click" },
    {
      id: "workspace-panel-toggle",
      type: "SfButton",
      action: "click-mobile-only",
      viewport: { width: 1279, height: 900 },
    },
    { id: "workspace-jarvis-launcher", type: "SfButton", action: "click" },
  ],
  "/wiley-workspace/rates": [
    { id: "current-rate-input", type: "SfNumericTextBox", action: "focus" },
    { id: "rates-comparison-chart", type: "SfChart", action: "visible" },
  ],
  "/wiley-workspace/break-even": [
    {
      id: "break-even-chart-water-utility",
      type: "SfAccumulationChart",
      action: "visible",
    },
    { id: "break-even-input-row", type: "panel", action: "visible" },
  ],
  "/wiley-workspace/apartment-config": [
    {
      selector: "#apartment-config-panel .e-grid",
      type: "SfGrid",
      action: "visible",
    },
  ],
  "/wiley-workspace/customers": [
    { id: "customer-directory-grid", type: "SfGrid", action: "visible" },
    { id: "add-customer-button", type: "SfButton", action: "click-dialog" },
  ],
  "/wiley-workspace/quickbooks-import": [
    {
      id: "quickbooks-import-uploader-host",
      type: "SfUploader",
      action: "visible-browse",
      scrollId: "quickbooks-import-uploader-card",
    },
    {
      selector: "button:has-text('Analyze file')",
      type: "SfButton",
      action: "visible",
    },
  ],
  "/wiley-workspace/scenario": [
    { id: "scenario-grid", type: "SfGrid", action: "visible" },
  ],
  "/wiley-workspace/affordability": [
    { id: "affordability-gauge", type: "SfCircularGauge", action: "visible" },
    {
      id: "affordability-class-impact-chart",
      type: "SfChart",
      action: "visible",
    },
    { id: "affordability-heatmap-panel", type: "SfHeatMap", action: "visible" },
  ],
  "/wiley-workspace/debt-coverage": [
    {
      id: "debt-coverage-dscr-gauge",
      type: "SfCircularGauge",
      action: "visible",
    },
    { id: "debt-coverage-waterfall-chart", type: "SfChart", action: "visible" },
  ],
  "/wiley-workspace/capital-gap": [
    { id: "capital-gap-chart", type: "SfChart", action: "visible" },
  ],
  "/wiley-workspace/reserve-trajectory": [
    { id: "reserve-trajectory-chart", type: "SfChart", action: "visible" },
  ],
  "/wiley-workspace/trends": [
    { id: "trends-projection-chart", type: "SfChart", action: "visible" },
  ],
  "/wiley-workspace/decision-support": [
    {
      selector: "#decision-support-panel #jarvis-chat-ui",
      type: "SfAssistView",
      action: "visible",
    },
  ],
  "/wiley-workspace/data-dashboard": [
    { id: "data-dashboard-panel", type: "panel", action: "visible" },
  ],
};

async function waitWorkspaceReady(page) {
  await page.waitForSelector("#app-shell-nav-toggle", { timeout: 120000 });
  await page
    .waitForFunction(
      () => !document.querySelector("#workspace-init-skeleton"),
      {
        timeout: 120000,
      },
    )
    .catch(() => undefined);
  await page.waitForTimeout(1500);
}

function shouldSkipDiscoveredControl(meta) {
  const id = meta?.id || "";
  const cls = meta?.classes || "";
  if (cls.includes("e-tooltip") && (!meta.visible || (meta.box?.w ?? 0) < 2))
    return true;
  if (cls.includes("e-file-clear-btn") || cls.includes("e-file-upload-btn"))
    return true;
  if (cls.includes("e-uploader") && (meta.box?.w ?? 0) < 2) return true;
  if (id.startsWith("tooltip-") && (!meta.visible || (meta.box?.w ?? 0) < 2))
    return true;
  return false;
}

function classifySfType(meta) {
  const cls =
    typeof meta === "string" ? meta : meta?.classes || meta?.className || "";
  const id = meta?.id || "";
  if (cls.includes("e-grid")) return "SfGrid";
  if (cls.includes("e-chart")) return "SfChart";
  if (cls.includes("e-circulargauge")) return "SfCircularGauge";
  if (cls.includes("e-heatmap")) return "SfHeatMap";
  if (cls.includes("e-accumulationchart")) return "SfAccumulationChart";
  if (cls.includes("e-btn")) return "SfButton";
  if (cls.includes("e-dropdownlist") || cls.includes("e-ddl"))
    return "SfDropDownList";
  if (cls.includes("e-numerictextbox")) return "SfNumericTextBox";
  if (cls.includes("e-input-group")) return "SfTextBox";
  if (cls.includes("e-uploader")) return "SfUploader";
  if (cls.includes("e-progress")) return "SfProgressBar";
  if (cls.includes("e-card")) return "SfCard";
  if (cls.includes("e-splitter")) return "SfSplitter";
  if (cls.includes("e-dialog")) return "SfDialog";
  if (cls.includes("e-assistview") || id.includes("jarvis"))
    return "SfAssistView";
  if (cls.includes("e-control")) return "SfControl";
  return "unknown";
}

async function auditDiscoveredControls(page, rootSelector) {
  return page.evaluate((rootSel) => {
    const root = rootSel ? document.querySelector(rootSel) : document.body;
    if (!root) return [];

    const nodes = [
      ...root.querySelectorAll(
        ".e-control, .e-grid, .e-chart, .e-btn, .e-card, .e-dialog, .e-splitter, .e-circulargauge, .e-heatmap, .e-uploader, .e-progress, .e-input-group",
      ),
    ];

    const seen = new Set();
    const results = [];

    for (const el of nodes) {
      const key = el.id || el.className?.toString().slice(0, 80) + el.tagName;
      if (seen.has(key)) continue;
      seen.add(key);

      const rect = el.getBoundingClientRect();
      const style = window.getComputedStyle(el);
      const visible =
        rect.width > 0 &&
        rect.height > 0 &&
        style.visibility !== "hidden" &&
        style.display !== "none" &&
        style.opacity !== "0";

      const disabled =
        el.hasAttribute("disabled") ||
        el.getAttribute("aria-disabled") === "true" ||
        el.classList.contains("e-disabled");

      const spacingIssues = [];
      if (rect.width < 2) spacingIssues.push("zero-width");
      if (rect.height < 2) spacingIssues.push("zero-height");
      if (rect.right < 0 || rect.bottom < 0) spacingIssues.push("off-screen");
      if (rect.top > window.innerHeight) spacingIssues.push("below-fold-only");

      let overlap = false;
      if (visible && rect.width > 10 && rect.height > 10) {
        const cx = rect.left + rect.width / 2;
        const cy = rect.top + rect.height / 2;
        const topEl = document.elementFromPoint(cx, cy);
        if (topEl && !el.contains(topEl) && !topEl.contains(el)) overlap = true;
      }

      results.push({
        id: el.id || null,
        tag: el.tagName.toLowerCase(),
        classes: [...el.classList]
          .filter((c) => c.startsWith("e-"))
          .slice(0, 6)
          .join(" "),
        ariaLabel:
          el.getAttribute("aria-label") || el.getAttribute("title") || null,
        text: (el.textContent || "").trim().slice(0, 60) || null,
        visible,
        disabled,
        box: {
          w: Math.round(rect.width),
          h: Math.round(rect.height),
          x: Math.round(rect.left),
          y: Math.round(rect.top),
        },
        spacingIssues,
        overlap,
      });
    }

    return results;
  }, rootSelector);
}

async function scrollAuditTarget(page, control, locator) {
  const scrollId = control.scrollId || control.id;
  if (scrollId) {
    await page
      .locator(`#${scrollId}`)
      .scrollIntoViewIfNeeded()
      .catch(() => undefined);
  } else {
    await locator
      .first()
      .scrollIntoViewIfNeeded()
      .catch(() => undefined);
  }
  await page.waitForTimeout(250);
}

async function testExpectedControl(page, control) {
  const name = control.id || control.selector;
  const locator = control.id
    ? page.locator(`#${control.id}`)
    : page.locator(control.selector).first();

  const result = {
    name,
    type: control.type,
    action: control.action,
    status: "unknown",
    notes: [],
  };

  try {
    const count = await locator.count();
    if (count === 0) {
      result.status = "fail";
      result.notes.push("not found in DOM");
      return result;
    }

    if (control.action !== "click-mobile-only") {
      await scrollAuditTarget(page, control, locator);
    }

    if (control.action === "visible-browse") {
      const browse = page.locator(
        "#quickbooks-import-uploader-host .e-upload-browse-btn, #quickbooks-import-uploader-host .e-file-select-wrap .e-btn, #quickbooks-import-uploader-card .e-upload-browse-btn",
      );
      const browseVisible = await browse
        .first()
        .isVisible()
        .catch(() => false);
      result.status = browseVisible ? "pass" : "fail";
      if (!browseVisible)
        result.notes.push("SfUploader browse button not visible");
      return result;
    }

    const visible = await locator.isVisible().catch(() => false);
    if (!visible && control.action !== "click-dialog") {
      result.status = "fail";
      result.notes.push("not visible");
      return result;
    }

    const box = await locator.boundingBox().catch(() => null);
    if (
      box &&
      (box.width < 2 || box.height < 2) &&
      control.action !== "open-ddl"
    ) {
      result.status = "fail";
      result.notes.push("zero-size bounding box");
      return result;
    }

    if (control.action === "visible") {
      result.status = visible ? "pass" : "fail";
      return result;
    }

    if (control.action === "focus") {
      await locator.focus({ timeout: 5000 });
      result.status = "pass";
      return result;
    }

    if (control.action === "open-ddl") {
      const wrapper = control.id
        ? page
            .locator(`#${control.id}`)
            .locator(
              "xpath=ancestor::*[contains(@class,'e-input-group') or contains(@class,'e-ddl')][1]",
            )
        : locator;
      await wrapper.scrollIntoViewIfNeeded().catch(() => undefined);
      await wrapper.click({ timeout: 5000, force: true });
      const popup = page
        .locator(".e-popup.e-ddl, .e-popup.e-dropdownbase")
        .first();
      const opened = await popup.isVisible().catch(() => false);
      result.status = opened ? "pass" : "warn";
      if (!opened)
        result.notes.push("dropdown popup not detected after wrapper click");
      await page.keyboard.press("Escape").catch(() => undefined);
      return result;
    }

    if (control.action === "click-expand-details") {
      if (control.detailsId) {
        await page.locator(`#${control.detailsId}`).evaluate((el) => {
          if (el instanceof HTMLDetailsElement) el.open = true;
        });
      }
      const enabled = await locator.isEnabled().catch(() => true);
      if (!enabled) {
        result.status = "warn";
        result.notes.push("disabled — skipped click");
        return result;
      }
      await locator.click({ timeout: 5000, force: true });
      result.status = "pass";
      return result;
    }

    if (control.action === "click-mobile-only") {
      if (control.viewport) await page.setViewportSize(control.viewport);
      const mobileLocator = page.locator(`#${control.id}`);
      const count = await mobileLocator.count();
      if (count === 0) {
        result.status = "skip";
        result.notes.push(
          "only rendered below lg breakpoint — skipped on desktop audit",
        );
        return result;
      }
      await mobileLocator.click({ timeout: 5000 });
      result.status = "pass";
      return result;
    }

    if (control.action === "click" || control.action === "click-dialog") {
      const enabled = await locator.isEnabled().catch(() => true);
      if (!enabled) {
        result.status = "warn";
        result.notes.push("disabled — skipped click");
        return result;
      }
      await locator.click({ timeout: 5000 });
      if (control.action === "click-dialog") {
        const dialog = page.getByRole("dialog").first();
        const dlgVisible = await dialog.isVisible().catch(() => false);
        result.status = dlgVisible ? "pass" : "fail";
        if (dlgVisible) {
          await page.keyboard.press("Escape").catch(() => undefined);
        } else {
          result.notes.push("expected dialog did not open");
        }
        return result;
      }
      result.status = "pass";
      return result;
    }

    result.status = visible ? "pass" : "fail";
  } catch (err) {
    result.status = "fail";
    result.notes.push(String(err.message || err).slice(0, 200));
  }

  return result;
}

function statusEmoji(status) {
  if (status === "pass") return "PASS";
  if (status === "warn") return "WARN";
  if (status === "fail") return "FAIL";
  if (status === "skip") return "SKIP";
  if (status === "unknown") return "UNKNOWN";
  return "UNKNOWN";
}

function renderMarkdown(report) {
  const lines = [];
  lines.push("# Syncfusion Control Audit Report");
  lines.push("");
  lines.push(`Generated: ${report.generatedAt}`);
  lines.push(`Base URL: ${report.baseUrl}`);
  lines.push(`Chrome debug: ${report.debugUrl}`);
  lines.push("");
  lines.push("## Summary");
  lines.push("");
  lines.push(`| Metric | Count |`);
  lines.push(`|--------|------:|`);
  lines.push(`| Pages audited | ${report.summary.pages} |`);
  lines.push(`| Expected controls tested | ${report.summary.expected} |`);
  lines.push(`| Discovered Syncfusion nodes | ${report.summary.discovered} |`);
  lines.push(`| PASS | ${report.summary.pass} |`);
  lines.push(`| WARN | ${report.summary.warn} |`);
  lines.push(`| FAIL | ${report.summary.fail} |`);
  lines.push("");
  lines.push("## Non-working / needs troubleshooting");
  lines.push("");
  const failures = report.pages
    .flatMap((p) =>
      [
        ...p.expected,
        ...p.discovered.filter((d) => d.auditStatus === "fail"),
      ].map((c) => ({
        page: p.name,
        ...c,
      })),
    )
    .filter((c) => c.status === "fail" || c.auditStatus === "fail");

  if (failures.length === 0) {
    lines.push(
      "_No hard failures recorded. Review WARN rows for spacing/interaction caveats._",
    );
  } else {
    for (const f of failures) {
      const label = f.name || f.id || f.classes || "control";
      lines.push(
        `- **${f.page}** — \`${label}\` (${f.type || classifySfType(f)}) — ${(f.notes || f.spacingIssues || []).join("; ")}`,
      );
    }
  }
  lines.push("");

  for (const page of report.pages) {
    lines.push(`## ${page.name}`);
    lines.push("");
    lines.push(`- **Route:** \`${page.path}\``);
    lines.push(
      `- **Panel anchor:** \`${page.panelSelector}\` — ${page.panelStatus}`,
    );
    if (page.pageError) lines.push(`- **Page error:** ${page.pageError}`);
    lines.push("");

    lines.push("### Expected controls (exercised)");
    lines.push("");
    lines.push("| Control | Type | Action | Status | Notes |");
    lines.push("|---------|------|--------|--------|-------|");
    for (const c of page.expected) {
      lines.push(
        `| ${c.name} | ${c.type} | ${c.action} | ${statusEmoji(c.status)} | ${(c.notes || []).join("; ") || "—"} |`,
      );
    }
    lines.push("");

    lines.push("### Discovered Syncfusion controls (visibility & spacing)");
    lines.push("");
    lines.push(
      "| ID / label | Classes | Visible | Size (wxh) | Spacing | Status |",
    );
    lines.push(
      "|------------|---------|---------|------------|---------|--------|",
    );
    for (const d of page.discovered) {
      const label = d.id || d.text || d.ariaLabel || "(anonymous)";
      const spacing =
        [...(d.spacingIssues || []), d.overlap ? "occluded" : ""]
          .filter(Boolean)
          .join(", ") || "—";
      const st = d.auditStatus || (d.visible ? "pass" : "fail");
      lines.push(
        `| ${label} | ${d.classes || "—"} | ${d.visible ? "yes" : "no"} | ${d.box?.w ?? 0}x${d.box?.h ?? 0} | ${spacing} | ${statusEmoji(st)} |`,
      );
    }
    lines.push("");
  }

  return lines.join("\n");
}

async function main() {
  const browser = await chromium.connectOverCDP(DEBUG_URL);
  const context = browser.contexts()[0] ?? (await browser.newContext());
  let page = context.pages().find((p) => p.url().includes("wiley-workspace"));
  if (!page) {
    page = await context.newPage();
  }

  const report = {
    generatedAt: new Date().toISOString(),
    baseUrl: BASE,
    debugUrl: DEBUG_URL,
    pages: [],
    summary: {
      pages: 0,
      expected: 0,
      discovered: 0,
      pass: 0,
      warn: 0,
      fail: 0,
    },
  };

  for (const pageDef of PAGES) {
    const pageResult = {
      name: pageDef.name,
      path: pageDef.path,
      panelSelector: pageDef.panelSelector,
      panelStatus: "unknown",
      pageError: null,
      expected: [],
      discovered: [],
    };

    const errors = [];
    page.on("pageerror", (err) => errors.push(err.message));

    try {
      await page.goto(`${BASE}${pageDef.path}`, {
        waitUntil: "domcontentloaded",
        timeout: 120000,
      });

      if (
        pageDef.path.startsWith("/wiley-workspace") ||
        pageDef.path === "/budget-dashboard"
      ) {
        await waitWorkspaceReady(page);
        if (
          pageDef.path.includes("debt-coverage") ||
          pageDef.path.includes("capital-gap")
        ) {
          await page.waitForTimeout(4000);
        }
      } else {
        await page.waitForTimeout(2000);
      }

      const panel = page.locator(pageDef.panelSelector).first();
      const panelCount = await panel.count();
      if (panelCount === 0) {
        pageResult.panelStatus = "FAIL — anchor not found";
      } else {
        const panelVisible = await panel.isVisible().catch(() => false);
        pageResult.panelStatus = panelVisible
          ? "PASS — visible"
          : "WARN — present but hidden";
      }

      const expectedList = EXPECTED_CONTROLS[pageDef.path] ?? [];
      for (const ctrl of expectedList) {
        const tested = await testExpectedControl(page, ctrl);
        pageResult.expected.push(tested);
      }

      const rootSel =
        pageDef.path === "/" ? ".home-hub" : pageDef.panelSelector;
      await page.keyboard.press("Escape").catch(() => undefined);
      await page
        .locator(".e-dlg-overlay")
        .click({ force: true, timeout: 1000 })
        .catch(() => undefined);

      const discovered = await auditDiscoveredControls(page, rootSel);
      pageResult.discovered = discovered
        .filter((d) => !(d.id || "").includes("LegendTemplateCollection"))
        .filter((d) => !shouldSkipDiscoveredControl(d))
        .map((d) => {
          let auditStatus = "pass";
          if (!d.visible) auditStatus = "fail";
          else if (d.overlap || (d.spacingIssues && d.spacingIssues.length > 0))
            auditStatus = "warn";
          return { ...d, auditStatus, type: classifySfType(d) };
        });

      if (errors.length) pageResult.pageError = errors.join(" | ");
    } catch (err) {
      pageResult.panelStatus = `FAIL — ${String(err.message || err).slice(0, 200)}`;
      pageResult.pageError = String(err.message || err);
      if (String(err.message || err).includes("has been closed")) {
        try {
          page = await context.newPage();
        } catch {
          // Best-effort recovery; remaining pages may still fail.
        }
      }
    }

    report.pages.push(pageResult);
  }

  for (const p of report.pages) {
    report.summary.pages += 1;
    for (const e of p.expected) {
      report.summary.expected += 1;
      report.summary[e.status] = (report.summary[e.status] ?? 0) + 1;
    }
    for (const d of p.discovered) {
      report.summary.discovered += 1;
      report.summary[d.auditStatus] = (report.summary[d.auditStatus] ?? 0) + 1;
    }
  }

  mkdirSync(join(process.cwd(), "TestResults"), { recursive: true });
  writeFileSync(OUT_JSON, JSON.stringify(report, null, 2));
  writeFileSync(OUT_MD, renderMarkdown(report));
  console.log(`Audit written:\n  ${OUT_MD}\n  ${OUT_JSON}`);

  // Do not browser.close() when attached over CDP — that terminates the user's debug Chrome.
}

main().catch((err) => {
  console.error(err);
  process.exit(1);
});
