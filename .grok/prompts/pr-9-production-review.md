# PR #9 Production Review & Merge Sign-Off (Grok CLI)

**PR:** https://github.com/Bigessfour/wiley-co-web/pull/9  
**Title:** Backend hardening, UI theme polish, and stabilization gate  
**Branch:** `fix/workspace-api-endpoints-and-tailwind-cdn` → `main`  
**Head:** `305ee20` (8 commits from main)

**Read first:** `AGENTS.md`, `.cursor/skills/wiley-widget-completion/SKILL.md`, `docs/wiley-widget-stabilization-report-2026-05.md`, `docs/release-notes-stabilization-2026-05.md`, `.grok/logs/remaining-due-outs-report.md`

**Mandatory workflow:** Plan Mode first. `read_file` before every `search_replace`. Run full gates. Do not ask for confirmation. Write deliverable to `.grok/logs/pr-9-production-review.md`.

---

## Phase 1 — Full PR review

Review **all changes vs `main`** (`git diff main...HEAD --stat` + targeted file reads):

### Backend

- JWT auth scaffolding (`JwtAuthenticationExtensions`, `RequireWorkspaceReadAuth` / mutating policies)
- `GlobalExceptionHandler` (sanitized 500, 409 dupe, Postgres 23505)
- `DuplicateImportException`, QuickBooks validation + duplicate guard `(canonical_entity, file_hash)`
- `AICacheWarmingService` + `NullAIService` DI safety
- Aurora schema alignment migration `20260525204607_SchemaAlignmentProductionReadiness`
- `WorkspaceSnapshotComposer` perf paths
- Jarvis health (`latestUsedFallback` canonical)

### Frontend

- MainLayout dark/light theme toggle, `data-wiley-theme`, Syncfusion Fluent2
- Responsive layout context, a11y on primary toggles
- `wwwroot/app.css`, `index.html`

### Tests & docs

- HighRisk coverage (Component 1, Integration 37, Widget 17)
- Playwright high-risk (4 specs)
- Release notes, ops handbook Amplify cutover checklist, stabilization reports

**Canonical guardrails (must not regress):**
| Area | Pattern |
|------|---------|
| Customer export | `#export-customer-grid-excel-button` + XlsIO |
| QB duplicate | `(canonical_entity, file_hash)` |
| Rate math | `EnterpriseRateService` only |
| Jarvis | `/api/ai/*`, `latestUsedFallback` |
| Playwright | `gotoWorkspacePanel` |

Report: risks, missing pieces, recommended fixes (P0/P1/P2). **Only implement P0 blockers** in this session; defer P1/P2 to report unless trivial doc fix.

---

## Phase 2 — Run stabilization gate

Execute exact commands; capture pass/fail counts:

```powershell
dotnet build WileyCoWeb.csproj
dotnet build WileyCoWeb.Api/WileyCoWeb.Api.csproj
dotnet test tests/WileyCoWeb.ComponentTests --filter "Category=HighRisk"
dotnet test tests/WileyCoWeb.IntegrationTests --filter "Category=HighRisk"
dotnet test tests/WileyWidget.Tests --filter "Category=HighRisk"
npm run playwright:test:ci:highrisk
```

Also inspect PR CI: `gh pr checks 9`. Fix any **local** failures or CI blockers on this branch.

---

## Phase 3 — Final sign-off & merge-ready summary

Produce `.grok/logs/pr-9-production-review.md` with:

1. **Approval recommendation:** APPROVE / APPROVE WITH CONDITIONS / REQUEST CHANGES
2. Backend robustness assessment
3. UI/UX polish assessment (theme, loading states, a11y)
4. Documentation completeness
5. **Post-merge steps** (Amplify prod cutover, SnapshotComposer perf review, deeper a11y, secret rotation — from ops handbook)
6. Suggested PR comment for merge (copy-paste ready)
7. Any last-minute tweaks implemented (list files) vs deferred

**Git discipline:** Logical commits if fixes needed. No `temp/`. No push to `main`. User merges via GitHub after CI green.

---

## Success criteria

- Full gate green (or failures explained + fixed)
- Review report written with clear merge recommendation
- P0 issues fixed or explicitly none
- AGENTS.md canonicals verified in test output
