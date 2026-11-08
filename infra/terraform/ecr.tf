resource "aws_ecr_repository" "apps" {
  for_each = local.ecr_repos

  name                 = each.value
  image_tag_mutability = "MUTABLE"
  force_delete         = true # allow terraform destroy without manual image cleanup

  image_scanning_configuration {
    scan_on_push = true
  }

  tags = {
    App = each.key
  }
}

resource "aws_ecr_lifecycle_policy" "apps" {
  for_each   = aws_ecr_repository.apps
  repository = each.value.name

  policy = jsonencode({
    rules = [
      {
        rulePriority = 1
        description  = "Keep newest ${var.ecr_image_count_limit} images"
        selection = {
          tagStatus   = "any"
          countType   = "imageCountMoreThan"
          countNumber = var.ecr_image_count_limit
        }
        action = {
          type = "expire"
        }
      }
    ]
  })
}
