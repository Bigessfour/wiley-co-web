import { execFileSync } from "node:child_process";
import { existsSync } from "node:fs";
import { join } from "node:path";

export default async function globalSetup() {
  const managedLocalRun =
    !process.env.WILEYCO_E2E_BASE_URL ||
    process.env.WILEYCO_E2E_BASE_URL === "http://localhost:5230";

  if (!managedLocalRun || process.platform !== "win32") {
    return;
  }

  const scriptPath = join(process.cwd(), "Scripts", "kill-test-processes.ps1");

  if (!existsSync(scriptPath)) {
    return;
  }

  execFileSync(
    "pwsh",
    ["-NoProfile", "-ExecutionPolicy", "Bypass", "-File", scriptPath],
    { stdio: "inherit" },
  );
}
