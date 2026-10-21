terraform {
  required_version = ">= 1.10, < 2.0"
  required_providers {
    aws = { source = "hashicorp/aws", version = "~> 6.10.0" }
  }
  backend "s3" {}
}

provider "aws" {
  region              = "ap-southeast-1"
  allowed_account_ids = [var.account_id]
  default_tags {
    tags = { Project = "AIExamBank", Environment = "staging", Owner = "M2", ManagedBy = "Terraform", CostCenter = "demo" }
  }
}
