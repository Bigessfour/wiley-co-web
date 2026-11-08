#!/usr/bin/env bash
# Tear down ALL resources in this Terraform stack (budgets, S3, ECR, IAM, SSM).
# Does NOT destroy existing Wiley Widget App Runner / Aurora outside this state.
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"

if [[ ! -f terraform.tfvars && ! -f terraform.tfvars.json ]]; then
  echo "warn: no terraform.tfvars found; using defaults / env TF_VAR_*" >&2
fi

echo "This will DESTROY Terraform-managed tow-suite resources in the current state."
echo "Existing Wiley Widget App Runner / Aurora (not in this state) are untouched."
echo
read -r -p "Type destroy-tow-suite to continue: " CONFIRM
if [[ "$CONFIRM" != "destroy-tow-suite" ]]; then
  echo "Aborted."
  exit 1
fi

terraform init -input=false
terraform destroy -input=false "$@"

echo
echo "Destroy finished. Verify Cost Explorer / Budgets console if alarms linger."
