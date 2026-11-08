check "budget_emails_when_enabled" {
  assert {
    condition     = !var.enable_budgets || length(var.budget_alert_emails) > 0
    error_message = "enable_budgets=true requires budget_alert_emails (at least one address)."
  }
}

resource "aws_budgets_budget" "suite_soft" {
  count = var.enable_budgets ? 1 : 0

  name         = "${var.name_prefix}-monthly-${var.budget_limit_usd}"
  budget_type  = "COST"
  limit_amount = var.budget_limit_usd
  limit_unit   = "USD"
  time_unit    = "MONTHLY"

  cost_types {
    include_credit             = true
    include_discount           = true
    include_other_subscription = true
    include_recurring          = true
    include_refund             = true
    include_subscription       = true
    include_support            = true
    include_tax                = true
    include_upfront            = true
    use_blended                = false
  }

  notification {
    comparison_operator        = "GREATER_THAN"
    threshold                  = 80
    threshold_type             = "PERCENTAGE"
    notification_type          = "ACTUAL"
    subscriber_email_addresses = var.budget_alert_emails
  }

  notification {
    comparison_operator        = "GREATER_THAN"
    threshold                  = 100
    threshold_type             = "PERCENTAGE"
    notification_type          = "FORECASTED"
    subscriber_email_addresses = var.budget_alert_emails
  }
}

resource "aws_budgets_budget" "suite_hard" {
  count = var.enable_budgets ? 1 : 0

  name         = "${var.name_prefix}-monthly-hard-${var.budget_hard_limit_usd}"
  budget_type  = "COST"
  limit_amount = var.budget_hard_limit_usd
  limit_unit   = "USD"
  time_unit    = "MONTHLY"

  notification {
    comparison_operator        = "GREATER_THAN"
    threshold                  = 100
    threshold_type             = "PERCENTAGE"
    notification_type          = "ACTUAL"
    subscriber_email_addresses = var.budget_alert_emails
  }
}
