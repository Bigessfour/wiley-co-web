# Secrets And Configuration Rotation Runbook

This runbook is the approved process for changing production runtime secrets or App Runner configuration values.

Use this for:

- xAI API key rotation
- database connection secret rotation
- Syncfusion runtime secret rotation for the API host
- App Runner environment variable changes
- xAI endpoint, retry, timeout, or proxy configuration changes

## Why This Matters

Production secrets and configuration are not just values. They are part of the running release.

For this system:

- App Runner runtime values are part of service configuration
- secret references must stay aligned with IAM permissions and service config
- runtime value changes need a deployment or service update to become effective in the running service
- post-change validation must prove that the new values work before the change window closes

## Core Rules

- Never hard-code production secrets in source files.
- Prefer secret references over plain text for sensitive values.
- Rotate one operational concern at a time when possible.
- Do not combine secret rotation, major code release, and domain or infrastructure cutover in one change window unless rollback is explicitly planned.
- Always capture the pre-change and post-change state in a release record.

## Supported Secret And Config Categories

### Runtime secrets

- `XAI_API_KEY`
- `DATABASE_URL`
- `ConnectionStrings__DefaultConnection`
- `SYNCFUSION_LICENSE_KEY`

### Runtime config values

- `XAI__ChatEndpoint`
- `XAI__Endpoint`
- `XAI__AllowDirectRetry`
- `XAI__TimeoutSeconds`
- `ASPNETCORE_ENVIRONMENT`
- `WorkspaceClientOrigins`
- other App Runner runtime environment variables used by the API host

## Preparation Checklist

Before changing anything:

1. Confirm the reason for the change.
2. Identify the exact secret or config keys affected.
3. Confirm the current service ARN and environment.
4. Confirm the rollback value or previous secret reference.
5. Confirm operator access to App Runner, Secrets Manager, and CloudWatch.
6. Confirm a validation plan for `/health`, snapshot, knowledge, and Jarvis.

## Standard Rotation Workflow

### 1. Record the current state

Capture in the release record:

- current key or config names
- current referenced secret ARN or name
- current App Runner runtime config values that will change
- current alarm state
- current known-good service behavior

### 2. Update the secret or config source

Examples:

- rotate the secret value in Secrets Manager
- update the App Runner environment variable reference
- update the App Runner runtime environment variable value

Important operational rule:

- if the application code still expects the old key name or shape, update code first in a normal release before rotating the runtime source

### 3. Redeploy or update the service

After rotation, trigger the required App Runner deployment or service update so the running service picks up the new value.

If the service uses image-based automatic deployment and only the secret value changed, the operator still needs a service update or equivalent deployment action so the running instances refresh against the intended configuration state.

### 4. Validate immediately

Minimum validation set:

- App Runner service reaches healthy state
- `/health` returns healthy
- `/api/workspace/snapshot` returns expected data
- `/api/workspace/knowledge` returns expected data
- if the change affects Jarvis, one non-onboarding `/api/ai/chat` validation turn succeeds

### 5. Close the change

Record:

- exact change completed
- deployment or operation ID
- validation result
- rollback target if the change needs to be reversed later

## Special Cases

### xAI key rotation

Validate:

- no auth-related xAI alarms
- Jarvis normal turn completes
- `Workspace AI request completed` logs do not show auth failure codes

### xAI endpoint or proxy routing change

Validate:

- endpoint normalization still lands on the intended `/v1` or `/v1/responses` path
- normal non-onboarding Jarvis turn does not degrade to fallback unexpectedly
- proxy-specific alarms and latency remain within expected range

### Database secret rotation

Validate:

- `/health` remains healthy
- snapshot and knowledge calls succeed
- no Aurora connection spike or connection failure alarms appear

### Workspace client origin or public config change

Validate:

- expected client origin still reaches the API successfully
- no CORS regression appears in hosted browser smoke tests

## Rollback Guidance

Rollback the secret or config change when:

- health checks fail after rotation
- the API cannot reach Aurora after database secret rotation
- Jarvis fails normal validation after xAI rotation
- alarms indicate new auth, endpoint, or transport failures tied to the change

Rollback path:

1. Restore the previous known-good secret value or runtime config value.
2. Trigger the required App Runner deployment or service update.
3. Re-run the same validation checks.
4. Record the rollback in the release record.

## Evidence To Keep

For every rotation, retain:

- date and operator
- secret or config key changed
- old and new reference identifiers, without exposing secret values
- deployment or update operation ID
- validation outcome
- rollback target

## Minimum Rule

Secret or config rotation is not complete until:

- the running service has reloaded the intended value
- the required validation checks pass
- the release record captures what changed and how to reverse it
