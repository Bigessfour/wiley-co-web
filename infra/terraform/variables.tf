variable "aws_region" {
  type        = string
  description = "AWS region for suite resources (match Wiley Widget)."
  default     = "us-east-2"
}

variable "aws_profile" {
  type        = string
  description = "AWS CLI profile. Prefer tow (Town of Wiley 818…) for municipal workloads."
  default     = "tow"
}

variable "expected_account_id" {
  type        = string
  description = "Town of Wiley official account id (starts with 818)."
  default     = "818904800844"
}

variable "require_town_account" {
  type        = bool
  description = "Fail plan/apply when caller account is not expected_account_id."
  default     = true
}

variable "name_prefix" {
  type        = string
  description = "Prefix for resource names (hyphens only; no em dashes)."
  default     = "tow-suite"
}

variable "environment" {
  type        = string
  description = "Environment tag (dev, staging, prod)."
  default     = "prod"
}

variable "budget_alert_emails" {
  type        = list(string)
  description = "Emails for AWS Budget notifications. Required when enable_budgets is true."
  default     = []
}

variable "budget_limit_usd" {
  type        = string
  description = "Monthly cost budget limit (USD) for the suite kill-switch alarm."
  default     = "80"
}

variable "budget_hard_limit_usd" {
  type        = string
  description = "Second monthly budget for urgent notification (USD)."
  default     = "100"
}

variable "enable_budgets" {
  type        = bool
  description = "Create AWS Budgets + email alerts. Cheap; enable for cost control."
  default     = true
}

variable "enable_contracts_bucket" {
  type        = bool
  description = "S3 drop bucket for shared-contracts JSON (Apartments export → Widget ingest)."
  default     = true
}

variable "enable_ecr" {
  type        = bool
  description = "ECR repos for Wiley Widget API and Wiley Apartments images."
  default     = true
}

variable "enable_publisher_user" {
  type        = bool
  description = "IAM user for CI/scripts that PUT contracts to S3 (access keys managed outside TF)."
  default     = false
}

variable "widget_reference_data_url" {
  type        = string
  description = "HTTPS base for Widget API (used in SSM parameter for publishers). Example: https://xxxxx.us-east-2.awsapprunner.com"
  default     = ""
}

variable "contracts_lifecycle_days" {
  type        = number
  description = "Expire objects under shared-contracts/ prefixes after N days (cost control)."
  default     = 30
}

variable "ecr_image_count_limit" {
  type        = number
  description = "Keep only the newest N images per ECR repo."
  default     = 10
}
