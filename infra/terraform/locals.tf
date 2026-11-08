locals {
  common_tags = {
    Project     = "tow-suite"
    Environment = var.environment
    ManagedBy   = "terraform"
    Combination = "widget-apartments-shared-contracts"
  }

  contracts_bucket_name = lower("${var.name_prefix}-shared-contracts-${data.aws_caller_identity.current.account_id}")

  ecr_repos = var.enable_ecr ? {
    widget-api = "${var.name_prefix}/wiley-widget-api"
    apartments = "${var.name_prefix}/wiley-apartments"
  } : {}
}
