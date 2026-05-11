# Release Record Template

Use this template for every production release.

Do not skip this because the change looks small. Professional teams keep a release record even for minor production updates because rollback, audit, and incident review depend on it.

## Release Summary

- Release name:
- Date:
- Operator:
- Change window:
- Environment: Production
- Release type: API / Web client / Config only / Secret rotation / Data operation / Combined

## Change Scope

- Business reason:
- User-facing impact:
- Systems affected:
- Risk level: Low / Medium / High
- Related issue or work item:

## Source And Artifact

- Git branch:
- Git commit SHA:
- ECR repository:
- Image tag:
- Image digest:
- Amplify job ID, if applicable:
- App Runner service ARN:
- App Runner operation ID:

## Configuration And Secrets

- Runtime environment variables changed:
- Secret references changed:
- Secret values rotated: Yes / No
- Config-only release: Yes / No
- Post-change App Runner deployment required: Yes / No

## Pre-Release Checks

- Relevant tests passed:
- Build completed:
- Container image build completed:
- Alarm state checked before release:
- Rollback target identified:
- Operator approval or sign-off captured:

## Deployment Steps Performed

List the exact steps that were performed in order.

1.
2.
3.

## Post-Deploy Validation

Record actual results, not planned checks.

- App Runner service status:
- `/health` result:
- `/api/workspace/snapshot` result:
- `/api/workspace/knowledge` result:
- `/api/ai/chat` result:
- Hosted browser smoke result, if applicable:
- Alarm state after release:

## Jarvis Validation

Use this section whenever the release could affect chat, transport, model configuration, proxy routing, timeout behavior, or live knowledge grounding.

- Enterprise tested:
- Fiscal year tested:
- Non-onboarding turn completed: Yes / No
- `UsedFallback=false` observed: Yes / No
- `AnswerSource` observed:
- `FailureCode` observed:
- `ElapsedMs` observed:
- Notes on response quality and data grounding:

## Data Validation

Use this section whenever the release touches imports, snapshot logic, knowledge logic, Aurora schema, or enterprise baselines.

- Import or data operation performed: Yes / No
- Enterprise counts validated:
- Snapshot values spot-checked:
- Knowledge response spot-checked:
- Utility customer or ledger row checks:

## Outcome

- Release result: Succeeded / Succeeded with follow-up / Rolled back / Failed before completion
- Follow-up items:
- Owner for follow-up:

## Rollback Section

Complete this section even if rollback was not needed.

- Rollback target image tag or digest:
- Rollback target commit SHA:
- Rollback trigger conditions:
- Rollback steps prepared:

If rollback happened:

- Rollback started at:
- Rollback operation ID:
- Rollback validation result:
- Customer or operator impact:

## Lessons Learned

- What went well:
- What should change before the next release:
- Documentation updated as part of this release: Yes / No

## Minimum Rule

A production release is not fully documented until this record includes:

- immutable source reference
- deployment evidence
- smoke-check evidence
- rollback target
- named follow-up owner for anything left open
