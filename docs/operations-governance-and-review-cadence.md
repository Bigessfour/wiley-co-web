# Operations Governance And Review Cadence

This document defines who owns Wiley Widget production support artifacts and how often core operational reviews must happen.

## Purpose

Post-release operations fail when everyone assumes someone else owns the docs, alarms, release evidence, and capacity reviews.

This document sets the minimum governance model for Wiley Widget.

## Operating Roles

| Role                | Primary responsibility                                                             |
| ------------------- | ---------------------------------------------------------------------------------- |
| Release owner       | Owns production deployment decisions, rollback readiness, and release records      |
| Service owner       | Owns API runtime health, App Runner or successor hosting, alarms, and logs         |
| Data owner          | Owns import correctness, snapshot plausibility, and enterprise baseline validation |
| Documentation owner | Keeps operational docs current after releases, incidents, and process changes      |

In a small team, one person may hold multiple roles. Ownership still needs to be explicit for each release window.

## Documentation Ownership

| Document                                                     | Owner                                         | Update trigger                                                  |
| ------------------------------------------------------------ | --------------------------------------------- | --------------------------------------------------------------- |
| `README.md`                                                  | Documentation owner                           | Repo-level runtime or deployment posture changes                |
| `docs/post-production-operations-handbook.md`                | Documentation owner with service owner review | Any day-two workflow change                                     |
| `docs/aws-server-side-closure-plan.md`                       | Service owner                                 | Infrastructure state change, hosting decision, alarm set change |
| `docs/release-record-template.md` and active release records | Release owner                                 | Every production release                                        |
| `docs/secrets-and-config-rotation-runbook.md`                | Service owner                                 | Secret-rotation or runtime-config process changes               |
| `docs/incident-response-matrix.md`                           | Service owner and documentation owner         | Incident-response lessons learned                               |
| `docs/quickbooks-desktop-import-guide.md`                    | Data owner                                    | Import workflow or operator guidance changes                    |
| `docs/aurora-postgresql-reset-runbook.md`                    | Service owner                                 | Recovery-path or migration-path changes                         |

## Review Cadence

### Daily

- check `/health`
- check active alarms
- scan recent application logs for Jarvis failures and import exceptions

### Weekly

- review release posture and open production risks
- review App Runner or successor service latency and 5xx signals
- review Aurora pressure and connection trends
- review Jarvis `AnswerSource`, `UsedFallback`, and `FailureCode` patterns

### Monthly

- review capacity assumptions against actual traffic and import activity
- confirm the latest production release has a completed release record
- confirm the current operational docs still match real operator behavior
- review documentation drift and assign updates in the same work window

### Quarterly

- review hosting-platform direction and the App Runner successor plan
- review backup and restore readiness
- review IAM access posture for operators and runtime roles
- review alert usefulness and retire noisy alarms that do not produce action

## Cost And Capacity Review Requirements

The monthly or quarterly review must answer these questions:

- Is API sizing still appropriate for normal interactive use and import activity?
- Are Aurora CPU and connection alarms still aligned with observed usage?
- Is Jarvis latency staying inside the expected council-facing interaction window?
- Are there recurring costs or idle resources that should be reduced before the next release window?

Record decisions in the current release record or the infrastructure planning document when they affect production posture.

## Minimum Governance Rules

- No production release closes without a named release owner.
- No incident closes without a named incident owner.
- No documentation gap stays open if it blocks repeatable operator behavior.
- No platform or runtime change is considered complete until the relevant document is updated in the same change window.

## Closeout Standard

Wiley Widget operations governance is in good shape when:

- every production activity has an owner
- every recurring review has a cadence
- the team can identify which document is authoritative for each operational task
- release and incident evidence are updated as part of the work, not later by memory
