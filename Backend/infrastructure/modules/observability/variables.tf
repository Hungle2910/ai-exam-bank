variable "project_prefix" {
  type        = string
  description = "Tiền tố định danh dự án"
  default     = "aieb"
}

variable "environment" {
  type        = string
  description = "Tên môi trường triển khai (staging, production)"
  default     = "staging"
}

variable "alert_email_endpoint" {
  type        = string
  description = "Địa chỉ email nhận thông báo sự cố qua SNS"
  default     = ""
}

variable "tags" {
  type        = map(string)
  description = "Thẻ tài nguyên AWS chung"
  default     = {}
}

