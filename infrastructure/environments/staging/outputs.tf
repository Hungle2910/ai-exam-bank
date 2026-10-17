output "operator_inventory" {
  description = "Operator-only inventory; never expose this through the health API."
  value = {
    region             = "ap-southeast-1"
    vpc_id             = aws_vpc.main.id
    subnet_ids         = { for name, subnet in aws_subnet.host : name => subnet.id }
    security_group_ids = { for name, group in aws_security_group.host : name => group.id }
    instance_ids       = { for name, host in aws_instance.host : name => host.id }
    entry_ip           = aws_eip.entry.public_ip
    data_bucket        = module.data.name
    db_data_volume_id  = aws_ebs_volume.db_data.id
    instance_profiles  = var.instance_profiles
    ssm_log_group      = aws_cloudwatch_log_group.host["ssm-sessions"].name
  }
}

output "health_url" {
  description = "Proposed URL only. A configured hostname does not prove DNS, TLS or authentication readiness."
  value       = var.public_domain == null ? null : "https://${var.public_domain}/api/v1/admin/infrastructure/health"
}
