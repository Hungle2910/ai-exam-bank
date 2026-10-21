resource "terraform_data" "authorization" {
  lifecycle {
    precondition {
      condition     = var.deployment_authorized
      error_message = "AWS deployment is disabled. Keep work local until deployment is authorized."
    }
  }
}

locals {
  name    = "aieb-staging"
  subnets = { entry = "10.20.1.0/24", app = "10.20.11.0/24", db = "10.20.21.0/24" }
}

resource "aws_vpc" "main" {
  cidr_block           = "10.20.0.0/16"
  enable_dns_support   = true
  enable_dns_hostnames = true
  tags                 = { Name = local.name }
  depends_on           = [terraform_data.authorization]
}

resource "aws_subnet" "host" {
  for_each                = local.subnets
  vpc_id                  = aws_vpc.main.id
  cidr_block              = each.value
  availability_zone       = var.availability_zone
  map_public_ip_on_launch = false
  tags                    = { Name = "${local.name}-${each.key}" }
}

resource "aws_internet_gateway" "main" {
  vpc_id = aws_vpc.main.id
  tags   = { Name = local.name }
}

resource "aws_eip" "nat" {
  domain = "vpc"
  tags   = { Name = "${local.name}-nat" }
}

resource "aws_nat_gateway" "main" {
  allocation_id = aws_eip.nat.id
  subnet_id     = aws_subnet.host["entry"].id
  tags          = { Name = local.name }
  depends_on    = [aws_internet_gateway.main]
}

resource "aws_route_table" "public" {
  vpc_id = aws_vpc.main.id
  tags   = { Name = "${local.name}-public" }
}

resource "aws_route" "internet" {
  route_table_id         = aws_route_table.public.id
  destination_cidr_block = "0.0.0.0/0"
  gateway_id             = aws_internet_gateway.main.id
}

resource "aws_route_table" "private" {
  vpc_id = aws_vpc.main.id
  tags   = { Name = "${local.name}-private" }
}

resource "aws_route" "egress" {
  route_table_id         = aws_route_table.private.id
  destination_cidr_block = "0.0.0.0/0"
  nat_gateway_id         = aws_nat_gateway.main.id
}

resource "aws_route_table_association" "host" {
  for_each       = local.subnets
  subnet_id      = aws_subnet.host[each.key].id
  route_table_id = each.key == "entry" ? aws_route_table.public.id : aws_route_table.private.id
}

resource "aws_security_group" "host" {
  for_each    = local.subnets
  name        = "${local.name}-${each.key}"
  description = "Explicit traffic rules for the ${each.key} host"
  vpc_id      = aws_vpc.main.id
  tags        = { Name = "${local.name}-${each.key}" }
}

resource "aws_vpc_security_group_ingress_rule" "https" {
  security_group_id = aws_security_group.host["entry"].id
  cidr_ipv4         = "0.0.0.0/0"
  ip_protocol       = "tcp"
  from_port         = 443
  to_port           = 443
}

resource "aws_vpc_security_group_ingress_rule" "app" {
  security_group_id            = aws_security_group.host["app"].id
  referenced_security_group_id = aws_security_group.host["entry"].id
  ip_protocol                  = "tcp"
  from_port                    = 8080
  to_port                      = 8080
}

resource "aws_vpc_security_group_ingress_rule" "sql" {
  security_group_id            = aws_security_group.host["db"].id
  referenced_security_group_id = aws_security_group.host["app"].id
  ip_protocol                  = "tcp"
  from_port                    = 1433
  to_port                      = 1433
}

resource "aws_vpc_security_group_egress_rule" "https" {
  for_each          = local.subnets
  security_group_id = aws_security_group.host[each.key].id
  cidr_ipv4         = "0.0.0.0/0"
  ip_protocol       = "tcp"
  from_port         = 443
  to_port           = 443
}

resource "aws_vpc_security_group_egress_rule" "app" {
  security_group_id            = aws_security_group.host["entry"].id
  referenced_security_group_id = aws_security_group.host["app"].id
  ip_protocol                  = "tcp"
  from_port                    = 8080
  to_port                      = 8080
}

resource "aws_vpc_security_group_egress_rule" "sql" {
  security_group_id            = aws_security_group.host["app"].id
  referenced_security_group_id = aws_security_group.host["db"].id
  ip_protocol                  = "tcp"
  from_port                    = 1433
  to_port                      = 1433
}
