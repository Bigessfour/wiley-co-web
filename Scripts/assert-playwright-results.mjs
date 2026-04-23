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
  throw new Error(`No Playwright tests were recorded in ${resultsPath}.`);
}

const passRate = (expected / total) * 100;

console.log(
  `[playwright] expected=${expected} unexpected=${unexpected} flaky=${flaky} skipped=${skipped} passRate=${passRate.toFixed(2)}%`,
);

if (unexpected > 0 || flaky > 0 || skipped > 0 || passRate < 100) {
  throw new Error(
    `Playwright pass-rate gate failed: expected=${expected}, unexpected=${unexpected}, flaky=${flaky}, skipped=${skipped}, passRate=${passRate.toFixed(2)}%.`,
  );
}
