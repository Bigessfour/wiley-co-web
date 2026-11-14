---
name: production-readiness-gap-analysis
description: >-
  Audits a repository against a small-team production-readiness framework
  (Google SRE production readiness review and launch checklist, AWS
  Well-Architected and operational readiness reviews, OWASP ASVS, NIST, CIS,
  WCAG 2.2, Nielsen usability heuristics) and writes an evidence-backed gap
  report at docs/production-readiness-gap-report.md. Use when asked what
  remains before an app is production ready, done, launch-ready, or ready for
  users, or when the user asks for a production readiness review, gap analysis,
  or launch checklist. Read-only: every finding cites a file and line or
  command output, and the audit changes nothing except the report.
---

# Production readiness gap analysis

Produce `docs/production-readiness-gap-report.md`: a complete, evidence-backed list of every
item that must be done before this project can be called production ready, prioritized and
assigned to an owner type. This is an audit, not a fix-up pass.

The framework below adapts Google SRE's Production Readiness Review and launch checklist,
AWS Well-Architected + Operational Readiness Reviews, Azure Well-Architected, Susan Fowler's
eight production-readiness standards, OWASP ASVS/Top 10, NIST SSDF/CSF and CIS Controls,
12-Factor, DORA, Scrum's Definition of Done, WCAG 2.2, and Nielsen's usability heuristics,
scaled for small teams whose end users are non-technical staff.

## Ground rules

1. **Evidence or it didn't happen.** Every finding cites a `path:line`, a command plus its
   output excerpt, a CI run, an issue/PR number, or a doc quote. No evidence = no finding.
2. **No guessing about things outside the repo.** Cloud console settings, DNS, IdP config,
   real user accounts, backups actually running, licenses purchased, etc. go in the
   **Unverifiable from repo** list with the exact question a human must answer.
3. **Read-only.** Do not change app code, config, infra, or cloud resources. Only write the
   report file (and a scratch dir under `.prr/` if needed, which you delete or gitignore).
   Do not open issues, PRs, or comments unless the user explicitly asks.
4. **Don't print secrets.** If you find one, report path:line and type, redact the value.
5. **Be exhaustive, then prioritize.** Walk every category even if early ones look bad.
   Mark a check `N/A` only with a one-line reason.
6. Prefer tools already in the repo (scripts, Makefile, package scripts, CI steps). If a scanner
   isn't installed and can't be installed without network/admin, record it as "not run" and
   list it under Unverifiable — never claim a pass you didn't observe.

## Phase 1 — Discover scope ("what does done mean here?")

Build the intended scope before judging completeness.

- Read: `README*`, `docs/**`, `CONTRIBUTING*`, `CHANGELOG*`, ADRs, roadmap/plan/spec files,
  `*.md` at root, `AGENTS.md`/`.cursor/rules`, issue/PR templates.
- If `gh` is authenticated: `gh issue list --state open --limit 200`,
  `gh pr list --state open`, `gh issue list --label bug`, milestones
  (`gh api repos/{owner}/{repo}/milestones`). Record open work as candidate gaps.
- Extract: target users and their skill level, features promised, environments
  (dev/stage/prod), non-functional targets (uptime, response time, data retention),
  compliance obligations (public records, privacy, accessibility law, payments).
- Output a **Scope statement** (5–15 bullets) and a **Feature inventory** table:
  feature | promised where | implemented? (evidence) | tested? (evidence).
- If no explicit requirements exist, that itself is a finding (Scope category, Must).

## Phase 2 — Inventory architecture

- Languages/frameworks: manifests (`*.csproj`, `*.sln`, `package.json`, `pyproject.toml`,
  `requirements*.txt`, `go.mod`, `pom.xml`, `Cargo.toml`, `Gemfile`).
- Deployables: front ends, APIs, workers, functions, DBs, queues, storage, third-party APIs.
- Hosting/IaC: `amplify/**`, `amplify.yml`, `apprunner.yaml`, `Dockerfile*`,
  `docker-compose*`, `*.tf`, `cdk*`, `template.y*ml` (SAM/CFN), `serverless.yml`,
  `k8s/**`, `.github/workflows/**`, other CI configs.
- Auth: identity provider, login flow, token storage, roles/groups, how APIs validate tokens.
- Config: `appsettings*.json`, `.env*`, `environment*.ts`, `config/**`, secrets references.
- Draw a text diagram: components → dependencies → data stores → external services. Mark
  every single point of failure and every hop that crosses a trust boundary.

## Phase 3 — Run the checks

For each category: run the evidence commands (adapt globs to the stack), record observations,
then score each check **Pass / Partial / Fail / Unverifiable / N/A**. Use `rg` (ripgrep);
add `-g '!**/node_modules/**' -g '!**/bin/**' -g '!**/obj/**' -g '!**/dist/**'` as needed.

### C1. Scope & requirements completeness
- Checks: every promised feature exists; open issues/PRs/milestones triaged; acceptance
  criteria exist; known "phase 2/later" items explicitly deferred, not silently missing.
- Evidence: Phase 1 feature inventory; `gh issue list`; `rg -n -i "phase [0-9]|roadmap|out of scope|deferred"`.
- Pass: every in-scope feature has implementation + test evidence; deferrals are written down
  and agreed.

### C2. Functional completeness (stubs, placeholders, dead ends)
- Evidence:
  - `rg -n -i "\b(TODO|FIXME|HACK|XXX|TBD|WIP)\b"`
  - `rg -n "NotImplementedException|NotImplementedError|throw new Error\(['\"]not implemented|unimplemented!|todo!\("`
  - `rg -n -i "REPLACE_ME|REPLACE_WITH|CHANGEME|placeholder|lorem ipsum|example\.com|your[-_ ]?(api[-_ ]?key|domain|url)|coming soon|dummy|fake data|mock(ed)? data"`
  - Hard-coded URLs/hosts: `rg -n "https?://(localhost|127\.0\.0\.1|[0-9]{1,3}(\.[0-9]{1,3}){3})"`, `rg -n "http://"` (non-TLS)
  - UI dead ends: links/cards/buttons with `href="#"`, empty handlers, disabled features, routes
    with no page: `rg -n "href=\"#\"|onclick=\"\"|@onclick=\"\(\) => \{\s*\}\""`
  - Feature flags/dev toggles left on: `rg -n -i "feature.?flag|useMock|UseFake|isDemo|DEBUG\s*=\s*true"`
- Pass: no placeholders in shipped paths; every TODO either fixed or tracked by an issue and
  judged non-blocking; every visible UI element does something real.

### C3. Security & authentication (OWASP ASVS L1–L2, Top 10, CIS)
- Secrets in repo/history: `gitleaks detect --redact -v` or `trufflehog git file://. --only-verified`;
  fallback `rg -n -i "(api[_-]?key|secret|password|passwd|token|connectionstring|private key)\s*[:=]"`
  and `git log -p -S'BEGIN RSA PRIVATE KEY' --all | head`. Check `.gitignore` covers `.env*`.
- Dependency vulns: `dotnet list package --vulnerable --include-transitive`,
  `npm audit --omit=dev` / `pnpm audit` / `yarn npm audit`, `pip-audit`, `govulncheck ./...`,
  `cargo audit`, or `osv-scanner -r .`. Also outdated/EOL runtimes
  (`dotnet list package --outdated`, `npm outdated`, target framework end-of-support).
- AuthN: every non-public endpoint requires auth. Enumerate endpoints
  (`rg -n "Map(Get|Post|Put|Delete|Patch)|\[Http(Get|Post|Put|Delete)|\[Route|app\.(get|post|put|delete)\(|router\.|@app\.route|APIRouter"`)
  and compare with `[Authorize]`/`RequireAuthorization`/middleware/`AllowAnonymous`.
  List each endpoint: route | auth required? | role check? | evidence.
- AuthZ: roles/groups enforced server-side, not only hidden in UI. IDOR: record access
  scoped to the caller.
- Token/session: storage (localStorage vs cookies), expiry/refresh, logout clears all
  sessions, OAuth code+PKCE for SPAs, no implicit flow, no tokens in URLs/logs.
- CORS: `rg -n -i "cors|AllowAnyOrigin|Access-Control-Allow-Origin|\*\""` — no wildcard with
  credentials; origins limited to prod + dev hosts.
- Input/output: validation on server, parameterized SQL (`rg -n "FromSqlRaw|ExecuteSqlRaw|\+ *\"? *(SELECT|INSERT|UPDATE|DELETE)"`),
  output encoding, file upload limits, `MarkupString`/`innerHTML`/`dangerouslySetInnerHTML`.
- Transport/headers: HTTPS only, HSTS, CSP, X-Content-Type-Options, frame options (check
  hosting config such as `customHttp.yml`, middleware, CDN rules).
- Admin hygiene (Unverifiable list unless IaC shows it): MFA for staff/admins, least-privilege
  IAM, account provisioning/deprovisioning, password reset path.
- Pass: no secrets in repo or history (or rotated + documented); no high/critical vulns
  without mitigation; 100% of non-public endpoints auth'd and authz'd server-side.

### C4. Data, backups & disaster recovery (Fowler: catastrophe-preparedness)
- Checks: schema + migrations versioned (`rg --files | rg -i "migrations?/|\.sql$"`);
  seed/import scripts idempotent; backups configured (IaC: PITR, snapshots, versioning,
  `DeletionPolicy`/`removalPolicy: RETAIN`); restore procedure written AND tested; RPO/RTO
  stated; data retention + public-records/privacy obligations; PII inventory; encryption at
  rest/in transit.
- Pass: documented RPO/RTO, automated backups, a restore test with date, retention policy.

### C5. Reliability & fault tolerance (SRE PRR, WAF Reliability, Fowler)
- Checks: dependency list with failure behavior (timeouts, retries with backoff, circuit
  breakers: `rg -n -i "timeout|retry|polly|backoff|circuit"`); health endpoints
  (`rg -n -i "health|/healthz|/ready|MapHealthChecks"`); graceful degradation when an
  external service is down; user-facing error pages instead of stack traces
  (`rg -n -i "UseDeveloperExceptionPage|ErrorBoundary|UseExceptionHandler|blazor-error-ui"`);
  single points of failure from Phase 2; rollback path for deploys and DB migrations.
- Pass: every external call has a timeout + handled failure; health check wired to the host;
  rollback documented.

### C6. Observability & incident response (SRE: monitoring, emergency response)
- Checks: structured logging with levels, no PII/secrets in logs
  (`rg -n -i "console\.log|Debug\.WriteLine|print\(|ILogger|Serilog|log\.(info|error)"`);
  error tracking; metrics/dashboards; alerts on symptoms (errors, latency, auth failures,
  cost); who gets paged and how; incident/runbook docs; status/contact page for users.
- Pass: errors in prod reach a human within a defined time; at least one runbook per
  critical failure mode.

### C7. Performance & scalability
- Checks: stated targets (page load, API p95) vs measured (Lighthouse/k6/artillery results if
  present); bundle size (WASM/JS payload, lazy loading, compression); N+1 queries,
  pagination on list endpoints; cold-start behavior; capacity/limits of hosting tier and
  third-party quotas.
- Pass: targets written, measured once on a prod-like env, and met.

### C8. Testing & quality gates (Definition of Done)
- Inventory: count test projects/files and what they cover
  (`rg --files | rg -i "(test|spec)s?[./]"`; unit vs integration vs E2E e.g. Playwright/bUnit/
  Vitest/Jest/pytest). Run the suite with the repo's own command; record pass/fail counts.
- Coverage if available (`dotnet test --collect:"XPlat Code Coverage"`, `npm test -- --coverage`).
- Map critical user journeys (login, each primary task, logout, error paths) → test evidence.
- Lint/format/type-check/build with warnings-as-errors where configured.
- Pass: suite green locally and in CI; every critical journey has an automated test;
  no skipped/quarantined tests on critical paths (`rg -n "\.skip\(|\[Skip|Ignore\]|xit\(|test\.fixme"`).

### C9. CI/CD, deployment & configuration (12-Factor, DORA, SSDF)
- CI: read every `.github/workflows/*` (or equivalent). Required on PR: build, tests,
  lint, dependency/secret scan. `gh run list --limit 20` for recent health on default branch.
- Branch protection/required checks: `gh api repos/{owner}/{repo}/branches/{default}/protection`
  (record 404/403 as Unverifiable).
- Deploy: automated, repeatable, environment-specific config, prod deploy gated, rollback
  defined. Note DORA signals you can observe (deploy frequency, change failure rate from
  reverts/hotfixes in `git log`).
- Config: config in env not code; prod config contains no placeholders or dev values
  (C2 patterns against `appsettings.Production.json`, `.env.production`, `environment.prod.ts`);
  required env vars documented (`.env.example`); secrets come from a secret store.
- IaC lint if present: `cfn-lint`, `checkov -d .`, `tflint`/`tfsec`, `cdk synth`.
- Pass: green, required CI on default branch; one-command reproducible deploy; complete
  documented config with no placeholders.

### C10. Accessibility & usability for non-technical users (WCAG 2.2 AA, Nielsen)
- Automated: run `npx @axe-core/cli <url>` / `pa11y` / Lighthouse a11y against a local build, or
  Playwright + `@axe-core/playwright` if E2E exists. Static: images without alt, inputs without
  labels, color-only status, keyboard traps (`rg -n "<img(?![^>]*alt=)" -P`, `rg -n "<input" `).
- Heuristic walkthrough of each critical journey (Nielsen 10): visible status, plain language
  (no jargon/stack traces/raw IDs), user control (undo/cancel/confirm destructive actions),
  consistency, error prevention, recognition over recall, clear error messages with what to do
  next and who to call, help/onboarding text.
- Login friction: number of sign-ins per session, session timeouts losing work, password
  reset path a user can do alone.
- Pass: zero serious/critical axe violations on key pages; each journey completable without
  developer help; error messages actionable.

### C11. Documentation, support & handover (Fowler: documentation)
- Checks: README with setup/run/test/deploy; architecture overview; config reference;
  runbooks (deploy, rollback, restore, rotate secrets, add/remove user, reset password);
  end-user quick-start; support contact and escalation; ownership (CODEOWNERS / named
  maintainer); changelog/release notes.
- Pass: a new maintainer and a new end user can each succeed from docs alone.

### C12. Licensing, legal & compliance
- Checks: repo LICENSE matches intent (public vs private); third-party license inventory
  (`npx license-checker --summary`, `dotnet-project-licenses`, `pip-licenses`); commercial
  component licenses registered from config, not committed (`rg -n -i "license(key)?\s*[:=]|RegisterLicense"`);
  trial/evaluation banners; privacy notice, terms, accessibility statement, records-retention
  and any sector rules (e.g. public records, payment card data, CJIS/HIPAA if applicable).
- Pass: no unlicensed/trial components in prod; required notices published.

### C13. Cost & sustainability (WAF Cost/Sustainability)
- Checks: hosting tiers and expected monthly cost; budget alerts; idle/dev resources; AI/LLM
  or third-party per-call costs with limits/rate limiting
  (`rg -n -i "rate.?limit|throttl|max_tokens|quota"`).
- Pass: a written monthly estimate and a budget alert (usually Unverifiable → human).

## Phase 4 — Classify every gap

For each Fail/Partial/Unverifiable check create a gap row:

- **Severity**
  - **Blocker** — security exposure, data loss risk, a core journey broken/missing, or
    legal non-compliance. Cannot go live.
  - **Must** — required for safe operation in the first weeks (backups tested, alerting,
    runbooks, auth on all endpoints, a11y serious issues).
  - **Should** — materially improves reliability/usability; schedule soon after launch.
  - **Nice** — polish, optimization.
- **Owner type**: **Code** (agent/dev can fix in repo) · **Ops/Console** (cloud/IdP/DNS/
  hosting settings) · **Human decision** (policy, scope, budget, legal, user acceptance).
- **Effort**: S (< ½ day) · M (½–3 days) · L (> 3 days).
- Dedupe: one root cause = one row; list all evidence locations in it.
- Order: Blockers first, then by dependency (what unblocks the most), then effort.

## Phase 5 — Write the report

Write `docs/production-readiness-gap-report.md` with exactly these sections:

```markdown
# Production readiness gap report — <project> — <YYYY-MM-DD>
Commit: <sha> · Branch: <branch> · Auditor: Cursor agent (read-only)

## 1. Verdict
Not ready / Ready with conditions / Ready. One paragraph. Counts: Blockers N, Must N, Should N, Nice N, Unverifiable N.

## 2. Scope statement and feature inventory
(from Phase 1)

## 3. Architecture summary
(text diagram + single points of failure)

## 4. Scorecard
| # | Category | Pass | Partial | Fail | Unverif. | N/A | Rating (Red/Amber/Green) |
Rating: Red = any Blocker or ≥1 Fail on a Must; Amber = only Should/Nice gaps or Partials; Green = all Pass/N/A.

## 5. Prioritized gap list
| ID | Category | Item | Evidence | Severity | Owner | Effort | Suggested fix |
IDs like PRR-C3-01. Evidence = path:line / command + excerpt / link.

## 6. Unverifiable from repo (questions for a human)
| ID | Question | Why it matters | How to verify (console page / command) |

## 7. Commands run
Each command, exit code, and a 1–3 line result summary (or "not run: <reason>").

## 8. Definition of production ready (exit criteria)
(checklist below, each box ticked or unticked with gap IDs)

## 9. Suggested work plan
Group gaps into PR-sized batches in dependency order; Ops/Console and Human items listed separately.
```

### Definition of production ready (exit criteria — include verbatim, tick per evidence)

- [ ] Every in-scope feature implemented, reachable in the UI, and covered by a test; no placeholders in shipped paths.
- [ ] Zero open Blocker and Must gaps (or each Must has a signed-off, dated exception).
- [ ] No secrets in repo or history, or all exposed secrets rotated; secrets come from a managed store.
- [ ] No unmitigated high/critical dependency vulnerabilities; runtimes in support.
- [ ] All non-public endpoints authenticate and authorize server-side; CORS and security headers locked down.
- [ ] Users sign in once per session as designed; logout ends every session; account lifecycle and MFA documented.
- [ ] Automated backups enabled; a restore was tested on <date>; RPO/RTO written.
- [ ] Health checks, error tracking, and alerts route to a named person; runbooks exist for top failure modes.
- [ ] CI green on default branch with required checks; deploy and rollback are repeatable and documented.
- [ ] Production config complete, documented, and free of dev/placeholder values.
- [ ] Key pages have zero serious/critical WCAG 2.2 AA issues; each critical journey passes a walkthrough by a real end user.
- [ ] End-user quick-start, support contact, and maintainer docs exist.
- [ ] Licenses for all commercial components registered; required legal/privacy/accessibility notices published.
- [ ] Monthly cost estimate written and budget alert set.
- [ ] Named owner signs off (user acceptance) on <date>.

## Phase 6 — Report back in chat

Reply with: verdict, counts by severity, top 5 Blockers/Musts with IDs, the Unverifiable
questions, and the report path. Offer (don't do) next steps: open one issue per gap,
or start PRs for the Code-owned batches.

## Sources
- Google SRE book, "Production Readiness Review: Engagement Insight" — https://sre.google/sre-book/evolving-sre-engagement-model/
- Google SRE book, "Reliable Product Launches at Scale" (launch checklist) — https://sre.google/sre-book/reliable-product-launches/
- Google SRE workbook, "SRE Engagement Model" — https://sre.google/workbook/engagement-model/
- AWS Well-Architected Framework, "The pillars of the framework" — https://docs.aws.amazon.com/wellarchitected/latest/framework/the-pillars-of-the-framework.html
- AWS, "Operational Readiness Reviews (ORR)" — https://docs.aws.amazon.com/wellarchitected/latest/operational-readiness-reviews/wa-operational-readiness-reviews.html
- Microsoft Azure Well-Architected Framework — https://learn.microsoft.com/en-us/azure/well-architected/
- Azure WAF, "Design review checklist for Operational Excellence" — https://learn.microsoft.com/en-us/azure/well-architected/operational-excellence/checklist
- Susan Fowler, *Production-Ready Microservices* (O'Reilly) — https://www.oreilly.com/library/view/production-ready-microservices/9781491965962/ ; summary of the 8 standards — https://marabesi.com/reviews/production-ready-microservices.html
- OWASP Application Security Verification Standard — https://owasp.org/www-project-application-security-verification-standard/
- OWASP Top 10 — https://owasp.org/Top10/
- NIST SP 800-218 Secure Software Development Framework — https://csrc.nist.gov/pubs/sp/800/218/final
- NIST Cybersecurity Framework — https://www.nist.gov/cyberframework
- CIS Critical Security Controls — https://www.cisecurity.org/controls/cis-controls-list
- CISA Secure by Design — https://www.cisa.gov/securebydesign
- The Twelve-Factor App — https://12factor.net/
- DORA software delivery metrics — https://dora.dev/guides/dora-metrics-four-keys/
- The Scrum Guide (Definition of Done) — https://scrumguides.org/scrum-guide.html
- W3C WCAG 2.2 — https://www.w3.org/TR/WCAG22/
- Nielsen Norman Group, "10 Usability Heuristics" — https://www.nngroup.com/articles/ten-usability-heuristics/
- Gruntwork, "The Production Readiness Checklist for AWS" — https://gruntwork.io/devops-checklist/
- mtdvio, "going-to-production" checklist — https://github.com/mtdvio/going-to-production
