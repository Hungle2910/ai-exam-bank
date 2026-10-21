module "data" {
  source      = "../../modules/private-bucket"
  bucket_name = "aieb-staging-data-${var.account_id}-ap-southeast-1"
  depends_on  = [terraform_data.authorization]
}

resource "aws_vpc_endpoint" "s3" {
  vpc_id            = aws_vpc.main.id
  service_name      = "com.amazonaws.ap-southeast-1.s3"
  vpc_endpoint_type = "Gateway"
  route_table_ids   = [aws_route_table.public.id, aws_route_table.private.id]
  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [{
      Effect   = "Allow", Principal = "*"
      Action   = ["s3:ListBucket", "s3:GetObject", "s3:PutObject"]
      Resource = [module.data.arn, "${module.data.arn}/*"]
    }]
  })
  tags = { Name = "${local.name}-s3" }
}

resource "aws_cloudwatch_log_group" "host" {
  for_each          = toset(["entry", "app", "db", "ssm-sessions"])
  name              = "/aieb/staging/${each.key}"
  retention_in_days = 14
  depends_on        = [terraform_data.authorization]
}
