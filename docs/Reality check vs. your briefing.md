# Action Plan

## Status

- The App Runner cutover is already complete.
- The remaining release blocker is Jarvis live-chat reliability on non-onboarding turns.
- The feature branch has been pushed cleanly and is now waiting on CI and GitHub-side branch protection follow-up.

## Immediate Actions

1. Prove Jarvis live chat returns a snapshot-grounded response with `UsedFallback=false` for Water Utility / FY2026 and Wiley Sanitation District / FY2026.
2. If the second-turn timeout repeats, isolate whether the failure is xAI latency, Semantic Kernel initialization, or VPC egress, then add a deterministic retry or timeout guard.
3. Curate `Import Data/` to one canonical workbook per dataset and run the production reference-data import.
4. Manually spot-check the imported snapshot for current rate, total costs, projected volume, and scenario outputs.

## Repo Hygiene

1. Watch the `feature/break-even-4-enterprises-dataviz` CI run and fix any follow-up issue it exposes.
2. Merge the branch into `main` and confirm the Amplify rebuild.
3. Apply the documented branch protections in GitHub.

## Operational Hardening

1. Review `WileyCo-AppRunner-ActiveInstancesLow` and tune autoscaling if needed.
2. Switch the next release to an immutable image tag and record the digest.
3. Fill out the release record with commit SHA, ECR digest, App Runner operation ID, health checks, smoke results, and rollback target.
4. Run the hosted Playwright smoke against production.

## Deferred

- Custom domain cutover.
- App Runner successor review.
- X-Ray to OpenTelemetry migration.

## Pause

- Stop here until the release blocker and the production validation steps above are complete.

## Status Update

- Immediate action 1: **Done in repo/tests**. The Jarvis live path now has a regression proving `UsedFallback=false` for both Water Utility and Wiley Sanitation District FY2026, with prompt-scoping assertions that include the enterprise context. Live production proof is still pending.
- Immediate action 2: **Done in repo/tests**. The Semantic Kernel path now has a bounded timeout guard and a timeout regression so stalled second turns fall back deterministically instead of hanging.
- Immediate action 3: **Done locally, manual-input scope confirmed**. The `Import Data/` folder now uses normalized filenames, the confusing CSV/archive duplicates were removed, and the import schema now includes support tables for lineage and validation; the customer-enrichment fields that are not present in the source exports will be entered manually in the workspace UI rather than imported from a backup source.
- Immediate action 4: **Blocked externally**. The production snapshot spot-check cannot be completed from the current local-only workspace state.
- Repo hygiene 1: **Done locally**. The feature branch was pushed cleanly and is waiting on CI results.
- Repo hygiene 2: **Not fully done**. The feature branch has not been merged into `main` from this local workspace state.
- Repo hygiene 3: **Still external**. The documented GitHub branch protections are still only recorded in the repo note.
- Operational hardening 1: **Blocked externally**. The App Runner alarm review and autoscaling tuning require live AWS access.
- Operational hardening 2: **Blocked externally**. The next immutable image tag and digest record require the release pipeline.
- Operational hardening 3: **Blocked externally**. The release record still needs live deployment metadata.
- Operational hardening 4: **Blocked externally**. The hosted Playwright smoke needs the production environment.
- Deferred items: still deferred.

## Next Blocker

- Repo hygiene 3 is now the next actionable blocker outside the local tree: apply the documented GitHub branch protections, then use CI results to decide whether any follow-up fix is needed.
