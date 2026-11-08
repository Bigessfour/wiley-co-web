provider "aws" {
  region  = var.aws_region
  profile = var.aws_profile

  default_tags {
    tags = local.common_tags
  }
}

data "aws_caller_identity" "current" {}
data "aws_region" "current" {}

check "expected_town_account" {
  assert {
    condition     = !var.require_town_account || data.aws_caller_identity.current.account_id == var.expected_account_id
    error_message = "Refusing to apply: caller account ${data.aws_caller_identity.current.account_id} != expected ${var.expected_account_id}. Set aws_profile=tow or require_town_account=false."
  }
}
