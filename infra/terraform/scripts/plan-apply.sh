#!/usr/bin/env bash
# Plan (and optionally apply) the Widget + Apartments shared-contracts stack.
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"

DO_APPLY=0
if [[ "${1:-}" == "--apply" ]]; then
  DO_APPLY=1
  shift
fi

echo "Caller identity:"
aws sts get-caller-identity --profile "${AWS_PROFILE:-tow}" || true
echo

terraform init -input=false
terraform fmt -recursive
terraform validate
terraform plan -input=false -out=tfplan "$@"

if [[ "$DO_APPLY" -eq 1 ]]; then
  terraform apply -input=false tfplan
  rm -f tfplan
  terraform output
else
  echo
  echo "Plan saved to tfplan. Review, then:"
  echo "  terraform apply tfplan"
  echo "  # or: ./scripts/plan-apply.sh --apply"
fi
