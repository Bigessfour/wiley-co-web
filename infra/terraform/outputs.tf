output "account_id" {
  description = "Caller AWS account id."
  value       = data.aws_caller_identity.current.account_id
}

output "region" {
  description = "Deploy region."
  value       = data.aws_region.current.name
}

output "nas_apartments_url" {
  description = "Tailscale URL for ClerkSuite on the NAS."
  value       = trimspace(var.nas_apartments_url) != "" ? trimspace(var.nas_apartments_url) : null
}

output "nas_tikr_url" {
  description = "Tailscale URL for TIKR on the NAS."
  value       = trimspace(var.nas_tikr_url) != "" ? trimspace(var.nas_tikr_url) : null
}

output "contracts_bucket_name" {
  description = "S3 bucket for shared-contracts drops (null if disabled)."
  value       = try(aws_s3_bucket.contracts[0].bucket, null)
}

output "contracts_bucket_arn" {
  description = "S3 bucket ARN (null if disabled)."
  value       = try(aws_s3_bucket.contracts[0].arn, null)
}

output "ecr_repository_urls" {
  description = "Map of ECR repository URLs for Widget API and Apartments."
  value       = { for k, r in aws_ecr_repository.apps : k => r.repository_url }
}

output "budget_names" {
  description = "AWS Budget names created for cost kill-switch."
  value = compact([
    try(aws_budgets_budget.suite_soft[0].name, ""),
    try(aws_budgets_budget.suite_hard[0].name, "")
  ])
}

output "destroy_hint" {
  description = "How to tear everything in this stack down."
  value       = "cd infra/terraform && ./scripts/destroy.sh   # or: terraform destroy -auto-approve"
}

output "ingest_hint" {
  description = "Apartments → Widget ingest reminder."
  value       = "PERIOD=YYYY-MM ./scripts/post-operating-costs-to-widget.sh (Apartments repo) or S3 drop under shared-contracts/events/apartments/"
}
