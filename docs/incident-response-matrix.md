# Incident Response Matrix

Use this matrix when production behavior is degraded, incorrect, unavailable, or risky.

This is not a substitute for technical diagnosis. It is the first-response framework that keeps the team organized and reduces panic-driven mistakes.

## Core Rules

- Stabilize first, explain second.
- Preserve evidence before making irreversible changes.
- Use the rollback path when a fresh release caused the incident and the rollback target is known-good.
- Record what changed, when it changed, and what operators observed.
- Update the relevant runbook after the incident if the response process changed.

## Severity Levels

| Severity | Definition                                                         | Examples                                                                                          | Target response   |
| -------- | ------------------------------------------------------------------ | ------------------------------------------------------------------------------------------------- | ----------------- |
| Sev 1    | Production outage or materially unsafe behavior                    | `/health` failing, App Runner unusable, Aurora unavailable, council-facing workflows blocked      | Immediate         |
| Sev 2    | Major degradation with workarounds or partial service              | Jarvis timing out on normal turns, snapshot or knowledge endpoint unstable, import commit blocked | Same hour         |
| Sev 3    | Limited degradation or diagnostic alert without broad service loss | fallback rate spike, alarm without customer-visible failure, single operator workflow broken      | Same business day |
| Sev 4    | Low-risk defect, documentation gap, or tuning task                 | noisy alarm action, threshold tuning, stale dashboard link, unclear operator wording              | Planned follow-up |

## First Response By Incident Type

### 1. App Runner Health Or Deployment Failure

Primary signals:

- App Runner service not `RUNNING`
- unhealthy instances
- `/health` failing
- recent deployment logs showing startup or config failure

First actions:

1. Check App Runner service status and latest operation.
2. Check App Runner service logs and deployment logs.
3. Check public `/health`.
4. Check active CloudWatch alarms.
5. If the issue started with a release, evaluate rollback immediately.

Escalate to Sev 1 when:

- the public API is unavailable
- health checks are failing across the service
- rollback cannot restore service quickly

### 2. Jarvis Reliability Or Fallback Incident

Primary signals:

- non-onboarding chat turns returning deterministic or legacy fallback
- repeated timeouts
- elevated `ElapsedMs`
- xAI or Jarvis alarms firing

First actions:

1. Query recent `Workspace AI request completed` logs.
2. Group results by `AnswerSource`, `UsedFallback`, and `FailureCode`.
3. Check whether the issue is isolated to one enterprise or fiscal year.
4. Check proxy endpoint, retry posture, and runtime secret state.
5. If the issue began after release, compare with the prior known-good revision and prepare rollback.

Escalate to Sev 1 when:

- council-facing or operator-critical chat workflows are unusable and there is no workaround

Escalate to Sev 2 when:

- Jarvis is available but materially degraded for normal use

### 3. Snapshot, Knowledge, Or Data Integrity Incident

Primary signals:

- `/api/workspace/snapshot` returns incomplete or implausible data
- `/api/workspace/knowledge` disagrees with snapshot reality
- enterprise counts or rates change unexpectedly after release or import

First actions:

1. Confirm whether a data import, schema change, or deployment happened recently.
2. Compare the affected enterprise and fiscal year with the expected baseline.
3. Validate that Aurora is reachable and not under pressure.
4. Review application logs for import, repository, or mapping errors.
5. Decide whether the fastest path is rollback, re-import, or targeted operator correction.

Escalate to Sev 1 when:

- the live data is materially wrong for public or council-facing use and no quick mitigation exists

### 4. Import Pipeline Incident

Primary signals:

- preview empty or malformed
- commit blocked incorrectly
- duplicate protection confusion
- enterprise/fiscal year mis-assignment

First actions:

1. Confirm the file family and file type.
2. Confirm operator selections before commit.
3. Check duplicate file behavior and prior import history.
4. Check post-import snapshot and knowledge outputs.
5. If data was committed incorrectly, stop repeat imports and move to controlled cleanup.

## Response Roles

| Role             | Responsibility                                                                      |
| ---------------- | ----------------------------------------------------------------------------------- |
| Release operator | Owns the current deployment status, rollback decision support, and release evidence |
| Service operator | Checks App Runner, CloudWatch alarms, logs, and health status                       |
| Data operator    | Validates snapshot, knowledge, import, and enterprise baseline behavior             |
| Incident owner   | Keeps a single timeline, records decisions, and closes the incident notes           |

In a small team, one person may hold multiple roles. The key rule is that one named person owns the incident timeline.

## Evidence Checklist

Capture these before closing an incident:

- incident start time
- affected user or workflow scope
- recent release or config changes
- alarm state
- relevant App Runner operation IDs
- relevant image digest or commit SHA
- relevant log excerpts or query output
- rollback or mitigation action taken
- final validation after mitigation

## Containment Options

Use the least risky option that restores service quickly.

In preferred order when appropriate:

1. Roll back to last known-good release.
2. Revert a configuration-only change.
3. Restart or redeploy only when the current artifact is still believed to be correct.
4. Perform controlled data correction or re-import.
5. Use database recovery runbook only when simpler containment is not enough.

## Communication Guidance

Initial incident note should answer:

- what is broken
- who is affected
- when it started
- whether a recent release or config change is suspected
- who owns the incident
- when the next update will be posted

Do not send speculative technical root causes as facts.

## Exit Criteria

An incident is not fully closed until:

- service is stable
- validation checks passed
- rollback or mitigation is documented
- follow-up work items are assigned
- the relevant runbook or handbook is updated if the incident changed the operator process
