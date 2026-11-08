# Terraform — Widget + Apartments shared-contracts (low-cost)

Deployable **cost-controlled** footing for the Wiley Widget ↔ Wiley Apartments combination described in [`docs/aws-low-cost-architecture.md`](../../docs/aws-low-cost-architecture.md).

This stack **does not** create Aurora, NAT gateways, App Runner, or ECS. Those stay as existing Widget resources (or a later optional module). Default resources are cheap: Budgets, one private S3 drop bucket, ECR repos, optional SSM pointers.

## What it creates (flags)

| Flag | Default | Resource |
| ---- | ------- | -------- |
| `enable_budgets` | `true` | Monthly budgets at `$80` and `$100` with email alerts |
| `enable_contracts_bucket` | `true` | Private S3 bucket + lifecycle for `shared-contracts/` drops |
| `enable_ecr` | `true` | ECR repos for Widget API + Apartments (`force_delete` for easy destroy) |
| `enable_publisher_user` | `false` | IAM user + S3 put policy for CI/scripts |
| `widget_reference_data_url` | empty | Optional SSM parameter for Widget API base URL |

Tags every resource with `Project=tow-suite` for Cost Explorer.

## Prerequisites

```bash
aws sts get-caller-identity --profile tow
# Account should be 818904800844 (Town of Wiley)
```

Copy example vars:

```bash
cd infra/terraform
cp terraform.tfvars.example terraform.tfvars
# set budget_alert_emails
```

## Plan / apply

```bash
./scripts/plan-apply.sh
./scripts/plan-apply.sh --apply
```

## Destroy (cost kill-switch)

If spend climbs or the experiment is done:

```bash
./scripts/destroy.sh
# confirm with: destroy-tow-suite
```

Or:

```bash
terraform destroy
```

ECR uses `force_delete = true` so destroy does not stick on leftover images. S3 objects under versioning may need emptying if destroy fails — lifecycle keeps the bucket small; for a stuck destroy run `aws s3 rm s3://BUCKET --recursive` then re-run destroy.

**Out of scope for destroy:** existing Wiley Widget App Runner service and Aurora cluster (not managed by this state). Tear those down only with their own runbooks.

## Data path (Apartments → Widget)

1. Apartments export: `GET /api/exports/shared-contracts/operational-events?period=YYYY-MM`
2. Either:
   - `POST` directly to Widget `/api/reference-data/events` via `Wiley_Apartments/scripts/post-operating-costs-to-widget.sh`, or
   - `aws s3 cp` to `s3://$contracts_bucket/shared-contracts/events/apartments/YYYY-MM.json` then a worker/cron POSTs the object body to Widget.

## Intentionally omitted (budget killers)

- NAT Gateway
- New Aurora / RDS
- Always-on Fargate
- New App Runner (closed to new customers after 2026-04-30; keep existing Widget service)
- Multi-AZ “defaults”

See architecture doc for the full exclusion list.
