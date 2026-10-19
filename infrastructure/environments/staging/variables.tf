variable "account_id" {
  type = string
  validation {
    condition     = can(regex("^[0-9]{12}$", var.account_id))
    error_message = "Supply the authorized 12-digit AWS account ID."
  }
}

variable "availability_zone" {
  type = string
  validation {
    condition     = can(regex("^ap-southeast-1[a-z]$", var.availability_zone))
    error_message = "Use an available AZ in Singapore. Verify it before deployment."
  }
}

variable "ubuntu_ami_id" {
  type        = string
  description = "Pinned and verified Ubuntu 22.04 x86_64 AMI in Singapore. No live AMI lookup runs during validation."
  validation {
    condition     = can(regex("^ami-[a-f0-9]{8,17}$", var.ubuntu_ami_id))
    error_message = "Supply a verified Ubuntu 22.04 x86_64 AMI ID."
  }
}

variable "instance_profiles" {
  type        = object({ entry = string, app = string, db = string })
  description = "Existing approved IAM instance profile NAMES, not role ARNs. This stack does not create IAM policies."
  validation {
    condition     = alltrue([for name in values(var.instance_profiles) : can(regex("^[A-Za-z0-9+=,.@_-]{1,128}$", name))])
    error_message = "Supply an existing profile name for each host."
  }
}

variable "deployment_authorized" {
  type        = bool
  default     = false
  description = "Keep false while deployment is not authorized. Local mock tests explicitly override it."
}

variable "public_domain" {
  type        = string
  default     = null
  nullable    = true
  description = "Verified public DNS hostname, without scheme/path. Leave null before domain allocation."
  validation {
    condition     = var.public_domain == null ? true : can(regex("^[A-Za-z0-9]([A-Za-z0-9-]*[A-Za-z0-9])?(\\.[A-Za-z0-9]([A-Za-z0-9-]*[A-Za-z0-9])?)+$", var.public_domain))
    error_message = "Supply a DNS hostname without scheme, wildcard, port or path."
  }
}
