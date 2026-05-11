# Post-Production Documentation Review

This review identifies the documentation Wiley Widget still needs after feature development and production cutover work.

The goal is not to rewrite every existing note. The goal is to separate:

- documents that already serve as authoritative runbooks
- documents that record release history or current state
- documentation that is still missing for steady-state production operations

## Current Documentation Coverage

The repository already has useful production-facing material.

| Existing document                         | Current value                                                           | Keep as source of truth for                                      |
| ----------------------------------------- | ----------------------------------------------------------------------- | ---------------------------------------------------------------- |
| `README.md`                               | High-level deployment, runtime, secrets, and environment context        | Operator orientation and repo-level production posture           |
| `docs/aws-server-side-closure-plan.md`    | Current AWS topology, release status, alarms, open infrastructure items | Deployment state, infrastructure decisions, and release evidence |
| `docs/aurora-postgresql-reset-runbook.md` | Approved destructive and non-destructive Aurora schema recovery path    | Database recovery and migration re-apply workflow                |
| `docs/quickbooks-desktop-import-guide.md` | Clerk-facing monthly import path and file expectations                  | Recurring import operations                                      |
| `docs/playwright-ui-test-strategy.md`     | Hosted browser validation expectations                                  | UI smoke and regression validation strategy                      |
| `docs/wileyco-ui-rebuild-plan.md`         | Product and implementation plan with production-readiness context       | Historical plan and remaining implementation context             |

## Documentation Gaps

The current set is strong on project status and narrow runbooks, but it is weak on day-two operational ownership.

### P0 Gaps

- No single post-production operations handbook ties deployment, rollback, monitoring, incidents, data operations, and release evidence together.
- No single operator-facing document explains how to handle Jarvis fallback, App Runner rollout checks, and post-import validation in one place.
- No single documentation map tells support staff which existing document is authoritative for each production activity.

### P1 Gaps

- No formal release record template exists for commit SHA, image digest, App Runner operation ID, smoke checks, alarm state, and rollback reference.
- No dedicated secrets and configuration rotation runbook exists for App Runner runtime secrets, xAI endpoint changes, and validation after rotation.
- No incident-response matrix exists for severity, first response, escalation, and evidence capture.
- No explicit backup and restore cadence is documented beyond the Aurora reset procedure itself.

### P2 Gaps

- These gaps are now closed by the added successor review and operations-governance documents.

## Documentation Delivered In This Change

This review now includes the central handbook and the closeout support set:

- `docs/post-production-operations-handbook.md`
- `docs/release-record-template.md`
- `docs/secrets-and-config-rotation-runbook.md`
- `docs/incident-response-matrix.md`
- `docs/app-runner-successor-review.md`
- `docs/operations-governance-and-review-cadence.md`

The handbook is now the primary source for day-two production operations and links back to the narrower runbooks that should remain authoritative in their own domains.

## Recommended Documentation Set

The following structure is the recommended post-production baseline for this project.

| Priority | Document                                           | Status   | Purpose                                             |
| -------- | -------------------------------------------------- | -------- | --------------------------------------------------- |
| P0       | `docs/post-production-operations-handbook.md`      | Added    | Central day-two operating guide                     |
| P0       | `docs/aurora-postgresql-reset-runbook.md`          | Existing | Database recovery and schema alignment              |
| P0       | `docs/quickbooks-desktop-import-guide.md`          | Existing | Clerk import operations                             |
| P1       | `docs/release-record-template.md`                  | Added    | Standard release evidence and rollback record       |
| P1       | `docs/secrets-and-config-rotation-runbook.md`      | Added    | Runtime secret rotation and config-change procedure |
| P1       | `docs/incident-response-matrix.md`                 | Added    | Severity, escalation, and response ownership        |
| P2       | `docs/app-runner-successor-review.md`              | Added    | Hosting replacement decision record                 |
| P2       | `docs/operations-governance-and-review-cadence.md` | Added    | Cost/capacity cadence and documentation ownership   |

## Recommended Documentation Rules

To keep the documentation set usable after production handoff:

- Keep status/history documents separate from standing runbooks.
- Keep one primary handbook for day-two operations and link outward to specialist runbooks.
- Record every production release with immutable evidence: commit SHA, image digest, deployment operation ID, smoke checks, and rollback target.
- Update the handbook when an operational workflow changes, not only when code changes.
- Update the narrow runbook closest to the operational task when a procedure changes.
- After each incident, capture the final diagnosis and any changed operator steps in the relevant runbook within the same change window.

## Review Conclusion

Wiley Widget does not need a large documentation rewrite.

It needs a cleaner operating model:

- one handbook for daily production support
- existing specialist runbooks retained as authoritative details
- a small, explicit set of release, incident, secret-rotation, successor-planning, and governance documents to close the remaining day-two gaps

That is the minimum documentation set that turns the current project notes into a maintainable post-production support system.
