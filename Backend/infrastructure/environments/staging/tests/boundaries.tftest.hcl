mock_provider "aws" {}

variables {
  account_id            = "000000000000"
  availability_zone     = "ap-southeast-1a"
  ubuntu_ami_id         = "ami-00000000000000000"
  instance_profiles     = { entry = "test-entry", app = "test-app", db = "test-db" }
  deployment_authorized = true
}

run "private_boundaries" {
  command = plan
  assert {
    condition     = alltrue([for host in aws_instance.host : !host.associate_public_ip_address && host.metadata_options[0].http_tokens == "required"])
    error_message = "Instances must not receive automatic public IPs and must require IMDSv2."
  }
  assert {
    condition     = aws_vpc_security_group_ingress_rule.https.from_port == 443 && aws_vpc_security_group_ingress_rule.app.from_port == 8080 && aws_vpc_security_group_ingress_rule.sql.from_port == 1433
    error_message = "Only the HTTPS entry and scoped app/SQL paths belong in the baseline."
  }
  assert {
    condition     = aws_ebs_volume.db_data.encrypted && aws_ebs_volume.db_data.size == 40
    error_message = "The retained SQL data disk must be encrypted and explicitly sized."
  }
  assert {
    condition     = aws_vpc_endpoint.s3.vpc_endpoint_type == "Gateway" && aws_route.egress.destination_cidr_block == "0.0.0.0/0"
    error_message = "Use S3 gateway routing and a separate NAT default route."
  }
  assert {
    condition     = aws_cloudwatch_log_group.host["ssm-sessions"].name == "/aieb/staging/ssm-sessions" && aws_cloudwatch_log_group.host["ssm-sessions"].retention_in_days == 14
    error_message = "Session logging must match the reviewed IAM log-group name and retention."
  }
  assert {
    condition     = output.health_url == null
    error_message = "Do not invent a public health URL before domain allocation."
  }
}

run "configured_domain_url" {
  command = plan
  variables { public_domain = "staging.example.com" }
  assert {
    condition     = output.health_url == "https://staging.example.com/api/v1/admin/infrastructure/health"
    error_message = "The proposed URL must use the configured hostname and versioned health route."
  }
}

run "deployment_stays_disabled" {
  command = plan
  variables { deployment_authorized = false }
  expect_failures = [terraform_data.authorization]
}
