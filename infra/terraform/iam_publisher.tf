# Optional IAM user for scripts/CI that upload shared-contracts drops to S3.
# Keys are NOT created here — create/rotate via console or aws iam create-access-key after apply.

resource "aws_iam_user" "contracts_publisher" {
  count = var.enable_publisher_user && var.enable_contracts_bucket ? 1 : 0
  name  = "${var.name_prefix}-contracts-publisher"

  tags = {
    App  = "shared-contracts"
    Role = "publisher"
  }
}

resource "aws_iam_user_policy" "contracts_publisher" {
  count = var.enable_publisher_user && var.enable_contracts_bucket ? 1 : 0
  name  = "${var.name_prefix}-contracts-publisher-s3"
  user  = aws_iam_user.contracts_publisher[0].name

  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Sid      = "ListContractsBucket"
        Effect   = "Allow"
        Action   = ["s3:ListBucket"]
        Resource = [aws_s3_bucket.contracts[0].arn]
        Condition = {
          StringLike = {
            "s3:prefix" = ["shared-contracts/*"]
          }
        }
      },
      {
        Sid    = "PutGetContractsObjects"
        Effect = "Allow"
        Action = [
          "s3:PutObject",
          "s3:GetObject",
          "s3:DeleteObject"
        ]
        Resource = ["${aws_s3_bucket.contracts[0].arn}/shared-contracts/*"]
      }
    ]
  })
}
