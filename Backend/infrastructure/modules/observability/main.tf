# ==============================================================================
# AI Exam Bank — Observability & Alerting Module (M5-05)
# CloudWatch Metrics, Alarms và Amazon SNS Topic định tuyến cảnh báo sự cố
# ==============================================================================

terraform {
  required_version = ">= 1.5.0"
  required_providers {
    aws = {
      source  = "hashicorp/aws"
      version = "~> 5.0"
    }
  }
}

# 1. Amazon SNS Topic tiếp nhận cảnh báo sự cố có thể hành động (Actionable Incidents)
resource "aws_sns_topic" "jobs_incident_alerts" {
  name              = "${var.project_prefix}-${var.environment}-job-alerts"
  kms_master_key_id = "alias/aws/sns" # Mã hóa dữ liệu lưu trữ (Encryption at Rest)

  tags = merge(var.tags, {
    Name        = "${var.project_prefix}-${var.environment}-job-alerts"
    Module      = "Jobs-Reliability"
    Owner       = "M5-GiaBao"
    Environment = var.environment
  })
}

# 2. SNS Topic Subscription (Email thông báo cho đội ngũ trực ca)
resource "aws_sns_topic_subscription" "email_alerts" {
  count     = var.alert_email_endpoint != "" ? 1 : 0
  topic_arn = aws_sns_topic.jobs_incident_alerts.arn
  protocol  = "email"
  endpoint  = var.alert_email_endpoint
}

# 3. CloudWatch Metric Alarm: AI Provider Circuit Breaker Tripped (CRITICAL)
resource "aws_cloudwatch_metric_alarm" "circuit_breaker_open" {
  alarm_name          = "${var.project_prefix}-${var.environment}-alarm-circuit-breaker-open"
  comparison_operator = "GreaterThanOrEqualToThreshold"
  evaluation_periods  = 1
  metric_name         = "ai_provider_circuit_breaker_state"
  namespace           = "AiExamBank/Jobs"
  period              = 60 # 1 phút
  statistic           = "Maximum"
  threshold           = 1.0 # 1: HalfOpen, 2: Open
  alarm_description   = "CẢNH BÁO KHẨN CẤP: Bộ ngắt mạch AI Bedrock đã bị ngắt (OPEN) do chuỗi lỗi liên tiếp vượt ngưỡng. Các tác vụ sinh đề thi/import tạm thời bị dừng để bảo vệ ngân sách AWS."
  treat_missing_data  = "notBreaching"

  alarm_actions = [aws_sns_topic.jobs_incident_alerts.arn]
  ok_actions    = [aws_sns_topic.jobs_incident_alerts.arn]

  dimensions = {
    Environment = var.environment
    provider    = "AWS_BEDROCK"
  }

  tags = var.tags
}

# 4. CloudWatch Metric Alarm: Tỷ lệ thất bại tác vụ tăng cao (WARNING)
resource "aws_cloudwatch_metric_alarm" "high_job_failure_rate" {
  alarm_name          = "${var.project_prefix}-${var.environment}-alarm-high-job-failures"
  comparison_operator = "GreaterThanOrEqualToThreshold"
  evaluation_periods  = 2
  metric_name         = "jobs_failed_total"
  namespace           = "AiExamBank/Jobs"
  period              = 300 # 5 phút
  statistic           = "Sum"
  threshold           = 5.0 # Lỗi >= 5 tác vụ trong 5 phút
  alarm_description   = "CẢNH BÁO: Phát hiện số lượng tác vụ nền thất bại (Failed) vượt quá ngưỡng an toàn trong 5 phút liên tiếp."
  treat_missing_data  = "notBreaching"

  alarm_actions = [aws_sns_topic.jobs_incident_alerts.arn]

  dimensions = {
    Environment = var.environment
  }

  tags = var.tags
}

# 5. CloudWatch Metric Alarm: Phát hiện Stale Worker Lease (Worker Crash / Heartbeat Lost)
resource "aws_cloudwatch_metric_alarm" "stale_lease_recoveries" {
  alarm_name          = "${var.project_prefix}-${var.environment}-alarm-stale-leases"
  comparison_operator = "GreaterThanOrEqualToThreshold"
  evaluation_periods  = 1
  metric_name         = "job_lease_recoveries_total"
  namespace           = "AiExamBank/Jobs"
  period              = 180 # 3 phút
  statistic           = "Sum"
  threshold           = 3.0 # Tự động phục hồi >= 3 job kẹt lease
  alarm_description   = "CẢNH BÁO VẬN HÀNH: Phát hiện nhiều tác vụ bị kẹt Worker Lease quá hạn. Nghi ngờ tiến trình Worker Daemon bị sập (Crash) hoặc mạng bị phân vùng."
  treat_missing_data  = "notBreaching"

  alarm_actions = [aws_sns_topic.jobs_incident_alerts.arn]

  dimensions = {
    Environment = var.environment
  }

  tags = var.tags
}

