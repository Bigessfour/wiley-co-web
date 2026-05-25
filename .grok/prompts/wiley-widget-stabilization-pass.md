# Wiley Widget — Complete Stabilization Pass (Grok CLI)

Run a **full stabilization, review, and release-readiness pass** on the Wiley Co municipal finance web app (Blazor WASM + ASP.NET Core API).

**Read first:** `AGENTS.md`, `.cursor/skills/wiley-widget-completion/SKILL.md`, `docs/council-readiness-may-2026.md`, `docs/wiley-widget-functional-breakdown.md`, `docs/post-production-operations-handbook.md`, `docs/playwright-ui-test-strategy.md`

**Branch context:** `fix/workspace-api-endpoints-and-tailwind-cdn` — latest commit includes backend hardening + theme shell polish. Do not push to `main`.

**Mandatory workflow:**

1. **Plan first** — use Plan Mode / `plan` sub-agent; write stabilization plan to `.grok/logs/wiley-widget-stabilization-plan.md`
2. Fix failures before polish
3. `read_file` before every `search_replace`
4. Do not ask for confirmation between todos
5. End with a **final report** written to `.grok/logs/wiley-widget-stabilization-report.md`

---

## Phase 1 — Build & test gate (fix all failures)

Run and fix until green:

```powershell
dotnet build WileyCoWeb.csproj
dotnet build WileyCoWeb.Api/WileyCoWeb.Api.csproj
dotnet test tests/WileyCoWeb.ComponentTests/WileyCoWeb.ComponentTests.csproj --filter "Category=HighRisk"
dotnet test tests/WileyCoWeb.IntegrationTests/WileyCoWeb.IntegrationTests.csproj --filter "Category=HighRisk"
dotnet test tests/WileyWidget.Tests/WileyWidget.Tests.csproj --filter "Category=HighRisk"
npm run playwright:test:ci:highrisk
```

If file locks occur, wait/retry; do not skip failing tests.

**Canonical patterns (never regress — AGENTS.md):**

- Customer export: XlsIO via `#export-customer-grid-excel-button` — not SfGrid toolbar Excel
- QuickBooks duplicate guard: `(canonical_entity, file_hash)`
- Rate math: `EnterpriseRateService` only; `RateCalculator` is forwarder
- Jarvis: `/api/ai/*`, response field `latestUsedFallback`
- Playwright: `gotoWorkspacePanel` from `tests/playwright/support/workspace.ts`

---

## Phase 2 — Code review (security, best practices, maintainability)

Review high-impact areas and fix clear issues (minimal diffs):

| Area                        | Focus                                                        |
| --------------------------- | ------------------------------------------------------------ |
| `WileyCoWeb.Api/Program.cs` | Auth gating, exception handling, hosted service registration |
| JWT / read-auth             | `JwtAuthenticationExtensions`, `RequireWorkspaceReadAuth`    |
| `GlobalExceptionHandler`    | Sanitized 500s, 409 dupe mapping                             |
| QuickBooks import           | Validation, atomic commit, duplicate guard                   |
| `WorkspaceSnapshotComposer` | SQL filtering, perf                                          |
| `WorkspaceState` / panels   | No leaked secrets, safe defaults                             |
| Theme / MainLayout          | Dark/light persistence, a11y basics                          |

Document findings in the stabilization plan; implement P0/P1 fixes in this session.

---

## Phase 3 — Final UI/UX polish

Complete remaining Phase 6 items from `docs/wileyco-ui-rebuild-plan.md` not yet done:

- Responsive refinements across panels (laptop-first)
- Consistent loading/empty/error states
- Syncfusion theme token usage (Fluent2 light/dark)
- Keyboard focus and aria labels on primary flows
- Grid/chart performance (paging, defer init until panel visible)

Do not rewrite panels wholesale — polish only.

---

## Phase 4 — Documentation & release notes

Create or update as needed (same PR spirit, docs proximity rule):

| Artifact                                           | Purpose                                               |
| -------------------------------------------------- | ----------------------------------------------------- |
| `.grok/logs/wiley-widget-stabilization-report.md`  | **Required final deliverable** (see template below)   |
| `docs/release-notes-stabilization-2026-05.md`      | User-facing release notes for this stabilization pass |
| Update `docs/wileyco-ui-rebuild-plan.md`           | Check off completed Phase 6 items                     |
| Update `docs/wiley-widget-functional-breakdown.md` | If behavior changed                                   |

**Final report template (required sections):**

1. Executive summary (PASS / FAIL with blockers)
2. Build & test results (exact commands + pass counts)
3. Code review findings (fixed vs deferred)
4. UI/UX changes summary
5. End-to-end workflow verification (utility rate study path — see Phase 5)
6. Documentation artifacts created/updated
7. Remaining action items (prioritized P0/P1/P2)
8. PR split recommendation

---

## Phase 5 — End-to-end workflow verification

Verify a **typical utility rate study workflow** (document steps + evidence):

1. Workspace loads (snapshot or bootstrap)
2. Select enterprise + fiscal year
3. Review rates / break-even panel (math credible vs `EnterpriseRateService`)
4. Run scenario planner what-if
5. View trends / data dashboard
6. QuickBooks import preview (duplicate guard on re-import)
7. Customer directory browse + export (`#export-customer-grid-excel-button`)
8. Jarvis health + one chat turn (or document degraded fallback if no API key locally)

Use Playwright specs where they exist; note manual steps for gaps. Do not initiate broken .NET E2E per skill if env unsupported — Playwright high-risk is the browser gate.

---

## Phase 6 — Git discipline

- Fix and test before committing
- Do not commit `temp/`
- Split commits logically if multiple concerns (build fixes vs docs vs UI)
- Do not push to `main`; leave branch ready for PR

---

## Success criteria

- All Phase 1 commands green (0 test failures)
- Final report written to `.grok/logs/wiley-widget-stabilization-report.md`
- Council-critical workflow documented with evidence
- No AGENTS.md canonical regressions

Summarize in the report: files changed, test counts, remaining action items.
