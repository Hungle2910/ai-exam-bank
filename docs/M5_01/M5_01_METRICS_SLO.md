# M5-01 — Telemetry and candidate SLOs

**Status:** Design only. No metrics, dashboards, alerts or measured SLOs are deployed. Thresholds below are starting hypotheses and must be calibrated with staging load and AWS cost evidence.

| Metric | Type | Low-cardinality dimensions |
|---|---|---|
| `jobs_enqueued_total` | Counter | Job type, scope kind |
| `jobs_completed_total` / `jobs_failed_total` | Counter | Job type, error category |
| `job_attempts_total` | Counter | Job type, outcome |
| `job_queue_wait_seconds` | Histogram | Job type |
| `job_processing_seconds` | Histogram | Job type |
| `jobs_pending` / `jobs_running` | Gauge | Job type |
| `job_lease_recoveries_total` | Counter | Job type |

Do not put user IDs, school IDs, exam IDs, job IDs, payload text or error messages in metric dimensions. Structured logs may carry a correlation/job ID for authorized investigation, with retention and access controls; no raw answer keys, prompt content, credentials or personal data. Metrics go to CloudWatch only after an implementation PR provisions bounded retention, budget and alert delivery tests.

Candidate objectives for review: measure daily terminal success rate, p95 queue wait and lease recovery latency per job type. Do not publish a 99.5% success or 10-second p95 commitment until workload, sample size, exclusions and observation window are agreed and measured. Alert initially on sustained queue age, repeated terminal failures and lease recoveries, with SNS recipient and runbook approved by M2/M5. SageMaker/ML evaluation has its own optional workload budget.
