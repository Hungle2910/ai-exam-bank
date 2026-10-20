terraform {
  required_version = ">= 1.10, < 2.0"
  required_providers {
    aws = { source = "hashicorp/aws", version = "~> 6.10.0" }
  }
  # Local state is only for the first bootstrap. Migrate it to this bucket
  # after creation; see README.md. Never commit bootstrap state.
}

variable "account_id" {
  type = string
  validation {
    condition     = can(regex("^[0-9]{12}$", var.account_id))
    error_message = "Supply the authorized 12-digit AWS account ID."
  }
}

variable "deployment_authorized" {
  type        = bool
  default     = false
  description = "Leave false during local preparation. No deployment is authorized yet."
}

provider "aws" {
  region              = "ap-southeast-1"
  allowed_account_ids = [var.account_id]
  default_tags {
    tags = { Project = "AIExamBank", Environment = "bootstrap", Owner = "M2", ManagedBy = "Terraform", CostCenter = "demo" }
  }
}

resource "terraform_data" "authorization" {
  lifecycle {
    precondition {
      condition     = var.deployment_authorized
      error_message = "Deployment is disabled. Obtain deployment authorization before planning or applying against AWS."
    }
  }
}

module "state" {
  source      = "../modules/private-bucket"
  bucket_name = "aieb-tfstate-${var.account_id}-ap-southeast-1"
  versioning  = true
  depends_on  = [terraform_data.authorization]
}

output "backend_configuration" {
  value = {
    bucket       = module.state.name
    key          = "staging/infrastructure/terraform.tfstate"
    region       = "ap-southeast-1"
    encrypt      = true
    use_lockfile = true
  }
}
