import { spawn } from "node:child_process";
import { mkdir, writeFile } from "node:fs/promises";
import { join } from "node:path";

const hostedTargets = [
  {
    name: "main.d2ellat1y3ljd9.amplifyapp.com",
    url: "https://main.d2ellat1y3ljd9.amplifyapp.com",
  },
  {
    name: "wileywidget.townofwiley.gov",
    url: "https://wileywidget.townofwiley.gov",
  },
  {
    name: "www.wileywidget.townofwiley.gov",
    url: "https://www.wileywidget.townofwiley.gov",
  },
];

const reportsDir = join("TestResults", "playwright-hosted");
const artifactsDir = join(reportsDir, "artifacts");
const playwrightCliPath = join(
  process.cwd(),
  "node_modules",
  "@playwright",
  "test",
  "cli.js",
);

function runHostedTarget(target) {
  return new Promise((resolve) => {
    const outputDir = join(artifactsDir, target.name);
    const child = spawn(
      process.execPath,
      [playwrightCliPath, "test", "--reporter=json", "--output", outputDir],
      {
        env: {
          ...process.env,
          WILEYCO_E2E_BASE_URL: target.url,
        },
        stdio: ["ignore", "pipe", "pipe"],
      },
    );

    let stdout = "";
    let stderr = "";

    child.stdout.on("data", (chunk) => {
      stdout += chunk.toString();
    });

    child.stderr.on("data", (chunk) => {
      const text = chunk.toString();
      stderr += text;
      process.stderr.write(text);
    });

    child.on("close", (code) => {
      resolve({ code: code ?? 1, stdout, stderr });
    });

    child.on("error", (error) => {
      resolve({
        code: 1,
        stdout,
        stderr: `${stderr}${error.stack ?? error.message}\n`,
      });
    });
  });
}

function readStats(report) {
  const stats = report?.stats ?? {};

  return {
    expected: Number(stats.expected ?? 0),
    unexpected: Number(stats.unexpected ?? 0),
    flaky: Number(stats.flaky ?? 0),
    skipped: Number(stats.skipped ?? 0),
    durationMs: Number(stats.duration ?? 0),
  };
}

function formatDuration(durationMs) {
  return `${(durationMs / 1000).toFixed(1)}s`;
}

await mkdir(reportsDir, { recursive: true });
await mkdir(artifactsDir, { recursive: true });

const summaries = [];
let hasFailures = false;

for (const target of hostedTargets) {
  console.log(`\n[hosted] Testing ${target.url}`);

  const result = await runHostedTarget(target);
  const reportPath = join(reportsDir, `${target.name}.json`);
  const stderrPath = join(reportsDir, `${target.name}.stderr.log`);

  if (result.stdout.trim()) {
    await writeFile(reportPath, result.stdout, "utf8");
  }

  if (result.stderr.trim()) {
    await writeFile(stderrPath, result.stderr, "utf8");
  }

  let summary = {
    host: target.name,
    url: target.url,
    exitCode: result.code,
    expected: 0,
    unexpected: 0,
    flaky: 0,
    skipped: 0,
    durationMs: 0,
    reportPath,
  };

  try {
    const parsed = JSON.parse(result.stdout);
    summary = {
      ...summary,
      ...readStats(parsed),
    };
  } catch (error) {
    summary = {
      ...summary,
      parseError: error instanceof Error ? error.message : String(error),
    };
  }

  summaries.push(summary);

  const targetFailed =
    result.code !== 0 ||
    summary.unexpected > 0 ||
    typeof summary.parseError === "string";

  hasFailures ||= targetFailed;

  const statusLabel = targetFailed ? "FAILED" : "PASSED";
  console.log(
    `[hosted] ${statusLabel} ${target.name} expected=${summary.expected} unexpected=${summary.unexpected} flaky=${summary.flaky} skipped=${summary.skipped} duration=${formatDuration(summary.durationMs)}`,
  );

  if (summary.parseError) {
    console.log(
      `[hosted] Could not parse JSON report for ${target.name}: ${summary.parseError}`,
    );
  }
}

const summaryPath = join(reportsDir, "summary.json");
await writeFile(
  summaryPath,
  `${JSON.stringify({ summaries }, null, 2)}\n`,
  "utf8",
);

console.log("\n[hosted] Summary");
for (const summary of summaries) {
  const statusLabel =
    summary.exitCode === 0 && summary.unexpected === 0 && !summary.parseError
      ? "PASS"
      : "FAIL";
  console.log(`[hosted] ${statusLabel} ${summary.host} -> ${summary.url}`);
}
console.log(`[hosted] Wrote ${summaryPath}`);

process.exitCode = hasFailures ? 1 : 0;
