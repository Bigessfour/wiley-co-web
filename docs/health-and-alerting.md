# Health checks and who is on call

## Checks that are already public

| URL | Auth | Use |
| --- | --- | --- |
| `/health` | Anonymous | App Runner process and dependencies |
| `/api/ai/health` | Anonymous | Jarvis probe |
| `/api/jarvis/health` | Anonymous | Same probe |

A clerk-facing page at `/support` names who to call: Town Hall (719) 829-4974, clerk@townofwiley.gov, Wiley Town Hall, 304 Main Street.

## Alarms already named for this app

The operations handbook lists:

- `WileyCo-Aurora-CPUHigh`
- `WileyCo-Aurora-ConnectionsHigh`

Confirm in CloudWatch (profile `steve`, region `us-east-2`) that each alarm has an action. The action should notify someone who will call Town Hall if clerks are blocked. This repo does not create or change those alarms.

## Alarm still to attach

Not applied from this change. The `steve` session was expired, and the live App Runner service name was not re-read.

When credentials are current:

1. `aws apprunner list-services --profile steve --region us-east-2` and note the service name.
2. In CloudWatch, alarm on that service's 5xx count or unhealthy instance count (confirm the metric id in the App Runner console before saving).
3. Use the same notification target as `WileyCo-Aurora-CPUHigh`.
4. Put Town Hall, (719) 829-4974, on the notification path so a failed `/health` reaches a person.

Do not alarm on request latency as a substitute for `/health`. Latency is not a down check.

Until that alarm exists, clerks use `/support` and the operator curls `/health`.
