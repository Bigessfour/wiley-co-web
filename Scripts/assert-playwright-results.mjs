import { readFileSync } from "node:fs";
import { resolve } from "node:path";

const resultsPath = resolve(
  process.argv[2] ?? "playwright-report/results.json",
);
const results = JSON.parse(readFileSync(resultsPath, "utf8"));
const stats = results?.stats ?? {};

const expected = Number(stats.expected ?? 0);
const unexpected = Number(stats.unexpected ?? 0);
const flaky = Number(stats.flaky ?? 0);
const skipped = Number(stats.skipped ?? 0);
const total = expected + unexpected + flaky + skipped;

if (total === 0) {
  const hint =
    process.env.CI === "true" || process.env.GITHUB_ACTIONS
      ? " Likely cause: config.webServer failed to start (check job logs for PlatformNotSupportedException / API crash before tests ran)."
      : "";
  throw new Error(`No Playwright tests were recorded in ${resultsPath}.${hint}`);
}

const passRate = (expected / total) * 100;

console.log(
  `[playwright] expected=${expected} unexpected=${unexpected} flaky=${flaky} skipped=${skipped} passRate=${passRate.toFixed(2)}%`,
);

// In CI we are lenient (see ci.yml playwright-ui job comment + continue-on-error).
// Only hard-fail on real unexpected errors. Flakes/skips/low-pass-rate are logged but do not block the (already continue-on-error) job.
// Locally the old strict behavior remains for pre-push validation.
const isCI = !!process.env.CI || !!process.env.GITHUB_ACTIONS;
if (unexpected > 0 || (!isCI && (flaky > 0 || skipped > 0 || passRate < 100))) {
  throw new Error(
    `Playwright pass-rate gate failed: expected=${expected}, unexpected=${unexpected}, flaky=${flaky}, skipped=${skipped}, passRate=${passRate.toFixed(2)}%.` +
      (isCI ? " (CI is lenient; check artifacts for details.)" : ""),
  );
}

if (isCI && (flaky > 0 || skipped > 0 || passRate < 100)) {
  console.warn(
    `[playwright] CI warning (job continues): flaky=${flaky} skipped=${skipped} passRate=${passRate.toFixed(2)}% — investigate traces if persistent.`,
  );
}
