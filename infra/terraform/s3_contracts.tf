resource "aws_s3_bucket" "contracts" {
  count  = var.enable_contracts_bucket ? 1 : 0
  bucket = local.contracts_bucket_name

  tags = {
    App  = "shared-contracts"
    Role = "drop-ingest"
  }
}

resource "aws_s3_bucket_public_access_block" "contracts" {
  count  = var.enable_contracts_bucket ? 1 : 0
  bucket = aws_s3_bucket.contracts[0].id

  block_public_acls       = true
  block_public_policy     = true
  ignore_public_acls      = true
  restrict_public_buckets = true
}

resource "aws_s3_bucket_versioning" "contracts" {
  count  = var.enable_contracts_bucket ? 1 : 0
  bucket = aws_s3_bucket.contracts[0].id

  versioning_configuration {
    status = "Enabled"
  }
}

resource "aws_s3_bucket_server_side_encryption_configuration" "contracts" {
  count  = var.enable_contracts_bucket ? 1 : 0
  bucket = aws_s3_bucket.contracts[0].id

  rule {
    apply_server_side_encryption_by_default {
      sse_algorithm = "AES256"
    }
  }
}

resource "aws_s3_bucket_lifecycle_configuration" "contracts" {
  count  = var.enable_contracts_bucket ? 1 : 0
  bucket = aws_s3_bucket.contracts[0].id

  rule {
    id     = "expire-shared-contracts"
    status = "Enabled"

    filter {
      prefix = "shared-contracts/"
    }

    expiration {
      days = var.contracts_lifecycle_days
    }

    noncurrent_version_expiration {
      noncurrent_days = 7
    }
  }
}

# Prefix layout documented for publishers (Apartments → events/, Widget consumers).
resource "aws_s3_object" "contracts_readme" {
  count        = var.enable_contracts_bucket ? 1 : 0
  bucket       = aws_s3_bucket.contracts[0].id
  key          = "shared-contracts/README.txt"
  content      = <<-EOT
    Town of Wiley shared-contracts drop bucket
    -----------------------------------------
    Put OperationalEvent[] JSON at:
      shared-contracts/events/apartments/YYYY-MM.json
    Put ActualsSummary JSON at:
      shared-contracts/actuals/<enterpriseId>/YYYY-MM.json

    Then POST the body to Wiley Widget:
      POST {widget_api}/api/reference-data/events
      POST {widget_api}/api/reference-data/actuals

    See wiley-co-web/docs/shared-contracts-reference-data-api.md
    and Wiley_Apartments/docs/shared-contracts-export.md
  EOT
  content_type = "text/plain"
}
