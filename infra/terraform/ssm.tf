resource "aws_ssm_parameter" "widget_reference_data_url" {
  count = var.widget_reference_data_url != "" ? 1 : 0

  name  = "/${var.name_prefix}/widget/reference-data-base-url"
  type  = "String"
  value = trimspace(var.widget_reference_data_url)

  tags = {
    App = "wiley-widget"
  }
}

resource "aws_ssm_parameter" "contracts_bucket" {
  count = var.enable_contracts_bucket ? 1 : 0

  name  = "/${var.name_prefix}/shared-contracts/bucket"
  type  = "String"
  value = aws_s3_bucket.contracts[0].bucket

  tags = {
    App = "shared-contracts"
  }
}
