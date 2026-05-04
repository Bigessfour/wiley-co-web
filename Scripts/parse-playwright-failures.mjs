import fs from "node:fs";

const reportPath = process.argv[2];
if (!reportPath) {
  console.error(
    "Usage: node Scripts/parse-playwright-failures.mjs <results.json>",
  );
  process.exit(1);
}

const j = JSON.parse(fs.readFileSync(reportPath, "utf8"));
const failures = [];

function walkSuite(suite, prefix) {
  const p = prefix ? `${prefix} > ${suite.title}` : suite.title;
  for (const spec of suite.specs ?? []) {
    for (const test of spec.tests ?? []) {
      for (const r of test.results ?? []) {
        if (r.status === "failed") {
          failures.push({
            path: `${p} :: ${spec.title}`,
            project: test.projectName,
            msg: r.error?.message?.slice(0, 400) ?? "",
          });
        }
      }
    }
  }
  for (const child of suite.suites ?? []) walkSuite(child, p);
}

for (const s of j.suites ?? []) walkSuite(s, "");
console.log(`failures: ${failures.length}`);
for (const f of failures) {
  console.log("\n---\n", JSON.stringify(f, null, 2));
}
