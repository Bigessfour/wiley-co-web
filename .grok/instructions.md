# Grok Build — Wiley Widget (e2e)

Official docs: [Getting Started](https://docs.x.ai/build/overview) · [Headless & Scripting](https://docs.x.ai/build/cli/headless-scripting) · [Modes and Commands](https://docs.x.ai/build/modes-and-commands)

Read **[../AGENTS.md](../AGENTS.md)** before editing this repository.

## 1. Install

Windows (PowerShell):

```powershell
irm https://x.ai/cli/install.ps1 | iex
grok --version
```

Or use the repo helper:

```powershell
.\Scripts\setup-grok.ps1 -Install
```

## 2. Authenticate

Interactive (browser):

```powershell
cd "C:\Users\biges\Desktop\Personal Github\WW AWS"
grok login
```

Headless / CI (API key from [console.x.ai](https://console.x.ai)):

```powershell
$env:XAI_API_KEY = "xai-..."
```

Verify discovery:

```powershell
.\Scripts\setup-grok.ps1
# or: grok inspect
```

`grok inspect` should show `AGENTS.md` as project instructions and `grok-build` as the default CLI model.

> **CLI vs API model names:** Grok Build CLI uses `grok-build` (`grok models`). The xAI HTTP API uses `grok-build-0.1` for the same agent — see [Grok Build 0.1 on the API](https://docs.x.ai/build/overview). Jarvis in this repo calls the API, not the CLI.

## 3. Interactive TUI

In **Cursor / VS Code integrated terminals**, use the repo launcher so the UI does not take over the alternate screen (which renders misaligned on Windows):

```powershell
cd "C:\Users\biges\Desktop\Personal Github\WW AWS"
.\Scripts\start-grok-tui.ps1
```

Equivalent manual flag: `grok --no-alt-screen --cwd .`

In a full terminal (Windows Terminal, etc.), plain `grok` is fine.

Useful first prompts:

```text
Explain this repo.
@State/WorkspaceState.cs Walk me through workspace state.
```

Resume a prior session in the TUI: `/load` or `/resume`.

## 4. Headless backend plan (recommended)

Per [headless docs](https://docs.x.ai/build/cli/headless-scripting): use `-p` or `--prompt-file`, `-s` for named multi-turn sessions, and `--always-approve` for automation.

```powershell
# Remaining todos (recommended)
.\Scripts\run-grok-backend-plan.ps1 -Remaining -Foreground

# Full plan from scratch
.\Scripts\run-grok-backend-plan.ps1 -Foreground

# Resume named headless session (uses -s, not -c)
.\Scripts\run-grok-backend-plan.ps1 -Continue -Foreground
```

Prompts:

- [backend-production-readiness-remaining.md](prompts/backend-production-readiness-remaining.md)
- [backend-production-readiness.md](prompts/backend-production-readiness.md)

Logs: `.grok/logs/backend-production-readiness-*.log`

## 5. Session management

| Goal                       | Command                                           |
| -------------------------- | ------------------------------------------------- |
| New named headless session | `-s backend-prod-readiness-v4` (launcher default) |
| Continue named session     | `.\Scripts\run-grok-backend-plan.ps1 -Continue`   |
| Resume by UUID             | `-ResumeSessionId <uuid>`                         |
| List sessions (TUI)        | `/sessions` inside `grok`                         |

Avoid `-c` / `--continue` in this repo on Windows — sessions are stored under extended `\\?\` cwd paths and cwd lookup can fail.

## 6. Project layout

| Path                                | Purpose                             |
| ----------------------------------- | ----------------------------------- |
| `AGENTS.md`                         | Project rules (auto-loaded by Grok) |
| `.grok/config.toml`                 | Project MCP servers (optional)      |
| `.grok/prompts/`                    | Headless plan prompts               |
| `Scripts/setup-grok.ps1`            | Install + auth + inspect            |
| `Scripts/run-grok-backend-plan.ps1` | Headless plan launcher              |

## 7. Verification gates

After Grok completes a slice, run the smallest HighRisk set:

```powershell
dotnet test tests/WileyCoWeb.ComponentTests --filter "Category=HighRisk"
dotnet test tests/WileyCoWeb.IntegrationTests --filter "Category=HighRisk"
dotnet test tests/WileyWidget.Tests --filter "Category=HighRisk"
npm run playwright:test:ci:highrisk
```

Use **.NET SDK 9.0.313** (`global.json`). Do not push to `main`.
