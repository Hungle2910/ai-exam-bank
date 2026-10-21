locals {
  hosts = {
    entry = { instance_type = "t3.micro", root_gib = 16 }
    app   = { instance_type = "t3.small", root_gib = 24 }
    db    = { instance_type = "t3.large", root_gib = 24 }
  }
}

resource "aws_instance" "host" {
  for_each                    = local.hosts
  ami                         = var.ubuntu_ami_id
  instance_type               = each.value.instance_type
  subnet_id                   = aws_subnet.host[each.key].id
  associate_public_ip_address = false
  vpc_security_group_ids      = [aws_security_group.host[each.key].id]
  iam_instance_profile        = var.instance_profiles[each.key]
  disable_api_termination     = true
  # Deliberately no user_data: installing services requires reviewed, pinned
  # packages and secrets supplied outside Terraform. See scripts/README.md.
  root_block_device {
    encrypted   = true
    volume_type = "gp3"
    volume_size = each.value.root_gib
  }
  metadata_options {
    http_endpoint               = "enabled"
    http_tokens                 = "required"
    http_put_response_hop_limit = 1
    instance_metadata_tags      = "disabled"
  }
  credit_specification { cpu_credits = "standard" }
  tags = { Name = "${local.name}-${each.key}" }
  depends_on = [
    aws_route.internet, aws_route.egress, aws_route_table_association.host,
    aws_vpc_security_group_egress_rule.https
  ]
}

resource "aws_eip" "entry" {
  domain     = "vpc"
  instance   = aws_instance.host["entry"].id
  tags       = { Name = "${local.name}-entry" }
  depends_on = [aws_internet_gateway.main]
}

resource "aws_ebs_volume" "db_data" {
  availability_zone = var.availability_zone
  type              = "gp3"
  size              = 40
  encrypted         = true
  tags              = { Name = "${local.name}-sql-data" }
  lifecycle { prevent_destroy = true }
  depends_on = [terraform_data.authorization]
}

resource "aws_volume_attachment" "db_data" {
  device_name = "/dev/sdf"
  instance_id = aws_instance.host["db"].id
  volume_id   = aws_ebs_volume.db_data.id
  # Do not format or mount automatically. Verify the device/UUID and backup
  # history before using scripts/configure-sql.sh on an authorized host.
}
